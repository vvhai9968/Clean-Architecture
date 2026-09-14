using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Platform.Domain.Platform.Tvan;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;

namespace Platform.Infrastructure.Tvan.Metadata;

public interface ITvanMetadataStore
{
    Task<TvanProvider?> GetProviderAsync(string code, CancellationToken ct);
    Task<TvanCredential?> GetCredentialAsync(Guid providerId, string taxCode, CancellationToken ct);
    /// <summary>Danh sách nhà truyền nhận của một MST, đã sắp theo thứ tự ưu tiên.</summary>
    Task<IReadOnlyList<string>> GetProviderCodesForTaxCodeAsync(string taxCode, CancellationToken ct);
}

/// <summary>
/// Đọc metadata có cache. Bắt buộc phải cache: không thể query DB cho từng hoá đơn
/// trong đợt phát hành cuối tháng. Admin CRUD gọi EvictProvider để config mới có hiệu lực ngay.
/// </summary>
internal sealed class TvanMetadataStore(
    PlatformDbContext db,
    IMemoryCache cache,
    AppSettings appSettings) : ITvanMetadataStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private string Environment => appSettings.Tvan.Environment;

    public async Task<TvanProvider?> GetProviderAsync(string code, CancellationToken ct)
    {
        var key = TvanCacheKeys.Provider(code, Environment);
        if (cache.TryGetValue(key, out TvanProvider? cached)) return cached;

        var provider = await db.TvanProviders
            .AsNoTracking()
            .Include(x => x.Endpoints)
            .Include(x => x.Headers)
            .FirstOrDefaultAsync(x => x.Code == code && x.Environment == Environment && x.IsActive, ct);

        cache.Set(key, provider, Ttl);
        return provider;
    }

    public async Task<TvanCredential?> GetCredentialAsync(Guid providerId, string taxCode, CancellationToken ct)
    {
        var key = TvanCacheKeys.Credential(providerId, taxCode);
        if (cache.TryGetValue(key, out TvanCredential? cached)) return cached;

        var now = DateTimeOffset.UtcNow;

        var credential = await db.TvanCredentials
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.IsActive)
            .Where(x => x.TaxCode == taxCode || x.TaxCode == TvanCredential.SharedTaxCode)
            .Where(x => x.ValidFrom == null || x.ValidFrom <= now)
            .Where(x => x.ValidTo == null || x.ValidTo >= now)
            // Credential riêng của MST luôn thắng credential dùng chung.
            .OrderBy(x => x.TaxCode == TvanCredential.SharedTaxCode ? 1 : 0)
            .FirstOrDefaultAsync(ct);

        cache.Set(key, credential, Ttl);
        return credential;
    }

    public async Task<IReadOnlyList<string>> GetProviderCodesForTaxCodeAsync(string taxCode, CancellationToken ct)
    {
        var key = TvanCacheKeys.Binding(taxCode);
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null) return cached;

        // Viết dạng query expression để thứ tự ưu tiên chắc chắn xuống được SQL ORDER BY —
        // .OrderBy().Join() không bảo đảm giữ thứ tự sau khi EF dịch sang SQL.
        var providerCodes = await (
            from binding in db.TvanTenantBindings.AsNoTracking()
            join provider in db.TvanProviders.AsNoTracking() on binding.ProviderId equals provider.Id
            where binding.TaxCode == taxCode && binding.IsActive && provider.IsActive
            orderby binding.Priority
            select provider.Code).ToListAsync(ct);

        cache.Set(key, (IReadOnlyList<string>)providerCodes, Ttl);
        return providerCodes;
    }
}

/// <summary>Singleton để admin CRUD (scoped) có thể xoá cache dùng chung toàn ứng dụng.</summary>
internal sealed class TvanMetadataCache(IMemoryCache cache, AppSettings appSettings) : ITvanMetadataCache
{
    public void EvictProvider(string providerCode)
    {
        cache.Remove(TvanCacheKeys.Provider(providerCode, appSettings.Tvan.Environment));
        TvanCacheKeys.BumpGeneration();
    }

    public void EvictAll() => TvanCacheKeys.BumpGeneration();
}

/// <summary>
/// Khoá cache có "generation": tăng generation là vô hiệu hoá toàn bộ credential/binding đã cache
/// mà không cần duyệt từng key — IMemoryCache không hỗ trợ xoá theo prefix.
/// </summary>
internal static class TvanCacheKeys
{
    private static int _generation;

    public static void BumpGeneration() => Interlocked.Increment(ref _generation);

    public static string Provider(string code, string environment) =>
        $"tvan:{_generation}:provider:{environment}:{code}";

    public static string Credential(Guid providerId, string taxCode) =>
        $"tvan:{_generation}:cred:{providerId}:{taxCode}";

    public static string Binding(string taxCode) =>
        $"tvan:{_generation}:binding:{taxCode}";
}
