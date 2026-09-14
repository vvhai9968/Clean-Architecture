namespace Platform.Infrastructure.Tvan.Templating;

/// <summary>
/// Render template lấy từ DB. Tách thành interface để có thể đổi sang Scriban/Liquid
/// mà không đụng tới gateway, và để test render riêng biệt.
/// </summary>
public interface ITemplateRenderer
{
    string Render(string template, TemplateScope scope);
}

/// <summary>
/// Dữ liệu đổ vào template.
/// <list type="bullet">
/// <item><c>{{ msg.X }}</c> — trường của thông điệp (MTDiep, MST, Xml, token...).</item>
/// <item><c>{{ cred.X }}</c> — secret của credential (PartnerGUID, password, ma_dvcs...).</item>
/// <item><c>{{ args.X }}</c> — hằng số khai báo ở endpoint (CmdType, MLTDiep...).</item>
/// <item><c>{{ payload }}</c> — payload sau khi chạy hết chuỗi transform.</item>
/// </list>
/// </summary>
public sealed record TemplateScope(
    IReadOnlyDictionary<string, string?> Msg,
    IReadOnlyDictionary<string, string?> Cred,
    IReadOnlyDictionary<string, string?> Args,
    string Payload)
{
    public static TemplateScope ForAuth(string token, IReadOnlyDictionary<string, string?> credentials) =>
        new(new Dictionary<string, string?> { ["token"] = token },
            credentials,
            new Dictionary<string, string?>(),
            token);
}
