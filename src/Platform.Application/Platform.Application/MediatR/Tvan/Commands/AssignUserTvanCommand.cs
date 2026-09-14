using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Tvan.Commands;

/// <summary>
/// Gán danh sách nhà truyền nhận và MST cho một account. Thứ tự trong
/// <paramref name="ProviderCodes"/> chính là thứ tự chuyển nhà khi nhà trước không hoạt động —
/// phần tử đầu là nhà chính. Danh sách được phát vào access token thành nhiều
/// claim <c>tvan_provider</c>, giữ nguyên thứ tự.
/// </summary>
public record AssignUserTvanCommand(
    string UserName,
    List<string> ProviderCodes,
    string? TaxCode) : IRequest<IResult>
{
    internal sealed class Handler(PlatformDbContext db) : IRequestHandler<AssignUserTvanCommand, IResult>
    {
        public async Task<IResult> Handle(AssignUserTvanCommand request, CancellationToken ct)
        {
            var userName = request.UserName.Trim().ToLowerInvariant();

            var user = await db.Users
                .Include(x => x.TvanProviders)
                .FirstOrDefaultAsync(x => x.UserName == userName, ct);

            if (user is null)
            {
                return Results.NotFound(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = $"Không tìm thấy tài khoản {userName}.",
                });
            }

            var codes = request.ProviderCodes
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToArray();

            var known = await db.TvanProviders.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => x.Code)
                .Distinct()
                .ToListAsync(ct);

            var unknown = codes.Except(known).ToArray();
            if (unknown.Length > 0)
            {
                return Results.BadRequest(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Chưa khai báo hoặc đã tắt nhà truyền nhận: {string.Join(", ", unknown)}.",
                });
            }

            db.UserTvanProviders.RemoveRange(user.TvanProviders);

            for (var priority = 0; priority < codes.Length; priority++)
            {
                db.UserTvanProviders.Add(new UserTvanProvider
                {
                    UserId = user.Id,
                    ProviderCode = codes[priority],
                    Priority = priority,
                });
            }

            user.TaxCode = request.TaxCode?.Trim();
            await db.SaveChangesAsync(ct);

            return Results.Ok(new ApiResponse<object>
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccess = true,
                Message = "Đã gán nhà truyền nhận. Token hiện tại vẫn giữ danh sách cũ tới khi đăng nhập lại.",
                Data = new { user.UserName, ProviderCodes = codes, user.TaxCode },
            });
        }
    }
}
