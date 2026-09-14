using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>
/// Credential theo cặp (Provider, MST). TaxCode = "*" là credential dùng chung cấp partner.
/// Secrets luôn được mã hoá at-rest qua ISecretProtector — không bao giờ lưu plaintext.
/// </summary>
public class TvanCredential : BaseEntity
{
    public const string SharedTaxCode = "*";

    public Guid ProviderId { get; set; }
    public string TaxCode { get; set; } = SharedTaxCode;

    /// <summary>Ciphertext của một JSON object {"key":"value"}.</summary>
    public string ProtectedSecretsJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
}
