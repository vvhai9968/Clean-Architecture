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

        // Trả đúng status mà use case quyết định (401, 403, 404, 502...), thay vì
        // ép mọi thất bại thành 400 — client phân biệt được "sai dữ liệu" với "không có quyền".
        return Results.Json(
            new ApiResponse<string>
            {
                StatusCode = outcome.Status,
                Message = outcome.Error,
            },
            statusCode: (int)outcome.Status);
    }
}
