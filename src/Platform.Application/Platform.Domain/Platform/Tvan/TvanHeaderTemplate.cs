using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>Header động. EndpointId = null nghĩa là áp cho mọi endpoint của provider.</summary>
public class TvanHeaderTemplate : BaseEntity
{
    public Guid ProviderId { get; set; }
    public Guid? EndpointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ValueTemplate { get; set; } = string.Empty;
    public int Order { get; set; }
}
