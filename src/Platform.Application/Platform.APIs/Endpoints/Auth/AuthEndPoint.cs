using MediatR;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.MediatR.Auth.Commands;
using Platform.Application.MediatR.Auth.Queries;

namespace Platform.APIs.Endpoints.Auth;

public static class AuthEndPoint
{
    public static void APIs(this WebApplication app)
    {
        var group = app.MapGroup("api/auth").WithTags("Auth");

        group.MapPost("sso", (ISender send, [FromBody] SsoLoginCommand query) => send.Send(query));
        group.MapPost("sign-in", (ISender send, [FromBody] SignInQuery query) => send.Send(query));
        group.MapPost("sign-up", (ISender send, [FromBody] SignUpCommand command) => send.Send(command));
        group.MapPost("refresh-token", (ISender send, [FromBody] RefreshTokenCommand command) => send.Send(command));
    }
}
