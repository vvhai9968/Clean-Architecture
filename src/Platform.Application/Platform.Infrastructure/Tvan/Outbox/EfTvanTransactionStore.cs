using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Tvan;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Persistence.PlatformContext;

namespace Platform.Infrastructure.Tvan.Outbox;

/// <summary>
/// Outbox trên EF Core. Việc ghi bản ghi Pending là điểm bền vững của toàn bộ luồng:
/// sau khi commit, giao dịch không thể biến mất kể cả khi T-VAN hoặc chính service này chết.
/// </summary>
internal sealed class EfTvanTransactionStore(PlatformDbContext db) : ITvanTransactionStore
{
    private const char PlanSeparator = ',';

    public async Task<TvanTransactionSnapshot?> FindAsync(string correlationKey, CancellationToken ct)
    {
        var entity = await db.TvanTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CorrelationKey == correlationKey, ct);

        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<TvanTransactionSnapshot> EnqueueAsync(
        TvanFailoverRequest request, string source, CancellationToken ct)
    {
        var entity = new TvanTransaction
        {
            ProviderCode = request.ProviderCodes[0],
            ProviderPlan = string.Join(PlanSeparator, request.ProviderCodes),
            TaxCode = request.TaxCode,
            OperationCode = request.OperationCode,
            CorrelationKey = request.CorrelationKey,
            RouteSource = source,
            State = TvanTransactionState.Pending,
            RequestXml = request.Xml,
            FieldsJson = JsonSerializer.Serialize(request.Fields),
            NextAttemptAt = DateTimeOffset.UtcNow,
        };

        db.TvanTransactions.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToSnapshot(entity);
    }

    public async Task<IReadOnlyList<TvanTransactionSnapshot>> LeaseAsync(
        string workerId, int batchSize, TimeSpan leaseDuration, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var staleBefore = now - leaseDuration;

        // Phải nằm trong transaction: FOR UPDATE chỉ giữ khoá tới khi commit.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // SKIP LOCKED: nhiều instance chạy song song vẫn không giành nhau cùng một bản ghi,
        // và bản ghi bị worker chết bỏ quên (LockedAt quá hạn lease) sẽ được nhặt lại.
        var candidates = await db.TvanTransactions
            .FromSqlRaw(
                """
                SELECT * FROM "TvanTransactions"
                WHERE ("State" = 0 OR ("State" = 1 AND "LockedAt" < {1}))
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {0})
                ORDER BY "CreatedAt"
                LIMIT {2}
                FOR UPDATE SKIP LOCKED
                """,
                now, staleBefore, batchSize)
            .ToListAsync(ct);

        foreach (var entity in candidates)
        {
            entity.State = TvanTransactionState.Dispatching;
            entity.LockedBy = workerId;
            entity.LockedAt = now;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return candidates.Select(ToSnapshot).ToArray();
    }

    public async Task CompleteAsync(Guid transactionId, TvanFailoverResult result, CancellationToken ct)
    {
        var entity = await db.TvanTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, ct);
        if (entity is null || result.Final is null) return;

        // Ghi lại nhà truyền nhận thật sự đã xử lý — có thể khác nhà chính nếu đã chuyển đổi.
        entity.ProviderCode = result.ProviderCode ?? entity.ProviderCode;
        entity.State = result.Accepted ? TvanTransactionState.Accepted : TvanTransactionState.Rejected;
        entity.ProviderReference = result.Final.ProviderReference;
        entity.ResultCode = result.Final.ResultCode;
        entity.ResultMessage = Truncate(result.Describe(), 2000);
        entity.AttemptCount += 1;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.LockedBy = null;
        entity.LockedAt = null;
        entity.NextAttemptAt = null;

        await db.SaveChangesAsync(ct);
    }

    public async Task RescheduleAsync(Guid transactionId, string error, int maxAttempts, CancellationToken ct)
    {
        var entity = await db.TvanTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, ct);
        if (entity is null) return;

        entity.AttemptCount += 1;
        entity.ResultMessage = Truncate(error, 2000);
        entity.LockedBy = null;
        entity.LockedAt = null;

        if (entity.AttemptCount >= maxAttempts)
        {
            entity.State = TvanTransactionState.Failed;
            entity.NextAttemptAt = null;
            entity.CompletedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            entity.State = TvanTransactionState.Pending;
            // Backoff mũ, chặn trần 30 phút để không kéo dài vô hạn.
            var delaySeconds = Math.Min(1800, 10 * Math.Pow(3, entity.AttemptCount - 1));
            entity.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<TvanTransactionSnapshot?> GetAsync(Guid transactionId, CancellationToken ct)
    {
        var entity = await db.TvanTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == transactionId, ct);

        return entity is null ? null : ToSnapshot(entity);
    }

    private static TvanTransactionSnapshot ToSnapshot(TvanTransaction entity) => new(
        entity.Id,
        entity.ProviderCode,
        entity.ProviderPlan.Split(PlanSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        entity.TaxCode,
        entity.OperationCode,
        entity.CorrelationKey,
        entity.State,
        entity.RequestXml,
        JsonSerializer.Deserialize<Dictionary<string, string?>>(entity.FieldsJson)
        ?? new Dictionary<string, string?>(),
        entity.ProviderReference,
        entity.ResultCode,
        entity.ResultMessage,
        entity.AttemptCount);

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
