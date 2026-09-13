using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Common;
using Platform.Application.Http;
using Platform.Application.Responses.Auth;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;
using Platform.Shared.Constants;

namespace Platform.Application.Services.Auth;

public interface IAuthService
{
    Task<ServiceOutcome<AuthResponse>> SignIn(string username, string password);
    Task<ServiceOutcome<AuthResponse>> SignUp(string username, string password, string confirmPassword, string email);
    Task<ServiceOutcome<AuthResponse>> RefreshToken(string refreshToken);
    Task<ServiceOutcome<AuthResponse>> SsoSignIn(string code, CancellationToken cancellationToken);
}

public class AuthService(
    PlatformDbContext db,
    IJwtTokenService jwtTokenService,
    IPasswordHasher<User> passwordHasher,
    AppSettings appSettings,
    IRestHttpClient httpClient) : IAuthService
{
    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    public async Task<ServiceOutcome<AuthResponse>> SignIn(string username, string password)
    {
        var email = NormalizeEmail(username);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email)
                   ?? throw new InvalidOperationException("Invalid email or password.");

        if (user.Status != UserStatus.Active)
            throw new InvalidOperationException("Account is not active.");

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Invalid email or password.");

        var roles = await db.GetRoleCodesAsync(user.Id);
        var res = await IssueTokensAndPersistRefreshAsync(user, roles);
        return ServiceOutcome<AuthResponse>.Success(res);
    }

    public async Task<ServiceOutcome<AuthResponse>> SignUp(string username, string password, string confirmPassword,
        string email)
    {
        if (password != confirmPassword)
            throw new InvalidOperationException("Passwords do not match.");

        var normalizedEmail = NormalizeEmail(email);
        var displayName = string.IsNullOrWhiteSpace(username) ? email.Trim() : username.Trim();

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new InvalidOperationException("Email is already registered.");

        var role = await db.Roles.AsNoTracking()
                       .FirstOrDefaultAsync(r => r.Code == AuthIdentityConstants.Admin)
                   ?? throw new InvalidOperationException("Default role is not configured. Seed ROLES first.");

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = displayName,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        db.UserIdentities.Add(new UserIdentity
        {
            UserId = user.Id,
            Provider = AuthIdentityConstants.Admin,
            ProviderUserId = normalizedEmail,
            ProviderEmail = normalizedEmail,
        });
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        await db.SaveChangesAsync();

        var roles = await db.GetRoleCodesAsync(user.Id);
        var res = await IssueTokensAndPersistRefreshAsync(user, roles);
        return ServiceOutcome<AuthResponse>.Success(res);
    }

    public async Task<ServiceOutcome<AuthResponse>> RefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Refresh token is required.");

        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var now = DateTimeOffset.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u =>
                       u.RefreshTokenHash == hash &&
                       u.RefreshTokenExpiresAt != null &&
                       u.RefreshTokenExpiresAt > now)
                   ?? throw new InvalidOperationException("Invalid or expired refresh token.");

        var roles = await db.GetRoleCodesAsync(user.Id);
        var res = await IssueTokensAndPersistRefreshAsync(user, roles);
        return ServiceOutcome<AuthResponse>.Success(res);
    }

    public async Task<ServiceOutcome<AuthResponse>> SsoSignIn(string code, CancellationToken cancellationToken)
    {
        var azureAd = appSettings.AzureAd;
        var tokenData = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
        var idToken = tokenData.GetProperty("id_token").GetString();

        if (string.IsNullOrEmpty(idToken))
            throw new InvalidOperationException("ID token is missing from the SSO response.");

        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(idToken);

        if (jwtSecurityToken.ValidTo < DateTime.UtcNow)
            throw new InvalidOperationException("ID token has expired.");

        if (!string.IsNullOrEmpty(azureAd.ClientId) && !jwtSecurityToken.Audiences.Contains(azureAd.ClientId))
            throw new InvalidOperationException("ID token audience is invalid.");

        var email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type is "email" or "preferred_username")?.Value;
        var displayName = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? email;

        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("Email not found in SSO token.");

        var normalizedEmail = NormalizeEmail(email);
        var providerUserId = jwtSecurityToken.Subject ?? normalizedEmail;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            var role = await db.Roles.AsNoTracking()
                           .FirstOrDefaultAsync(r => r.Code == AuthIdentityConstants.User, cancellationToken)
                       ?? throw new InvalidOperationException("Default role is not configured. Seed ROLES first.");

            user = new User
            {
                Email = normalizedEmail,
                DisplayName = displayName ?? normalizedEmail,
                Status = UserStatus.Active,
                PasswordHash = string.Empty,
            };
            db.Users.Add(user);
            db.UserIdentities.Add(new UserIdentity
            {
                UserId = user.Id,
                Provider = "AzureAd",
                ProviderUserId = providerUserId,
                ProviderEmail = normalizedEmail,
            });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Account is not active.");
        }

        var roles = await db.GetRoleCodesAsync(user.Id, cancellationToken);
        var res = await IssueTokensAndPersistRefreshAsync(user, roles);
        return ServiceOutcome<AuthResponse>.Success(res);
    }

    private async Task<System.Text.Json.JsonElement> ExchangeAuthorizationCodeAsync(string code,
        CancellationToken cancellationToken)
    {
        var azureAd = appSettings.AzureAd;

        if (string.IsNullOrEmpty(azureAd.TenantId) || string.IsNullOrEmpty(azureAd.ClientId) ||
            string.IsNullOrEmpty(azureAd.ClientSecret))
            throw new InvalidOperationException("Azure AD configuration is missing.");

        var requestBody = new Dictionary<string, string>
        {
            { "client_id", azureAd.ClientId },
            { "scope", azureAd.Scope ?? "openid profile email" },
            { "redirect_uri", azureAd.RedirectUri ?? string.Empty },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "client_secret", azureAd.ClientSecret }
        };

        var requestContent = new FormUrlEncodedContent(requestBody);
        var response = await httpClient.PostFormAsync(azureAd.OAuth2Endpoint!, requestContent, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseContent);
    }

    private async Task<AuthResponse> IssueTokensAndPersistRefreshAsync(User user, IReadOnlyList<string> roles)
    {
        var (accessToken, _) = jwtTokenService.CreateAccessToken(user.Id, user.Email, roles);
        var refreshPlain = jwtTokenService.CreateRefreshToken();
        var refreshHash = jwtTokenService.HashRefreshToken(refreshPlain);
        var expires = DateTimeOffset.UtcNow.AddDays(appSettings.Jwt.RefreshTokenDays);

        user.RefreshTokenHash = refreshHash;
        user.RefreshTokenExpiresAt = expires;
        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshPlain);
    }
}
