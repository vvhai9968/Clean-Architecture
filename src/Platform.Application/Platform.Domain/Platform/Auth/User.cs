using Platform.Shared.Common;

namespace Platform.Domain.Platform.Auth;

public class User : BaseEntity
{
    /// <summary>Định danh đăng nhập. Không dùng email để đăng nhập nữa.</summary>
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>MST người nộp thuế gắn với account. Phát ra JWT dưới claim tvan_taxcode.</summary>
    public string? TaxCode { get; set; }

    /// <summary>
    /// Danh sách nhà truyền nhận của account, xếp theo thứ tự ưu tiên.
    /// Phát ra JWT thành nhiều claim tvan_provider, giữ nguyên thứ tự.
    /// </summary>
    public ICollection<UserTvanProvider> TvanProviders { get; set; } = new List<UserTvanProvider>();
}
