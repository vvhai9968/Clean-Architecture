using Platform.Shared.Common;

namespace Platform.Domain.Platform.Auth;

public class Role : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
