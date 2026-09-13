using System.Net;
using Microsoft.AspNetCore.Http;
using Platform.Shared.Common;

namespace Platform.Shared;

public sealed class ServiceOutcome<T>
{
    public bool Ok { get; private init; }
    public T? Data { get; private init; }
    public string? Error { get; private init; }
    public HttpStatusCode Status { get; private init; }

    public static ServiceOutcome<T> Success(T data) =>
        new() { Ok = true, Data = data, Status = HttpStatusCode.OK };

    public static ServiceOutcome<T> Fail(string message, HttpStatusCode status) =>
        new() { Ok = false, Error = message, Status = status };
}

public static class ServiceOutcomeHttp
{
    public static IResult ToIResult<T>(this ServiceOutcome<T> outcome, string successMessage = "Success")
    {
        if (outcome.Ok)
        {
            return Results.Ok(new ApiResponse<T>
            {
                Data = outcome.Data,
                StatusCode = outcome.Status,
                IsSuccess = true,
                Message = successMessage,
            });
        }

        if (outcome.Status == HttpStatusCode.NotFound)
        {
            return Results.NotFound(new ApiResponse<string>
            {
                StatusCode = HttpStatusCode.NotFound,
                Message = outcome.Error,
            });
        }

        return Results.BadRequest(new ApiResponse<string>
        {
            StatusCode = HttpStatusCode.BadRequest,
            Message = outcome.Error,
        });
    }
}
