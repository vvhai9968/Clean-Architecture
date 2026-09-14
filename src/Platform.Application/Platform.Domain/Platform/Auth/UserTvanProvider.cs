using Platform.Shared.Common;

namespace Platform.Domain.Platform.Auth;

/// <summary>
/// Một nhà truyền nhận mà account được phép đi qua.
/// Một account có nhiều dòng; <see cref="Priority"/> nhỏ hơn được thử trước.
/// </summary>
public class UserTvanProvider : BaseEntity
{
    public Guid UserId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;

    /// <summary>0 là nhà truyền nhận chính; các số lớn hơn là phương án dự phòng theo thứ tự.</summary>
    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;
}
