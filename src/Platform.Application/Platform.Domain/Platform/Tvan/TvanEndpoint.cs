using Platform.Shared.Common;

namespace Platform.Domain.Platform.Tvan;

/// <summary>Map một nghiệp vụ chuẩn hoá (OperationCode) sang một lời gọi HTTP cụ thể của NCC.</summary>
public class TvanEndpoint : BaseEntity
{
    public Guid ProviderId { get; set; }
    public TvanProvider Provider { get; set; } = null!;

    /// <summary>Xem <see cref="Shared.Constants.TvanOperations"/>.</summary>
    public string OperationCode { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";

    /// <summary>Path tương đối, cho phép placeholder: "/api/get?MTDiep={{ msg.MTDiep }}".</summary>
    public string PathTemplate { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/json";

    /// <summary>Chỉ dùng cho SOAP.</summary>
    public string? SoapAction { get; set; }

    /// <summary>
    /// Template body. Placeholder khả dụng: {{ msg.* }}, {{ cred.* }}, {{ args.* }}, {{ payload }}.
    /// Filter: | json_escape, | xml_escape, | upper, | lower, | raw.
    /// </summary>
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>Chuỗi transform áp lên XML trước khi nhúng vào body. jsonb array.</summary>
    public string RequestTransformsJson { get; set; } = "[]";

    /// <summary>Cách bóc response: envelope -> inverse transforms -> extract paths. jsonb.</summary>
    public string ResponseMapJson { get; set; } = "{}";

    /// <summary>Hằng số riêng của endpoint, vd {"CmdType":"501"}. jsonb.</summary>
    public string ArgsJson { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
}
