namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Gửi một thông điệp qua danh sách nhà truyền nhận theo thứ tự ưu tiên, tự chuyển sang
/// nhà kế tiếp khi nhà hiện tại không hoạt động. Chỉ khi không còn nhà nào khả dụng
/// mới báo lỗi truyền nhận.
/// <para>
/// <see cref="ITvanGateway"/> vẫn là lời gọi tới đúng MỘT nhà truyền nhận; toàn bộ
/// chính sách chuyển đổi nằm ở đây để tầng Application không phải biết tới nó.
/// </para>
/// </summary>
public interface ITvanDispatcher
{
    Task<TvanFailoverResult> DispatchAsync(TvanFailoverRequest request, CancellationToken ct);
}

public sealed record TvanFailoverRequest
{
    /// <summary>Danh sách nhà truyền nhận đã sắp theo thứ tự ưu tiên.</summary>
    public required IReadOnlyList<string> ProviderCodes { get; init; }

    public required string TaxCode { get; init; }
    public required string OperationCode { get; init; }
    public required string Xml { get; init; }
    public required string CorrelationKey { get; init; }

    public IReadOnlyDictionary<string, string?> Fields { get; init; } =
        new Dictionary<string, string?>();

    public Guid? TransactionId { get; init; }
}

/// <param name="Final">
/// Câu trả lời thật sự từ một nhà truyền nhận — chấp nhận hoặc từ chối vì lý do nghiệp vụ.
/// Null nghĩa là không nhà nào phản hồi được: lỗi truyền nhận.
/// </param>
public sealed record TvanFailoverResult(
    TvanDispatchResult? Final,
    string? ProviderCode,
    IReadOnlyList<TvanProviderAttempt> Attempts)
{
    public bool Accepted => Final?.Accepted == true;

    /// <summary>Đã thử hết danh sách mà không nhà nào hoạt động.</summary>
    public bool AllProvidersUnavailable => Final is null;

    public string Describe() => AllProvidersUnavailable
        ? "Không còn nhà truyền nhận nào khả dụng: " +
          string.Join(" | ", Attempts.Select(a => $"{a.ProviderCode}: {a.Reason}"))
        : $"[{ProviderCode}] {Final!.ResultCode}: {Final.Message}";
}

/// <param name="Skipped">
/// True khi nhà truyền nhận này bị bỏ qua vì không hoạt động (lỗi mạng, 5xx, không đăng nhập
/// được, chưa khai báo nghiệp vụ). False khi nhà này đã trả lời thật sự.
/// </param>
public sealed record TvanProviderAttempt(
    string ProviderCode,
    bool Skipped,
    string? ResultCode,
    string Reason,
    long ElapsedMs);
