namespace Platform.Shared.Constants;

/// <summary>
/// Nghiệp vụ chuẩn hoá theo QĐ 1450/QĐ-TCT — KHÔNG dùng thuật ngữ riêng của bất kỳ T-VAN nào.
/// Mỗi T-VAN tự map operation này sang endpoint của họ qua bảng TvanEndpoints.
/// </summary>
public static class TvanOperations
{
    public const string SendRegistration = "send-registration";
    public const string SendInvoiceWithCode = "send-invoice-coded";
    public const string SendInvoiceNoCode = "send-invoice-uncoded";
    public const string SendPosInvoice = "send-invoice-pos";
    public const string SendErrorNotice = "send-error-notice";
    public const string SendSummary = "send-summary";
    public const string QueryResult = "query-result";

    public static readonly string[] All =
    [
        SendRegistration, SendInvoiceWithCode, SendInvoiceNoCode,
        SendPosInvoice, SendErrorNotice, SendSummary, QueryResult,
    ];
}

/// <summary>Key registry của ITvanAuthStrategy.</summary>
public static class TvanAuthSchemes
{
    public const string None = "none";

    /// <summary>Login lấy token rồi gắn header. Phủ cả HILO (Bearer) lẫn Minvoice (Bear {t};{dvcs}).</summary>
    public const string LoginToken = "login-token";
}

/// <summary>Key registry của IPayloadTransform.</summary>
public static class TvanTransforms
{
    public const string Gzip = "gzip";
    public const string AesCbc = "aes-cbc";
    public const string Base64 = "base64";
    public const string BkavCommandData = "bkav-command-data";
}

/// <summary>Key registry của IResponseValueExtractor.</summary>
public static class TvanResponseFormats
{
    public const string Json = "json";
    public const string Xml = "xml";
}

/// <summary>Tên named HttpClient.</summary>
public static class TvanHttpClients
{
    /// <summary>Client gửi nghiệp vụ — có auth + audit + retry handler.</summary>
    public const string Dispatch = "tvan-dispatch";

    /// <summary>Client gọi API login — KHÔNG có auth handler, tránh đệ quy vô hạn.</summary>
    public const string Login = "tvan-login";
}

/// <summary>Claim gắn vào JWT để xác định T-VAN của từng account.</summary>
public static class TvanClaimTypes
{
    /// <summary>Mã T-VAN mặc định của account, vd "BKAV".</summary>
    public const string ProviderCode = "tvan_provider";

    /// <summary>MST người nộp thuế gắn với account.</summary>
    public const string TaxCode = "tvan_taxcode";
}

/// <summary>Mã provider dùng khi seed. Provider mới KHÔNG cần thêm hằng số ở đây.</summary>
public static class TvanProviderCodes
{
    public const string Bkav = "BKAV";
    public const string Hilo = "HILO";
    public const string Minvoice = "MINVOICE";
}
