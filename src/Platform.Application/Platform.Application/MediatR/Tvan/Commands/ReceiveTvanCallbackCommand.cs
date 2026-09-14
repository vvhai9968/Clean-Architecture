using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Tvan.Commands;

/// <summary>
/// Nhận webhook NCC gọi về (HILO CallBackUrl). Endpoint này anonymous,
/// việc xác thực nằm trong ITvanCallbackProcessor theo cấu hình của từng provider.
/// </summary>
public record ReceiveTvanCallbackCommand(
    string ProviderCode,
    string RawBody,
    Dictionary<string, string> Headers) : IRequest<IResult>
{
    internal sealed class Handler(
        ITvanCallbackProcessor processor,
        ILogger<Handler> logger) : IRequestHandler<ReceiveTvanCallbackCommand, IResult>
    {
        public async Task<IResult> Handle(ReceiveTvanCallbackCommand request, CancellationToken ct)
        {
            try
            {
                var result = await processor.ProcessAsync(
                    new TvanCallbackRequest(request.ProviderCode, request.RawBody, request.Headers), ct);

                if (!result.Handled)
                    logger.LogWarning("Callback {Provider} không khớp giao dịch nào: {Key}",
                        request.ProviderCode, result.CorrelationKey);

                // Luôn trả 200 để NCC không retry vô hạn; lỗi nội bộ đã được log lại.
                return Results.Ok(new ApiResponse<object>
                {
                    StatusCode = HttpStatusCode.OK,
                    IsSuccess = true,
                    Message = result.Handled ? "Đã tiếp nhận" : "Bỏ qua",
                    Data = new { result.CorrelationKey, result.ResultCode },
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Callback {Provider} không hợp lệ", request.ProviderCode);
                return Results.Unauthorized();
            }
        }
    }
}
