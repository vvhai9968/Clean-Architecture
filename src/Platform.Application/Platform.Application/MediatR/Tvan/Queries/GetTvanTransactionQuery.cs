using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Platform.Application.MediatR.Tvan.Commands;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Shared.Common;
using Platform.Shared.Constants;

namespace Platform.Application.MediatR.Tvan.Queries;

/// <summary>
/// Tra trạng thái một giao dịch. Với NCC có API tra cứu (HILO) thì Refresh=true sẽ hỏi lại
/// T-VAN đã xử lý giao dịch đó; NCC không có API tra cứu thì chỉ trả trạng thái outbox.
/// </summary>
public record GetTvanTransactionQuery(string CorrelationKey, bool Refresh = false) : IRequest<IResult>
{
    internal sealed class Handler(
        ITvanTransactionStore store,
        ITvanDispatcher dispatcher) : IRequestHandler<GetTvanTransactionQuery, IResult>
    {
        public async Task<IResult> Handle(GetTvanTransactionQuery request, CancellationToken ct)
        {
            var transaction = await store.FindAsync(request.CorrelationKey, ct);

            if (transaction is null)
            {
                return Results.NotFound(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = $"Không tìm thấy giao dịch {request.CorrelationKey}.",
                });
            }

            if (!request.Refresh)
                return Ok(transaction, null);

            // Hỏi đúng nhà đã xử lý giao dịch này trước; nếu nhà đó đang chết thì
            // hỏi các nhà còn lại trong kế hoạch, vì có thể đã từng failover sang đó.
            var plan = new[] { transaction.ProviderCode }
                .Concat(transaction.ProviderPlan.Where(x => x != transaction.ProviderCode))
                .ToArray();

            var result = await dispatcher.DispatchAsync(new TvanFailoverRequest
            {
                ProviderCodes = plan,
                TaxCode = transaction.TaxCode,
                OperationCode = TvanOperations.QueryResult,
                Xml = string.Empty,
                CorrelationKey = transaction.CorrelationKey,
                Fields = new Dictionary<string, string?> { ["MTDiep"] = transaction.CorrelationKey },
                TransactionId = transaction.Id,
            }, ct);

            return Ok(transaction, result);
        }

        private static IResult Ok(TvanTransactionSnapshot snapshot, TvanFailoverResult? live) =>
            Results.Ok(new ApiResponse<object>
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccess = true,
                Message = "Success",
                Data = new
                {
                    Transaction = new TvanDispatchResponse(
                        snapshot.Id, snapshot.ProviderCode, snapshot.ProviderPlan, snapshot.CorrelationKey,
                        snapshot.State.ToString(), snapshot.ProviderReference, snapshot.ResultCode,
                        snapshot.ResultMessage, []),
                    Live = live is null ? null : new
                    {
                        live.Accepted,
                        live.ProviderCode,
                        live.Final,
                        live.Attempts,
                    },
                },
            });
    }
}
