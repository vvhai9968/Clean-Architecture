namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Cổng duy nhất tầng Application nhìn thấy. Không lộ HttpClient / SOAP / AES / provider nào cả.
/// Implementation nằm ở Infrastructure và được lái hoàn toàn bằng metadata trong DB.
/// </summary>
public interface ITvanGateway
{
    Task<TvanDispatchResult> DispatchAsync(TvanDispatchRequest request, CancellationToken ct);
}

public sealed record TvanDispatchRequest
{
    public required string ProviderCode { get; init; }
    public required string TaxCode { get; init; }
    public required string OperationCode { get; init; }

    /// <summary>XML chuẩn QĐ 1450 (đã ký số) — dạng canonical duy nhất trong hệ thống.</summary>
    public required string Xml { get; init; }

    /// <summary>MTDiep / transId — idempotency key.</summary>
    public required string CorrelationKey { get; init; }

    /// <summary>Biến bổ sung đổ vào template (PBan, MLTDiep, SLuong, MNNhan...).</summary>
    public IReadOnlyDictionary<string, string?> Fields { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>Id giao dịch outbox, để audit handler gắn log vào đúng bản ghi.</summary>
    public Guid? TransactionId { get; init; }
}

public sealed record TvanDispatchResult(
    bool Accepted,
    string ProviderCode,
    string? ProviderReference,
    string? ResultCode,
    string? Message,
    int HttpStatus,
    string? RawResponse)
{
    /// <summary>
    /// True khi lỗi mang tính kỹ thuật/tạm thời (5xx, timeout, 429) — đáng retry.
    /// Lỗi nghiệp vụ (NCC trả mã lỗi rõ ràng) thì KHÔNG retry, tránh gửi trùng lên CQT.
    /// </summary>
    public bool IsTransient => !Accepted && HttpStatus is 0 or 408 or 429 or >= 500;
}

/// <summary>
/// Xử lý webhook NCC gọi về (vd HILO CallBackUrl). Cách bóc payload nằm trong
/// TvanProvider.CallbackMapJson nên thêm NCC mới không phải sửa code.
/// </summary>
public interface ITvanCallbackProcessor
{
    Task<TvanCallbackResult> ProcessAsync(TvanCallbackRequest request, CancellationToken ct);
}

public sealed record TvanCallbackRequest(
    string ProviderCode,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers);

public sealed record TvanCallbackResult(
    bool Handled,
    string? CorrelationKey,
    string? ResultCode,
    string? Message);
