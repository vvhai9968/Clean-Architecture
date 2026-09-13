namespace Platform.Shared;

public class AppSettings
{
    public ConnectionStrings ConnectionStrings { get; set; } = null!;
    public JwtSettings Jwt { get; set; } = new();
    public AzureAd AzureAd { get; set; } = new();
    public string[] CorsOrigins { get; set; } = [];
}

public class JwtSettings
{
    public string MasterKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}

public class AzureAd
{
    public string? Instance { get; set; }
    public string? OAuth2Endpoint { get; set; }
    public string? Domain { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
    public string? RedirectUri { get; set; }
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = null!;
}
