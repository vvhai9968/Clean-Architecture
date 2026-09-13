using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Platform.Application.Services.Auth;
using Platform.Shared;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Auth.Commands;

public record SsoLoginCommand(string Code) : IRequest<IResult>
{
    internal class Handler(IAuthService authService) : IRequestHandler<SsoLoginCommand, IResult>
    {
        public async Task<IResult> Handle(SsoLoginCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await authService.SsoSignIn(request.Code, cancellationToken);
                return result.ToIResult();
            }
            catch (Exception e)
            {
                return Results.BadRequest(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = e.Message
                });
            }
        }
    }
}
