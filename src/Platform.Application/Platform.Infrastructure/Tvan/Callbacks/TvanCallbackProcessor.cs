using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Infrastructure.Tvan.Response;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan.Callbacks;

/// <summary>
/// Xử lý webhook NCC gọi về. Cách bóc payload và cách xác thực đều nằm trong
/// TvanProvider.CallbackMapJson, nên thêm NCC có webhook mới không cần code mới.
/// </summary>
internal sealed class TvanCallbackProcessor(
    PlatformDbContext db,
    ITvanMetadataStore metadata,
    ISecretProtector protector,
    IResponseValueExtractorRegistry extractors,
    ILogger<TvanCallbackProcessor> logger) : ITvanCallbackProcessor
{
    public async Task<TvanCallbackResult> ProcessAsync(TvanCallbackRequest request, CancellationToken ct)
    {
        var provider = await metadata.GetProviderAsync(request.ProviderCode, ct)
                       ?? throw new TvanConfigurationException($"Không có T-VAN '{request.ProviderCode}'.");

        if (string.IsNullOrWhiteSpace(provider.CallbackMapJson))
            throw new TvanConfigurationException($"T-VAN '{provider.Code}' chưa khai báo CallbackMapJson.");

        var map = CallbackMap.Parse(provider.CallbackMapJson);
        await VerifyAsync(provider, map, request, ct);

        var extractor = extractors.Resolve(map.Format);
        var correlationKey = extractor.Extract(request.RawBody, map.CorrelationPath);

        if (string.IsNullOrWhiteSpace(correlationKey))
        {
            logger.LogWarning("Callback {Provider} không có mã thông điệp tại '{Path}'.",
                provider.Code, map.CorrelationPath);
            return new TvanCallbackResult(false, null, null, "Thiếu mã thông điệp tham chiếu");
        }

        // Tìm theo CorrelationKey đơn lẻ: giao dịch có thể đã failover sang nhà khác nhà
        // đang gọi callback về, nhưng vẫn là cùng một thông điệp nghiệp vụ.
        var transaction = await db.TvanTransactions.FirstOrDefaultAsync(
            x => x.CorrelationKey == correlationKey, ct);

        if (transaction is not null && transaction.ProviderCode != provider.Code)
        {
            logger.LogWarning(
                "Callback đến từ {Caller} nhưng giao dịch {Key} được ghi nhận ở {Owner} — vẫn cập nhật.",
                provider.Code, correlationKey, transaction.ProviderCode);
        }

        var code = map.CodePath is null ? null : extractor.Extract(request.RawBody, map.CodePath);
        var message = map.MessagePath is null ? null : extractor.Extract(request.RawBody, map.MessagePath);

        if (transaction is null)
            return new TvanCallbackResult(false, correlationKey, code, "Không khớp giao dịch nào");

        transaction.ResultCode = code;
        transaction.ResultMessage = message;
        transaction.CompletedAt = DateTimeOffset.UtcNow;

        // Callback mang kết quả thật từ CQT — nó có quyền lật trạng thái đã ghi lúc gửi.
        if (map.SuccessValues.Length > 0)
        {
            transaction.State = map.SuccessValues.Contains(code, StringComparer.OrdinalIgnoreCase)
                ? TvanTransactionState.Accepted
                : TvanTransactionState.Rejected;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Callback {Provider} cập nhật {Key} -> {State} ({Code})",
            provider.Code, correlationKey, transaction.State, code);

        return new TvanCallbackResult(true, correlationKey, code, message);
    }

    private async Task VerifyAsync(
        TvanProvider provider, CallbackMap map, TvanCallbackRequest request, CancellationToken ct)
    {
        if (map.SecretHeader is null) return;

        var credential = await metadata.GetCredentialAsync(provider.Id, TvanCredential.SharedTaxCode, ct)
                         ?? throw new TvanConfigurationException(
                             $"T-VAN '{provider.Code}' bật xác thực callback nhưng chưa có credential dùng chung.");

        var expected = protector.Unprotect(credential.ProtectedSecretsJson).GetValueOrDefault(map.SecretRef);
        var actual = request.Headers.GetValueOrDefault(map.SecretHeader);

        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual) || !FixedTimeEquals(expected, actual))
            throw new UnauthorizedAccessException($"Callback {provider.Code} có chữ ký không hợp lệ.");
    }

    /// <summary>So sánh thời gian cố định để không rò rỉ secret qua timing attack.</summary>
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}

internal sealed record CallbackMap(
    string Format,
    string CorrelationPath,
    string? CodePath,
    string? MessagePath,
    string[] SuccessValues,
    string? SecretHeader,
    string SecretRef)
{
    public static CallbackMap Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new CallbackMap(
            Format: Read(root, "format") ?? TvanResponseFormats.Json,
            CorrelationPath: Read(root, "correlationPath")
                             ?? throw new TvanConfigurationException("CallbackMapJson thiếu 'correlationPath'."),
            CodePath: Read(root, "codePath"),
            MessagePath: Read(root, "messagePath"),
            SuccessValues: root.TryGetProperty("successValues", out var values) &&
                           values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(x => x.ToString()).ToArray()
                : [],
            SecretHeader: Read(root, "secretHeader"),
            SecretRef: Read(root, "secretRef") ?? "CallbackSecret");
    }

    private static string? Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
