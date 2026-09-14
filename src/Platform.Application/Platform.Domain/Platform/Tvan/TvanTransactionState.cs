namespace Platform.Domain.Platform.Tvan;

/// <summary>Vòng đời một giao dịch T-VAN trong outbox.</summary>
public enum TvanTransactionState
{
    /// <summary>Đã ghi nhận, chờ dispatcher gửi đi.</summary>
    Pending = 0,

    /// <summary>Đang được một worker giữ và gửi.</summary>
    Dispatching = 1,

    /// <summary>T-VAN đã nhận. LƯU Ý: chưa chắc CQT đã chấp nhận.</summary>
    Accepted = 2,

    /// <summary>T-VAN trả về mã lỗi nghiệp vụ — không retry.</summary>
    Rejected = 3,

    /// <summary>Lỗi kỹ thuật, đã hết số lần thử — cần can thiệp thủ công.</summary>
    Failed = 4,
}
