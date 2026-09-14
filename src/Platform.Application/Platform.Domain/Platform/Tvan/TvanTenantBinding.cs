using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>
/// Định tuyến dự phòng theo MST, dùng khi JWT không mang claim tvan_provider
/// (vd: tiến trình nền, hệ thống nội bộ không có user context).
/// Một MST có nhiều dòng; <see cref="Priority"/> nhỏ hơn được thử trước.
/// </summary>
public class TvanTenantBinding : BaseEntity
{
    public string TaxCode { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}
