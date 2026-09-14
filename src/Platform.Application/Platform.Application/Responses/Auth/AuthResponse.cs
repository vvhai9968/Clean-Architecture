namespace Platform.Application.Responses.Auth;

/// <summary>
/// Không có refresh token: hết hạn thì đăng nhập lại. <paramref name="ExpiresAtUtc"/>
/// để client biết trước thời điểm đó thay vì phải đợi lỗi 401.
/// <paramref name="TvanProviderCodes"/> trả kèm để phần mềm Invoice hiển thị được
/// nó đang đi qua nhà nào, dù không cần dùng để gọi API.
/// </summary>
public record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> TvanProviderCodes);

public record CreatedUserResponse(
    Guid Id,
    string UserName,
    string Email,
    string DisplayName,
    string RoleCode,
    IReadOnlyList<string> TvanProviderCodes);
