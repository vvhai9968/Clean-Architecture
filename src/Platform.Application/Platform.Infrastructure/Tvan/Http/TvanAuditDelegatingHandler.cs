using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan;
using Platform.Infrastructure.Persistence.PlatformContext;

namespace Platform.Infrastructure.Tvan.Http;

/// <summary>
/// Ghi log từng lần gọi vào TvanTransactionAttempts để đối soát với CQT.
/// Header nhạy cảm được mask; body request KHÔNG được ghi vì có thể chứa dữ liệu đã mã hoá lẫn secret.
/// </summary>
internal sealed class TvanAuditDelegatingHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<TvanAuditDelegatingHandler> logger) : DelegatingHandler
{
    private static readonly string[] SensitiveHeaders = ["Authorization", "Cookie", "X-Api-Key"];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Options.TryGetValue(TvanRequestOptions.TransactionId, out var transactionId) ||
            !request.Options.TryGetValue(TvanRequestOptions.Channel, out var channel))
        {
            return await base.SendAsync(request, ct);
        }

        request.Options.TryGetValue(TvanRequestOptions.AttemptNo, out var attemptNo);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? error = null;
        string? responseBody = null;

        try
        {
            response = await base.SendAsync(request, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);
            return response;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await WriteAttemptAsync(new TvanTransactionAttempt
            {
                TransactionId = transactionId,
                AttemptNo = attemptNo == 0 ? 1 : attemptNo,
                ProviderCode = channel.ProviderCode,
                RequestUri = request.RequestUri?.ToString() ?? string.Empty,
                RequestHeaders = MaskHeaders(request),
                HttpStatus = response is null ? null : (int)response.StatusCode,
                ResponseSnapshot = Truncate(responseBody, 8000),
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = Truncate(error, 4000),
            });
        }
    }

    private async Task WriteAttemptAsync(TvanTransactionAttempt attempt)
    {
        try
        {
            // Scope riêng: audit phải ghi được ngay cả khi scope nghiệp vụ đã rollback.
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.TvanTransactionAttempts.Add(attempt);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Không bao giờ để lỗi audit làm hỏng nghiệp vụ gửi hoá đơn.
            logger.LogError(ex, "Không ghi được audit log cho giao dịch {TransactionId}", attempt.TransactionId);
        }
    }

    private static string MaskHeaders(HttpRequestMessage request) =>
        string.Join('\n', request.Headers.Select(header =>
            SensitiveHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase)
                ? $"{header.Key}: ***"
                : $"{header.Key}: {string.Join(", ", header.Value)}"));

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max] + "...[truncated]";
}
