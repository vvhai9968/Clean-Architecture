using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Tvan.Auth;

namespace Platform.Infrastructure.Tvan.Http;

/// <summary>
/// Gắn xác thực động: đọc AuthSchemeKey từ channel của chính request, resolve strategy
/// tương ứng trong keyed registry. Nếu NCC trả 401/403 thì xoá token và thử lại đúng một lần.
/// </summary>
internal sealed class TvanAuthDelegatingHandler(
    IServiceProvider provider,
    ILogger<TvanAuthDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Options.TryGetValue(TvanRequestOptions.Channel, out var channel))
            return await base.SendAsync(request, ct);

        var strategy = provider.GetKeyedService<ITvanAuthStrategy>(channel.Auth.SchemeKey)
                       ?? throw new TvanConfigurationException(
                           $"Chưa đăng ký auth scheme '{channel.Auth.SchemeKey}'.");

        await strategy.ApplyAsync(request, channel.Auth, ct);
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            return response;

        logger.LogWarning("T-VAN {Provider} trả {Status}, thử đăng nhập lại một lần.",
            channel.ProviderCode, (int)response.StatusCode);

        response.Dispose();
        await strategy.InvalidateAsync(channel.Auth, ct);

        var retry = await request.CloneAsync(ct);
        await strategy.ApplyAsync(retry, channel.Auth, ct);
        return await base.SendAsync(retry, ct);
    }
}
