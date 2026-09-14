namespace Platform.Domain.Platform.Auth.Abstractions;

/// <summary>
/// Phát access token. Đây là một cổng ra thế giới bên ngoài: nội dung token là hợp đồng
/// nghiệp vụ (ai, vai trò gì, đi qua nhà truyền nhận nào), còn cách ký và định dạng JWT
/// là chi tiết kỹ thuật nằm ở tầng Infrastructure.
/// </summary>
public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        string userName,
        string email,
        IReadOnlyList<string> roleCodes,
        TvanTokenBinding? tvan = null);
}
