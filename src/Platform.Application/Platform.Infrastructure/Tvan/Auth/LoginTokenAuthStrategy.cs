using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Tvan.Model;
using Platform.Infrastructure.Tvan.Response;
using Platform.Infrastructure.Tvan.Templating;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan.Auth;

/// <summary>
/// Gọi API login lấy token rồi gắn vào header — toàn bộ hành vi lái bằng metadata.
/// Một class này phủ được cả HILO (<c>Bearer {token}</c>) lẫn Minvoice
/// (<c>Bear {token};{ma_dvcs}</c>) mà không có một dòng if nào theo tên nhà cung cấp.
/// </summary>
public sealed class LoginTokenAuthStrategy(
    IHttpClientFactory httpClientFactory,
    ITemplateRenderer renderer,
    IResponseValueExtractorRegistry extractors,
    IDistributedCache cache,
    ILogger<LoginTokenAuthStrategy> logger) : ITvanAuthStrategy
{
    /// <summary>Chống thundering herd: nhiều hoá đơn gửi song song chỉ login một lần.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async ValueTask ApplyAsync(HttpRequestMessage request, TvanAuthDescriptor auth, CancellationToken ct)
    {
        var config = LoginTokenConfig.From(auth.Config);
        var token = await GetOrFetchTokenAsync(auth, config, ct);

        var headerValue = renderer.Render(
            config.HeaderFormat, TemplateScope.ForAuth(token, auth.Secrets.AsScope()));

        request.Headers.Remove(config.HeaderName);
        request.Headers.TryAddWithoutValidation(config.HeaderName, headerValue);
    }

    public ValueTask InvalidateAsync(TvanAuthDescriptor auth, CancellationToken ct) =>
        new(cache.RemoveAsync(CacheKey(auth), ct));

    private async Task<string> GetOrFetchTokenAsync(
        TvanAuthDescriptor auth, LoginTokenConfig config, CancellationToken ct)
    {
        var key = CacheKey(auth);

        var cached = await cache.GetStringAsync(key, ct);
        if (!string.IsNullOrEmpty(cached)) return cached;

        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            cached = await cache.GetStringAsync(key, ct);
            if (!string.IsNullOrEmpty(cached)) return cached;

            var token = await LoginAsync(auth, config, ct);

            // Trừ hao 10% TTL để tránh token hết hạn ngay giữa lúc đang gửi.
            await cache.SetStringAsync(key, token, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(30, config.TtlSeconds * 0.9)),
            }, ct);

            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> LoginAsync(TvanAuthDescriptor auth, LoginTokenConfig config, CancellationToken ct)
    {
        // Dùng client RIÊNG không gắn TvanAuthDelegatingHandler, nếu không sẽ đệ quy vô hạn.
        var client = httpClientFactory.CreateClient(TvanHttpClients.Login);

        var body = renderer.Render(
            config.BodyTemplate, TemplateScope.ForAuth(string.Empty, auth.Secrets.AsScope()));

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(auth.BaseUrl, config.LoginPath))
        {
            Content = new StringContent(body, Encoding.UTF8, config.ContentType),
        };

        using var response = await client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new TvanAuthenticationException(
                $"Đăng nhập T-VAN thất bại (HTTP {(int)response.StatusCode}).");
        }

        var extractor = extractors.Resolve(config.Format);

        if (config.SuccessPath is not null)
        {
            var status = extractor.Extract(raw, config.SuccessPath);
            if (!config.SuccessValues.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                var message = config.MessagePath is null ? null : extractor.Extract(raw, config.MessagePath);
                throw new TvanAuthenticationException(
                    $"Đăng nhập T-VAN thất bại (code={status}): {message}");
            }
        }

        var token = extractor.Extract(raw, config.TokenPath);
        if (string.IsNullOrWhiteSpace(token))
            throw new TvanAuthenticationException($"Không tìm thấy token tại đường dẫn '{config.TokenPath}'.");

        logger.LogInformation("Lấy token T-VAN thành công cho provider {ProviderId} / MST {TaxCode}",
            auth.ProviderId, auth.TaxCode);

        return token;
    }

    private static string CacheKey(TvanAuthDescriptor auth) =>
        $"tvan:token:{auth.ProviderId}:{auth.TaxCode}";
}

/// <summary>Hình dạng của TvanProvider.AuthConfigJson khi AuthSchemeKey = "login-token".</summary>
public sealed record LoginTokenConfig(
    string LoginPath,
    string BodyTemplate,
    string ContentType,
    string Format,
    string TokenPath,
    string? SuccessPath,
    string[] SuccessValues,
    string? MessagePath,
    string HeaderName,
    string HeaderFormat,
    int TtlSeconds)
{
    public static LoginTokenConfig From(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new TvanConfigurationException("AuthConfigJson của scheme 'login-token' phải là một JSON object.");

        return new LoginTokenConfig(
            LoginPath: Required(element, "loginPath"),
            BodyTemplate: Required(element, "bodyTemplate"),
            ContentType: Optional(element, "contentType") ?? "application/json",
            Format: Optional(element, "format") ?? TvanResponseFormats.Json,
            TokenPath: Required(element, "tokenPath"),
            SuccessPath: Optional(element, "successPath"),
            SuccessValues: element.TryGetProperty("successValues", out var values) &&
                           values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(x => x.ToString()).ToArray()
                : [],
            MessagePath: Optional(element, "messagePath"),
            HeaderName: Optional(element, "headerName") ?? "Authorization",
            HeaderFormat: Required(element, "headerFormat"),
            TtlSeconds: element.TryGetProperty("ttlSeconds", out var ttl) && ttl.TryGetInt32(out var seconds)
                ? seconds
                : 1800);
    }

    private static string Required(JsonElement element, string name) =>
        Optional(element, name)
        ?? throw new TvanConfigurationException($"AuthConfigJson thiếu thuộc tính bắt buộc '{name}'.");

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
