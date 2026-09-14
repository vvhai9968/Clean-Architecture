namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Mã hoá/giải mã secret của T-VAN (PartnerToken, password, ma_dvcs...).
/// Implementation mặc định dùng ASP.NET DataProtection; production nên đổi sang Key Vault / KMS.
/// </summary>
public interface ISecretProtector
{
    string Protect(IReadOnlyDictionary<string, string?> secrets);
    IReadOnlyDictionary<string, string?> Unprotect(string protectedPayload);
}

/// <summary>Cho phép admin CRUD đẩy config mới có hiệu lực ngay, không cần restart service.</summary>
public interface ITvanMetadataCache
{
    void EvictProvider(string providerCode);
    void EvictAll();
}
