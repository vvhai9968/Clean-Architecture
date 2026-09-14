using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>Log từng lần gọi HTTP. Secret đã được mask trước khi ghi.</summary>
public class TvanTransactionAttempt : BaseEntity
{
    public Guid TransactionId { get; set; }
    public int AttemptNo { get; set; }

    /// <summary>Nhà truyền nhận của đúng lần gọi này — khác nhau giữa các lần khi failover.</summary>
    public string ProviderCode { get; set; } = string.Empty;
    public string RequestUri { get; set; } = string.Empty;
    public string? RequestHeaders { get; set; }
    public int? HttpStatus { get; set; }
    public string? ResponseSnapshot { get; set; }
    public long ElapsedMs { get; set; }
    public string? Error { get; set; }
}
