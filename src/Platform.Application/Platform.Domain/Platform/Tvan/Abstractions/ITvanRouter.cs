namespace Platform.Domain.Platform.Tvan.Abstractions;

/// <summary>
/// Quyết định account hiện tại được đi qua những nhà truyền nhận nào, theo thứ tự nào.
/// Nguồn ưu tiên: claim trong JWT (tvan_provider / tvan_taxcode) → binding theo MST trong DB.
/// </summary>
public interface ITvanRouter
{
    Task<TvanRoutePlan> ResolveAsync(string? providerCodeOverride, string? taxCodeOverride, CancellationToken ct);
}

/// <summary>
/// Kế hoạch định tuyến: danh sách nhà truyền nhận đã sắp theo thứ tự ưu tiên.
/// Phần tử đầu là nhà chính, các phần tử sau là phương án dự phòng khi nhà trước không hoạt động.
/// </summary>
/// <param name="Source">Nguồn quyết định — ghi vào log để truy vết khi đối soát.</param>
public sealed record TvanRoutePlan(IReadOnlyList<string> ProviderCodes, string TaxCode, string Source)
{
    public string Primary => ProviderCodes[0];
}

public sealed class TvanRoutingException(string message) : Exception(message);
