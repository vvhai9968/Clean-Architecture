using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Shared;

namespace Platform.Infrastructure.Tvan.Outbox;

/// <summary>
/// Tiến trình nền gửi lại các giao dịch còn treo trong outbox.
/// Nhờ nó, mất kết nối T-VAN chỉ làm chậm hoá đơn chứ không làm mất hoá đơn —
/// và đợt phát hành cuối tháng không bị dồn tải đồng bộ vào request của người dùng.
/// </summary>
public sealed class TvanOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    AppSettings appSettings,
    ILogger<TvanOutboxDispatcher> logger) : BackgroundService
{
    private readonly string _workerId = $"{System.Environment.MachineName}:{System.Environment.ProcessId}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = appSettings.Tvan;
        if (!options.OutboxEnabled)
        {
            logger.LogInformation("T-VAN outbox dispatcher đang tắt theo cấu hình.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, options.OutboxPollSeconds));
        logger.LogInformation("T-VAN outbox dispatcher khởi động ({Worker}), chu kỳ {Interval}s.",
            _workerId, interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DrainAsync(options, stoppingToken);

                // Còn hàng thì quay lại ngay, không chờ hết chu kỳ.
                if (processed >= options.OutboxBatchSize) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vòng lặp outbox lỗi, sẽ thử lại ở chu kỳ sau.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }

        logger.LogInformation("T-VAN outbox dispatcher đã dừng.");
    }

    private async Task<int> DrainAsync(TvanSettings options, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITvanTransactionStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ITvanDispatcher>();

        var batch = await store.LeaseAsync(
            _workerId, options.OutboxBatchSize, TimeSpan.FromMinutes(options.OutboxLeaseMinutes), ct);

        if (batch.Count == 0) return 0;

        logger.LogInformation("Outbox: nhận {Count} giao dịch để gửi.", batch.Count);

        foreach (var transaction in batch)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Dùng lại đúng kế hoạch định tuyến đã chụp lúc nhận giao dịch,
                // nên lần gửi lại vẫn chuyển nhà theo thứ tự mà account được cấp khi đó.
                var result = await dispatcher.DispatchAsync(new TvanFailoverRequest
                {
                    ProviderCodes = transaction.ProviderPlan,
                    TaxCode = transaction.TaxCode,
                    OperationCode = transaction.OperationCode,
                    Xml = transaction.RequestXml,
                    CorrelationKey = transaction.CorrelationKey,
                    Fields = transaction.Fields,
                    TransactionId = transaction.Id,
                }, ct);

                if (result.AllProvidersUnavailable)
                {
                    // Cả danh sách đều chết — đây là sự cố hạ tầng, đáng chờ rồi thử lại.
                    await store.RescheduleAsync(
                        transaction.Id, result.Describe(), options.OutboxMaxAttempts, ct);
                    continue;
                }

                // Có nhà trả lời rồi thì chốt luôn, kể cả khi là từ chối nghiệp vụ:
                // gửi lại cũng nhận đúng lời từ chối ấy, kèm rủi ro hoá đơn trùng trên CQT.
                await store.CompleteAsync(transaction.Id, result, ct);
            }
            catch (TvanRoutingException ex)
            {
                logger.LogError(ex, "Giao dịch {Key} không còn kế hoạch định tuyến hợp lệ",
                    transaction.CorrelationKey);
                await store.RescheduleAsync(transaction.Id, ex.Message, maxAttempts: 0, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Gửi lại thất bại {Key}, lần thử {Attempt}",
                    transaction.CorrelationKey, transaction.AttemptCount + 1);
                await store.RescheduleAsync(transaction.Id, ex.Message, options.OutboxMaxAttempts, ct);
            }
        }

        return batch.Count;
    }
}
