using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Responses.Auth;
using Platform.Domain.Platform.Auth;
using Platform.Domain.Platform.Auth.Abstractions;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;

namespace Platform.Application.MediatR.Auth.Queries;

/// <summary>Đăng nhập bằng username. Không còn SSO và không có refresh token.</summary>
public record SignInQuery(string UserName, string Password) : IRequest<IResult>
{
    internal sealed class Handler(
        PlatformDbContext db,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService) : IRequestHandler<SignInQuery, IResult>
    {
        public async Task<IResult> Handle(SignInQuery request, CancellationToken ct)
        {
            var outcome = await Execute(request, ct);
            return outcome.ToIResult("Đăng nhập thành công");
        }

        private async Task<ServiceOutcome<AuthResponse>> Execute(SignInQuery request, CancellationToken ct)
        {
            var userName = request.UserName.Trim().ToLowerInvariant();

            var user = await db.Users
                .Include(u => u.TvanProviders)
                .FirstOrDefaultAsync(u => u.UserName == userName, ct);

            // Cùng một thông báo cho sai tài khoản và sai mật khẩu, để không dò được
            // username nào có thật.
            if (user is null ||
                passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                    == PasswordVerificationResult.Failed)
            {
                return ServiceOutcome<AuthResponse>.Fail(
                    "Tài khoản hoặc mật khẩu không đúng.", HttpStatusCode.Unauthorized);
            }

            if (user.Status != UserStatus.Active)
                return ServiceOutcome<AuthResponse>.Fail("Tài khoản đang bị khoá.", HttpStatusCode.Forbidden);

            var roles = await db.GetRoleCodesAsync(user.Id, ct);

            var providerCodes = user.TvanProviders
                .Where(x => x.IsActive)
                .OrderBy(x => x.Priority)
                .Select(x => x.ProviderCode)
                .ToArray();

            var (accessToken, expiresAt) = jwtTokenService.CreateAccessToken(
                user.Id, user.UserName, user.Email, roles,
                new TvanTokenBinding(providerCodes, user.TaxCode));

            user.LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return ServiceOutcome<AuthResponse>.Success(
                new AuthResponse(accessToken, expiresAt, roles, providerCodes));
        }
    }
}
