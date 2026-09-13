using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Platform.Application.Services.Auth;
using Platform.Shared;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Auth.Commands;

public record SignUpCommand(string UserName, string Email, string Password, string PasswordConfirm) : IRequest<IResult>
{
    internal class Handler(IAuthService authService) : IRequestHandler<SignUpCommand, IResult>
    {
        public async Task<IResult> Handle(SignUpCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await authService.SignUp(request.UserName, request.Password, request.PasswordConfirm,
                    request.Email);
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
