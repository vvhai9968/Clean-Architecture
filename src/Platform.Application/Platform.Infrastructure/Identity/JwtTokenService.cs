using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Platform.Domain.Platform.Auth;
using Platform.Domain.Platform.Auth.Abstractions;
using Platform.Shared;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Identity;

/// <summary>
/// Adapter sang định dạng JWT: ký HS256, sắp claim, tính hạn. Không có quyết định nghiệp vụ nào
/// ở đây — ai được cấp token và cấp kèm quyền gì là việc của tầng Application.
/// </summary>
public class JwtTokenService(AppSettings appSettings) : IJwtTokenService
{
    private JwtSettings Jwt => appSettings.Jwt;

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        string userName,
        string email,
        IReadOnlyList<string> roleCodes,
        TvanTokenBinding? tvan = null)
    {
        var keyBytes = Encoding.UTF8.GetBytes(Jwt.SigningKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (UTF-8) for HS256.");

        var key = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(Jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.PreferredUsername, userName),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var code in roleCodes)
            claims.Add(new Claim(ClaimTypes.Role, code));

        // Nhiều claim cùng tên, giữ nguyên thứ tự ưu tiên. Đây là toàn bộ kế hoạch
        // định tuyến của account: gateway đọc thẳng từ token, không tra DB mỗi hoá đơn.
        foreach (var providerCode in tvan?.ProviderCodes ?? [])
        {
            if (!string.IsNullOrWhiteSpace(providerCode))
                claims.Add(new Claim(TvanClaimTypes.ProviderCode, providerCode));
        }

        if (!string.IsNullOrWhiteSpace(tvan?.TaxCode))
            claims.Add(new Claim(TvanClaimTypes.TaxCode, tvan.TaxCode!));

        var token = new JwtSecurityToken(
            Jwt.Issuer,
            Jwt.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
