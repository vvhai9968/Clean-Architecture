using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Responses.Auth;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;
using Platform.Shared.Constants;

namespace Platform.Application.MediatR.Auth.Commands;

/// <summary>
/// Quản trị viên tạo tài khoản cho người dùng. Không còn đường tự đăng ký:
/// endpoint gọi command này yêu cầu role ADMIN.
/// </summary>
public record CreateUserCommand : IRequest<IResult>
{
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string PasswordConfirm { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string RoleCode { get; init; } = AuthIdentityConstants.User;

    /// <summary>Nhà truyền nhận theo thứ tự ưu tiên; phần tử đầu là nhà chính.</summary>
    public List<string>? TvanProviderCodes { get; init; }

    public string? TaxCode { get; init; }

    internal sealed class Handler(
        PlatformDbContext db,
        IPasswordHasher<User> passwordHasher) : IRequestHandler<CreateUserCommand, IResult>
    {
        public async Task<IResult> Handle(CreateUserCommand request, CancellationToken ct)
        {
            var outcome = await Execute(request, ct);
            return outcome.ToIResult("Đã tạo tài khoản");
        }

        private async Task<ServiceOutcome<CreatedUserResponse>> Execute(
            CreateUserCommand request, CancellationToken ct)
        {
            if (request.Password != request.PasswordConfirm)
                return Fail("Mật khẩu xác nhận không khớp.");

            var userName = request.UserName.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(userName))
                return Fail("Thiếu tên đăng nhập.");

            var email = request.Email.Trim().ToLowerInvariant();
            var roleCode = string.IsNullOrWhiteSpace(request.RoleCode)
                ? AuthIdentityConstants.User
                : request.RoleCode.Trim().ToUpperInvariant();

            if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
                return Fail($"Tên đăng nhập {userName} đã tồn tại.");

            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return Fail($"Email {email} đã được đăng ký.");

            var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Code == roleCode, ct);
            if (role is null)
                return Fail($"Chưa cấu hình vai trò {roleCode}.");

            var providerCodes = (request.TvanProviderCodes ?? [])
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToArray();

            var known = await db.TvanProviders.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.Code)
                .Distinct()
                .ToListAsync(ct);

            var unknown = providerCodes.Except(known).ToArray();
            if (unknown.Length > 0)
                return Fail($"Chưa khai báo hoặc đã tắt nhà truyền nhận: {string.Join(", ", unknown)}.");

            var user = new User
            {
                UserName = userName,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? userName
                    : request.DisplayName.Trim(),
                TaxCode = request.TaxCode?.Trim(),
            };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            db.Users.Add(user);
            db.UserIdentities.Add(new UserIdentity
            {
                UserId = user.Id,
                Provider = AuthIdentityConstants.LocalProvider,
                ProviderUserId = userName,
                ProviderEmail = email,
            });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

            for (var priority = 0; priority < providerCodes.Length; priority++)
            {
                db.UserTvanProviders.Add(new UserTvanProvider
                {
                    UserId = user.Id,
                    ProviderCode = providerCodes[priority],
                    Priority = priority,
                });
            }

            await db.SaveChangesAsync(ct);

            // Không phát token cho tài khoản vừa tạo: người gọi là quản trị viên,
            // còn chủ tài khoản phải tự đăng nhập bằng mật khẩu của mình.
            return ServiceOutcome<CreatedUserResponse>.Success(new CreatedUserResponse(
                user.Id, user.UserName, user.Email, user.DisplayName, role.Code, providerCodes));
        }

        private static ServiceOutcome<CreatedUserResponse> Fail(string message) =>
            ServiceOutcome<CreatedUserResponse>.Fail(message, HttpStatusCode.BadRequest);
    }
}
