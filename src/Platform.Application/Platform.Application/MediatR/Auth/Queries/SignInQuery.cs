using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Platform.Application.Services.Auth;
using Platform.Shared;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Auth.Queries;

public record SignInQuery(string UserName, string Password) : IRequest<IResult>
{
    internal class Handler(IAuthService authService) : IRequestHandler<SignInQuery, IResult>
    {
        public async Task<IResult> Handle(SignInQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await authService.SignIn(request.UserName, request.Password);
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
