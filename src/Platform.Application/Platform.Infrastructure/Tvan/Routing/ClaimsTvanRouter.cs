using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan.Routing;

/// <summary>
/// Dựng kế hoạch định tuyến theo thứ tự ưu tiên:
/// <list type="number">
/// <item>Tham số override trên request — chỉ dành cho admin (test / chuyển nhà thủ công).</item>
/// <item>Các claim <c>tvan_provider</c> trong access token, giữ nguyên thứ tự lúc phát token.</item>
/// <item>Bảng TvanTenantBindings theo MST — dùng cho tiến trình nền không có user context.</item>
/// </list>
/// </summary>
internal sealed class ClaimsTvanRouter(
    IHttpContextAccessor httpContextAccessor,
    ITvanMetadataStore metadata,
    ILogger<ClaimsTvanRouter> logger) : ITvanRouter
{
    public async Task<TvanRoutePlan> ResolveAsync(
        string? providerCodeOverride, string? taxCodeOverride, CancellationToken ct)
    {
        var user = httpContextAccessor.HttpContext?.User;

        var taxCode = FirstNonEmpty(taxCodeOverride, user?.FindFirst(TvanClaimTypes.TaxCode)?.Value)
                      ?? throw new TvanRoutingException(
                          "Không xác định được MST. Token thiếu claim tvan_taxcode và request không truyền taxCode.");

        if (!string.IsNullOrWhiteSpace(providerCodeOverride))
        {
            // Override là đường tắt nguy hiểm: chỉ cho phép admin, và luôn ghi log để truy vết.
            if (user?.IsInRole(AuthIdentityConstants.Admin) != true)
                throw new TvanRoutingException("Chỉ tài khoản quản trị mới được chỉ định nhà truyền nhận thủ công.");

            logger.LogWarning("Ép định tuyến T-VAN sang {Provider} cho MST {TaxCode} bởi {User}",
                providerCodeOverride, taxCode, user.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // Ép một nhà cụ thể thì cũng tắt luôn failover: người vận hành đang muốn
            // đúng nhà đó, im lặng chuyển sang nhà khác sẽ che mất thứ họ đang kiểm tra.
            return new TvanRoutePlan([providerCodeOverride.Trim().ToUpperInvariant()], taxCode, "override");
        }

        var fromClaims = user?.FindAll(TvanClaimTypes.ProviderCode)
            .Select(c => c.Value.Trim().ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToArray() ?? [];

        if (fromClaims.Length > 0)
            return new TvanRoutePlan(fromClaims, taxCode, "jwt-claim");

        var bound = await metadata.GetProviderCodesForTaxCodeAsync(taxCode, ct);
        if (bound.Count > 0)
            return new TvanRoutePlan(bound, taxCode, "tenant-binding");

        throw new TvanRoutingException(
            $"Account chưa được cấp nhà truyền nhận nào. Token thiếu claim {TvanClaimTypes.ProviderCode} " +
            $"và MST {taxCode} chưa có trong TvanTenantBindings.");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}

/// <summary>
/// Sinh MTDiep theo QĐ 1450: "V" + MST + GUID viết hoa bỏ gạch ngang.
/// Cùng quy tắc mà cả BKAV, HILO và Minvoice đều yêu cầu.
/// </summary>
internal sealed class MtDiepGenerator : ICorrelationKeyGenerator
{
    public string Create(string taxCode)
    {
        var normalized = taxCode.Replace("-", string.Empty).Trim();
        return $"V{normalized}{Guid.NewGuid():N}".ToUpperInvariant();
    }
}
