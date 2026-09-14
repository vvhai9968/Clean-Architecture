using System.Text.Json;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan.Model;

namespace Platform.Infrastructure.Tvan.Metadata;

/// <summary>
/// Abstract Factory: nhận (provider, MST, nghiệp vụ) và trả về một <see cref="TvanChannel"/>
/// đã resolve đầy đủ. Đây là chỗ duy nhất metadata thô được biến thành mô hình chạy được.
/// </summary>
public interface ITvanChannelFactory
{
    Task<TvanChannel> CreateAsync(string providerCode, string taxCode, string operationCode, CancellationToken ct);
}

internal sealed class TvanChannelFactory(
    ITvanMetadataStore metadata,
    ISecretProtector protector) : ITvanChannelFactory
{
    public async Task<TvanChannel> CreateAsync(
        string providerCode, string taxCode, string operationCode, CancellationToken ct)
    {
        var provider = await metadata.GetProviderAsync(providerCode, ct)
                       ?? throw new TvanConfigurationException(
                           $"Chưa cấu hình T-VAN '{providerCode}' hoặc đang bị tắt.");

        var endpoint = provider.Endpoints.FirstOrDefault(x => x.OperationCode == operationCode && x.IsActive)
                       ?? throw new TvanConfigurationException(
                           $"T-VAN '{providerCode}' chưa khai báo nghiệp vụ '{operationCode}'.");

        var credential = await metadata.GetCredentialAsync(provider.Id, taxCode, ct)
                         ?? throw new TvanConfigurationException(
                             $"Thiếu credential cho {providerCode} / MST {taxCode}.");

        var secrets = new TvanSecretBag(protector.Unprotect(credential.ProtectedSecretsJson));
        var resilience = ParseResilience(provider.ResilienceConfigJson);

        if (!Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var baseUrl))
            throw new TvanConfigurationException($"BaseUrl của '{providerCode}' không hợp lệ: {provider.BaseUrl}");

        return new TvanChannel
        {
            ProviderCode = provider.Code,
            ProviderId = provider.Id,
            BaseUrl = baseUrl,
            PathTemplate = endpoint.PathTemplate,
            Method = HttpMethod.Parse(endpoint.HttpMethod),
            ContentType = endpoint.ContentType,
            SoapAction = endpoint.SoapAction,
            BodyTemplate = endpoint.BodyTemplate,
            RequestTransforms = ParseTransforms(endpoint.RequestTransformsJson),
            Headers = provider.Headers
                .Where(h => h.EndpointId is null || h.EndpointId == endpoint.Id)
                .OrderBy(h => h.Order)
                .Select(h => new TvanHeaderRule(h.Name, h.ValueTemplate))
                .ToArray(),
            ResponseMap = TvanResponseMap.Parse(endpoint.ResponseMapJson),
            Args = ParseArgs(endpoint.ArgsJson),
            Secrets = secrets,
            Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds),
            MaxAttempts = resilience.MaxAttempts,
            BaseDelayMs = resilience.BaseDelayMs,
            Auth = new TvanAuthDescriptor(
                provider.AuthSchemeKey,
                provider.Id,
                taxCode,
                baseUrl,
                ParseJson(provider.AuthConfigJson),
                secrets),
        };
    }

    private static IReadOnlyList<TransformStep> ParseTransforms(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return TvanResponseMap.ParseTransforms(document.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            throw new TvanConfigurationException($"RequestTransformsJson không hợp lệ: {ex.Message}");
        }
    }

    private static IReadOnlyDictionary<string, string?> ParseArgs(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(
                       string.IsNullOrWhiteSpace(json) ? "{}" : json)
                   ?? new Dictionary<string, string?>();
        }
        catch (JsonException ex)
        {
            throw new TvanConfigurationException($"ArgsJson phải là object string-string: {ex.Message}");
        }
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static (int MaxAttempts, int BaseDelayMs) ParseResilience(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (3, 500);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return (
            root.TryGetProperty("maxAttempts", out var attempts) && attempts.TryGetInt32(out var max) ? max : 3,
            root.TryGetProperty("baseDelayMs", out var delay) && delay.TryGetInt32(out var ms) ? ms : 500);
    }
}
