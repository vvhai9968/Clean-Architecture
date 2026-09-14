using MediatR;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.MediatR.Auth.Commands;
using Platform.Application.MediatR.Auth.Queries;
using Platform.Shared.Constants;

namespace Platform.APIs.Endpoints.Auth;

public static class AuthEndPoint
{
    public static void APIs(this WebApplication app)
    {
        var group = app.MapGroup("api/auth").WithTags("Auth");

        group.MapPost("sign-in", (ISender send, [FromBody] SignInQuery query) => send.Send(query))
            .AllowAnonymous()
            .WithSummary("Đăng nhập bằng email + mật khẩu");

        // Không còn tự đăng ký: chỉ quản trị viên được tạo tài khoản.
        group.MapPost("users", (ISender send, [FromBody] CreateUserCommand command) => send.Send(command))
            .RequireRoles(AuthIdentityConstants.Admin)
            .WithSummary("Quản trị viên tạo tài khoản mới");
    }
}
