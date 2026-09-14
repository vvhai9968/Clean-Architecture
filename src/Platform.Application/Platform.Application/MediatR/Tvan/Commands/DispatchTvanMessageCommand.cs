using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Tvan.Commands;

/// <summary>
/// Gửi một thông điệp lên T-VAN. Handler không biết BKAV/HILO/Minvoice là gì và cũng không
/// biết việc chuyển nhà truyền nhận diễn ra thế nào — đó là việc của <see cref="ITvanDispatcher"/>.
/// </summary>
public record DispatchTvanMessageCommand : IRequest<IResult>
{
    /// <summary>Bỏ trống để lấy từ claim tvan_taxcode của token.</summary>
    public string? TaxCode { get; init; }

    public required string OperationCode { get; init; }

    /// <summary>XML chuẩn QĐ 1450 đã ký số.</summary>
    public required string Xml { get; init; }

    /// <summary>MTDiep do phần mềm Invoice sinh. Bỏ trống thì gateway tự sinh.</summary>
    public string? CorrelationKey { get; init; }

    /// <summary>
    /// Ép đúng một nhà truyền nhận, chỉ dành cho quản trị viên. Khi có giá trị thì
    /// tắt luôn cơ chế chuyển nhà — người vận hành đang muốn kiểm tra chính nhà đó.
    /// </summary>
    public string? ProviderCode { get; init; }

    public Dictionary<string, string?>? Fields { get; init; }

    /// <summary>true = chỉ ghi outbox rồi trả 202, để background dispatcher gửi.</summary>
    public bool Deferred { get; init; }

    internal sealed class Handler(
        ITvanRouter router,
        ITvanTransactionStore store,
        ITvanDispatcher dispatcher,
        ICorrelationKeyGenerator keyGenerator,
        ILogger<Handler> logger) : IRequestHandler<DispatchTvanMessageCommand, IResult>
    {
        public async Task<IResult> Handle(DispatchTvanMessageCommand request, CancellationToken ct)
        {
            try
            {
                return await Execute(request, ct);
            }
            catch (TvanRoutingException ex)
            {
                return Results.BadRequest(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = ex.Message,
                });
            }
        }

        private async Task<IResult> Execute(DispatchTvanMessageCommand request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Xml))
            {
                return Results.BadRequest(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Thiếu dữ liệu XML.",
                });
            }

            var plan = await router.ResolveAsync(request.ProviderCode, request.TaxCode, ct);
            var correlationKey = string.IsNullOrWhiteSpace(request.CorrelationKey)
                ? keyGenerator.Create(plan.TaxCode)
                : request.CorrelationKey!;

            // Idempotency: gửi lại cùng CorrelationKey không bắn trùng lên CQT.
            var existing = await store.FindAsync(correlationKey, ct);
            if (existing is not null && existing.State is TvanTransactionState.Accepted or TvanTransactionState.Rejected)
            {
                logger.LogInformation("Bỏ qua gửi trùng {Key} ({State})", correlationKey, existing.State);
                return Ok(ToResponse(existing), "Giao dịch đã được xử lý trước đó");
            }

            var dispatch = new TvanFailoverRequest
            {
                ProviderCodes = plan.ProviderCodes,
                TaxCode = plan.TaxCode,
                OperationCode = request.OperationCode,
                Xml = request.Xml,
                CorrelationKey = correlationKey,
                Fields = request.Fields ?? new Dictionary<string, string?>(),
            };

            // Bước ghi outbox là điểm bền vững: từ đây trở đi giao dịch không thể mất.
            var transaction = existing ?? await store.EnqueueAsync(dispatch, plan.Source, ct);

            logger.LogInformation(
                "Nhận giao dịch T-VAN {Key}, kế hoạch định tuyến: {Plan} (nguồn: {Source})",
                correlationKey, string.Join(" → ", plan.ProviderCodes), plan.Source);

            if (request.Deferred)
            {
                return Results.Accepted($"/api/tvan/messages/{correlationKey}", new ApiResponse<TvanDispatchResponse>
                {
                    StatusCode = HttpStatusCode.Accepted,
                    IsSuccess = true,
                    Message = "Đã ghi nhận, sẽ gửi T-VAN ở tiến trình nền",
                    Data = ToResponse(transaction),
                });
            }

            var result = await dispatcher.DispatchAsync(dispatch with { TransactionId = transaction.Id }, ct);

            // Không nhà nào hoạt động: chưa có câu trả lời nào, để outbox gửi lại sau.
            if (result.AllProvidersUnavailable)
            {
                await store.RescheduleAsync(transaction.Id, result.Describe(), maxAttempts: 5, ct);

                return Results.Accepted($"/api/tvan/messages/{correlationKey}", new ApiResponse<TvanDispatchResponse>
                {
                    StatusCode = HttpStatusCode.Accepted,
                    IsSuccess = true,
                    Message = "Không còn nhà truyền nhận nào khả dụng. Giao dịch đã được ghi nhận và sẽ tự động gửi lại.",
                    Data = ToResponse(transaction) with
                    {
                        State = TvanTransactionState.Pending.ToString(),
                        Message = result.Describe(),
                        Attempts = result.Attempts,
                    },
                });
            }

            await store.CompleteAsync(transaction.Id, result, ct);

            var response = ToResponse(transaction) with
            {
                ProviderCode = result.ProviderCode!,
                State = (result.Accepted ? TvanTransactionState.Accepted : TvanTransactionState.Rejected).ToString(),
                ProviderReference = result.Final!.ProviderReference,
                ResultCode = result.Final.ResultCode,
                Message = result.Final.Message,
                Attempts = result.Attempts,
            };

            return result.Accepted
                ? Ok(response, "Gửi T-VAN thành công")
                : Results.BadRequest(new ApiResponse<TvanDispatchResponse>
                {
                    StatusCode = HttpStatusCode.BadGateway,
                    Message = result.Describe(),
                    Data = response,
                });
        }

        private static TvanDispatchResponse ToResponse(TvanTransactionSnapshot s) => new(
            s.Id, s.ProviderCode, s.ProviderPlan, s.CorrelationKey, s.State.ToString(),
            s.ProviderReference, s.ResultCode, s.ResultMessage, []);

        private static IResult Ok(TvanDispatchResponse data, string message) =>
            Results.Ok(new ApiResponse<TvanDispatchResponse>
            {
                StatusCode = HttpStatusCode.OK, IsSuccess = true, Message = message, Data = data,
            });
    }
}

/// <param name="Attempts">Từng nhà truyền nhận đã được thử và vì sao bị bỏ qua — dùng để đối soát.</param>
public sealed record TvanDispatchResponse(
    Guid TransactionId,
    string ProviderCode,
    IReadOnlyList<string> ProviderPlan,
    string CorrelationKey,
    string State,
    string? ProviderReference,
    string? ResultCode,
    string? Message,
    IReadOnlyList<TvanProviderAttempt> Attempts);
