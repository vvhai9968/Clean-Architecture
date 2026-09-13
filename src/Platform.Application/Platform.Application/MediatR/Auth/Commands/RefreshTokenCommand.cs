using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Platform.Application.Services.Auth;
using Platform.Shared;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<IResult>
{
    internal class Handler(IAuthService authService) : IRequestHandler<RefreshTokenCommand, IResult>
    {
        public async Task<IResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await authService.RefreshToken(request.RefreshToken);
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
