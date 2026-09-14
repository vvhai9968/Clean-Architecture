using System.Net;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Tvan.Http;

/// <summary>
/// Retry có backoff + jitter cho lỗi tạm thời (5xx, 408, 429, lỗi mạng).
/// CỐ Ý không retry lỗi nghiệp vụ 4xx: hoá đơn gửi trùng lên CQT là sự cố nghiêm trọng hơn
/// nhiều so với một lần gửi hụt — trường hợp đó để outbox dispatcher quyết định.
/// </summary>
internal sealed class TvanRetryDelegatingHandler(ILogger<TvanRetryDelegatingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Options.TryGetValue(TvanRequestOptions.Channel, out var channel))
            return await base.SendAsync(request, ct);

        var maxAttempts = Math.Max(1, channel.MaxAttempts);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var current = attempt == 1 ? request : await request.CloneAsync(ct);
            current.Options.Set(TvanRequestOptions.AttemptNo, attempt);

            try
            {
                var response = await base.SendAsync(current, ct);

                if (attempt == maxAttempts || !IsTransient(response.StatusCode))
                    return response;

                logger.LogWarning("T-VAN {Provider} trả {Status}, thử lại lần {Attempt}/{Max}.",
                    channel.ProviderCode, (int)response.StatusCode, attempt + 1, maxAttempts);
                response.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                lastError = ex;
                if (attempt == maxAttempts) throw;

                logger.LogWarning(ex, "Lỗi mạng khi gọi T-VAN {Provider}, thử lại lần {Attempt}/{Max}.",
                    channel.ProviderCode, attempt + 1, maxAttempts);
            }

            await Task.Delay(BackoffFor(attempt, channel.BaseDelayMs), ct);
        }

        throw lastError ?? new TvanProtocolException($"Không gọi được T-VAN {channel.ProviderCode}.");
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError;

    private static TimeSpan BackoffFor(int attempt, int baseDelayMs)
    {
        var exponential = baseDelayMs * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, baseDelayMs);
        return TimeSpan.FromMilliseconds(exponential + jitter);
    }
}
