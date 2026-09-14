namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Outbox store. Tách khỏi DbContext để handler unit-test được mà không cần EF.
/// </summary>
public interface ITvanTransactionStore
{
    /// <summary>
    /// Tìm giao dịch theo idempotency key. Không kèm mã nhà truyền nhận: cùng một thông điệp
    /// nghiệp vụ có thể đã chuyển qua nhiều nhà khi failover, nhưng vẫn là một giao dịch.
    /// </summary>
    Task<TvanTransactionSnapshot?> FindAsync(string correlationKey, CancellationToken ct);

    /// <summary>Ghi nhận giao dịch ở trạng thái Pending. Đây là điểm commit của outbox.</summary>
    Task<TvanTransactionSnapshot> EnqueueAsync(
        TvanFailoverRequest request, string source, CancellationToken ct);

    /// <summary>Giữ (lock) tối đa <paramref name="batchSize"/> giao dịch đến hạn gửi.</summary>
    Task<IReadOnlyList<TvanTransactionSnapshot>> LeaseAsync(
        string workerId, int batchSize, TimeSpan leaseDuration, CancellationToken ct);

    /// <summary>Ghi kết quả cuối cùng (Accepted hoặc Rejected) kèm nhà truyền nhận đã xử lý.</summary>
    Task CompleteAsync(Guid transactionId, TvanFailoverResult result, CancellationToken ct);

    /// <summary>Ghi lỗi kỹ thuật và đặt lịch thử lại; hết lượt thì chuyển Failed.</summary>
    Task RescheduleAsync(Guid transactionId, string error, int maxAttempts, CancellationToken ct);

    Task<TvanTransactionSnapshot?> GetAsync(Guid transactionId, CancellationToken ct);
}

public sealed record TvanTransactionSnapshot(
    Guid Id,
    string ProviderCode,
    IReadOnlyList<string> ProviderPlan,
    string TaxCode,
    string OperationCode,
    string CorrelationKey,
    TvanTransactionState State,
    string RequestXml,
    IReadOnlyDictionary<string, string?> Fields,
    string? ProviderReference,
    string? ResultCode,
    string? ResultMessage,
    int AttemptCount);
