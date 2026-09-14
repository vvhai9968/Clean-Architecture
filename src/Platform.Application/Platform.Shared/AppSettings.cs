namespace Platform.Shared;

public class AppSettings
{
    public ConnectionStrings ConnectionStrings { get; set; } = null!;
    public JwtSettings Jwt { get; set; } = new();
    public string[] CorsOrigins { get; set; } = [];
    public TvanSettings Tvan { get; set; } = new();
    public BootstrapSettings Bootstrap { get; set; } = new();
}

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}

/// <summary>
/// Tài khoản quản trị đầu tiên. Vì chỉ quản trị viên mới được tạo tài khoản,
/// hệ thống phải tự sinh ra một tài khoản duy nhất lúc khởi động trên DB trống.
/// </summary>
public class BootstrapSettings
{
    public string AdminUserName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = "System Administrator";
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = null!;
}

public class TvanSettings
{
    /// <summary>Môi trường T-VAN đang dùng: UAT | PROD. Quyết định record TvanProvider nào được chọn.</summary>
    public string Environment { get; set; } = "UAT";

    /// <summary>Bật tiến trình nền gửi lại các giao dịch còn treo trong outbox.</summary>
    public bool OutboxEnabled { get; set; } = true;

    public int OutboxPollSeconds { get; set; } = 10;
    public int OutboxBatchSize { get; set; } = 20;
    public int OutboxMaxAttempts { get; set; } = 5;
    public int OutboxLeaseMinutes { get; set; } = 5;
}
