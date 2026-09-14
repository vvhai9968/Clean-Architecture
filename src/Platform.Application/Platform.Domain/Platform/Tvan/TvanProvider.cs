using Platform.Shared.Common;
using Platform.Shared.Constants;

namespace Platform.Domain.Platform.Tvan;

/// <summary>
/// Một nhà cung cấp T-VAN gắn với một môi trường. UAT và PROD là 2 record riêng biệt.
/// Toàn bộ đặc thù kỹ thuật của NCC nằm ở đây và ở <see cref="TvanEndpoint"/> — không nằm trong code.
/// </summary>
public class TvanProvider : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>UAT | PROD.</summary>
    public string Environment { get; set; } = "UAT";

    public string BaseUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Thứ tự ưu tiên khi failover (nhỏ hơn = ưu tiên hơn).</summary>
    public int Priority { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Key của ITvanAuthStrategy — xem <see cref="TvanAuthSchemes"/>.</summary>
    public string AuthSchemeKey { get; set; } = TvanAuthSchemes.None;

    /// <summary>Config riêng của auth scheme (loginPath, tokenPath, headerFormat, ttlSeconds...). jsonb.</summary>
    public string AuthConfigJson { get; set; } = "{}";

    /// <summary>Cấu hình retry: {"maxAttempts":3,"baseDelayMs":500}. jsonb. Null = dùng mặc định.</summary>
    public string? ResilienceConfigJson { get; set; }

    /// <summary>Cách bóc payload webhook NCC gọi về + cách xác thực. jsonb. Null = không nhận callback.</summary>
    public string? CallbackMapJson { get; set; }

    public ICollection<TvanEndpoint> Endpoints { get; set; } = new List<TvanEndpoint>();
    public ICollection<TvanHeaderTemplate> Headers { get; set; } = new List<TvanHeaderTemplate>();
}
