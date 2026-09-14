using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>
/// Outbox + audit trail. Đây là nguồn sự thật để đối soát với CQT,
/// và là bản ghi được commit cùng transaction DB của nghiệp vụ gọi đến.
/// </summary>
public class TvanTransaction : BaseEntity
{
    /// <summary>Nhà truyền nhận đã (hoặc đang) xử lý. Cập nhật lại khi failover sang nhà khác.</summary>
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>
    /// Ảnh chụp danh sách ưu tiên lúc nhận giao dịch, phân tách bằng dấu phẩy.
    /// Outbox dispatcher dùng lại chính danh sách này khi gửi lại, để lần thử sau
    /// vẫn failover đúng thứ tự mà account được cấp tại thời điểm phát sinh.
    /// </summary>
    public string ProviderPlan { get; set; } = string.Empty;

    public string TaxCode { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;

    /// <summary>MTDiep / transId do hệ thống sinh — idempotency key, duy nhất toàn hệ thống.</summary>
    public string CorrelationKey { get; set; } = string.Empty;

    public TvanTransactionState State { get; set; } = TvanTransactionState.Pending;

    /// <summary>XML chuẩn QĐ 1450 đã ký số.</summary>
    public string RequestXml { get; set; } = string.Empty;

    /// <summary>Biến bổ sung đổ vào template, serialize JSON.</summary>
    public string FieldsJson { get; set; } = "{}";

    /// <summary>Nguồn định tuyến: jwt-claim | tenant-binding | override.</summary>
    public string RouteSource { get; set; } = string.Empty;

    /// <summary>maThongdiep / MTDiep do NCC trả về.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>Mã kết quả của NCC: "00" | "200" | ...</summary>
    public string? ResultCode { get; set; }

    public string? ResultMessage { get; set; }

    public int AttemptCount { get; set; }

    /// <summary>Thời điểm được phép thử lại (exponential backoff).</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Chống hai worker cùng giữ một bản ghi.</summary>
    public string? LockedBy { get; set; }

    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<TvanTransactionAttempt> Attempts { get; set; } = new List<TvanTransactionAttempt>();
}
