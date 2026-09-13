using System.Net;

namespace Platform.Shared.Common;

public class Response<TData>
{
    public bool IsSuccess { get; set; }
    public TData? Data { get; set; }
    public string? Message { get; set; }

    public Response() { }

    private Response(string? message) : this()
    {
        Message = message;
    }

    public Response(string? message, TData? data) : this(message)
    {
        IsSuccess = false;
        Data = data;
    }
}

public class ApiResponse<TData> : Response<TData>
{
    public HttpStatusCode StatusCode { get; set; }
}
