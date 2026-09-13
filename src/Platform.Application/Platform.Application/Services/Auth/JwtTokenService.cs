using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Platform.Shared;

namespace Platform.Application.Services.Auth;

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(Guid userId, string email, IReadOnlyList<string> roleCodes);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public class JwtTokenService(AppSettings appSettings) : IJwtTokenService
{
    private JwtSettings Jwt => appSettings.Jwt;

    public (string AccessToken, DateTimeOffset ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        string email,
        IReadOnlyList<string> roleCodes)
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
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        foreach (var code in roleCodes)
            claims.Add(new Claim(ClaimTypes.Role, code));

        var token = new JwtSecurityToken(
            Jwt.Issuer,
            Jwt.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }

    public string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
