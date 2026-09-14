using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Platform.Domain.Platform.Tvan.Abstractions;

namespace Platform.Infrastructure.Tvan.Security;

/// <summary>
/// Mã hoá secret bằng ASP.NET DataProtection. Đủ tốt cho môi trường một cụm có key ring chung.
/// Production nhiều node hoặc yêu cầu tuân thủ cao hơn thì thay bằng Key Vault / KMS —
/// chỉ cần một implementation khác của <see cref="ISecretProtector"/>, không đụng chỗ nào khác.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private const string Purpose = "Platform.Tvan.Credentials.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Protect(IReadOnlyDictionary<string, string?> secrets) =>
        _protector.Protect(JsonSerializer.Serialize(secrets));

    public IReadOnlyDictionary<string, string?> Unprotect(string protectedPayload)
    {
        if (string.IsNullOrWhiteSpace(protectedPayload))
            return new Dictionary<string, string?>();

        try
        {
            var json = _protector.Unprotect(protectedPayload);
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json)
                   ?? new Dictionary<string, string?>();
        }
        catch (Exception ex)
        {
            // Không đưa nội dung ciphertext vào message để tránh rò rỉ qua log.
            throw new TvanConfigurationException(
                $"Không giải mã được credential T-VAN (key ring đã đổi?): {ex.GetType().Name}");
        }
    }
}
