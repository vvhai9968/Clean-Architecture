namespace Platform.Domain.Platform.Auth;

/// <summary>
/// Kế hoạch định tuyến T-VAN của một account, ở dạng sẵn sàng nhúng vào access token.
/// <paramref name="ProviderCodes"/> đã sắp theo thứ tự ưu tiên, và thứ tự đó được giữ nguyên
/// trong token — nó chính là thứ tự chuyển nhà khi nhà trước không hoạt động.
/// </summary>
public sealed record TvanTokenBinding(IReadOnlyList<string> ProviderCodes, string? TaxCode)
{
    public static readonly TvanTokenBinding None = new([], null);
}
