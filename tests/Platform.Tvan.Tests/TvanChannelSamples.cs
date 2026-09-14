using Platform.Infrastructure.Tvan.Model;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>
/// Dựng TvanChannel đúng như metadata trong AppSettings/tvan-providers.json,
/// nhưng bằng POCO — không cần DB, không cần EF, không cần DI container.
/// Việc dựng được như thế này chính là thước đo tính test-được của thiết kế.
/// </summary>
internal static class TvanChannelSamples
{
    public const string BkavTransforms = """
        [
          { "key": "bkav-command-data", "options": { "cmdTypeArg": "CmdType" } },
          { "key": "gzip" },
          { "key": "aes-cbc", "options": { "secretRef": "PartnerToken", "mode": "CBC", "padding": "PKCS7" } },
          { "key": "base64" }
        ]
        """;

    private const string BkavBody =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
        "<ExecuteCommand xmlns=\"http://tempuri.org/\">" +
        "<PartnerGUID>{{ cred.PartnerGUID }}</PartnerGUID>" +
        "<EncryptedCommandData>{{ payload }}</EncryptedCommandData>" +
        "</ExecuteCommand></soap:Body></soap:Envelope>";

    public static TvanChannel Bkav() => new()
    {
        ProviderCode = TvanProviderCodes.Bkav,
        ProviderId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        BaseUrl = new Uri("https://wstndemo.ehoadon.vn"),
        PathTemplate = "/WSPublicETN.asmx",
        Method = HttpMethod.Post,
        ContentType = "text/xml",
        SoapAction = "http://tempuri.org/ExecuteCommand",
        BodyTemplate = BkavBody,
        RequestTransforms = TestData.Steps(BkavTransforms),
        Headers = [],
        Args = new Dictionary<string, string?> { ["CmdType"] = "501" },
        Secrets = TestData.Secrets(
            ("PartnerGUID", TestData.BkavPartnerGuid),
            ("PartnerToken", TestData.BkavPartnerToken)),
        ResponseMap = TvanResponseMap.Parse("""
            {
              "envelope": { "format": "json", "payloadPath": "d" },
              "result": { "format": "json" }
            }
            """),
        Auth = Auth(TvanAuthSchemes.None, "{}", TvanSecretBag.Empty, "https://wstndemo.ehoadon.vn"),
        MaxAttempts = 1,
        BaseDelayMs = 1,
    };

    private const string HiloAuthConfig = """
        {
          "loginPath": "/api/authentication/gettoken",
          "bodyTemplate": "{\"TaxCode\":\"{{ cred.TaxCode }}\",\"UserName\":\"{{ cred.UserName }}\",\"Password\":\"{{ cred.Password }}\"}",
          "tokenPath": "Data.accessToken",
          "successPath": "Code",
          "successValues": ["200"],
          "messagePath": "Information",
          "headerFormat": "Bearer {{ msg.token }}",
          "ttlSeconds": 1800
        }
        """;

    private const string HiloBody =
        "{\"PBan\":\"{{ msg.PBan }}\",\"MNGui\":\"{{ cred.MNGui }}\",\"MNNhan\":\"{{ cred.MNNhan }}\"," +
        "\"MLTDiep\":{{ msg.MLTDiep }},\"MTDiep\":\"{{ msg.MTDiep }}\",\"MST\":\"{{ msg.MST }}\"," +
        "\"SLuong\":{{ msg.SLuong }},\"XmlData\":[{\"Xml\":{{ msg.Xml | json_string }}}]}";

    private static readonly TvanSecretBag HiloSecrets = TestData.Secrets(
        ("TaxCode", "0106713804"),
        ("UserName", "demo"),
        ("Password", "demo"),
        ("MNGui", "V0106713804"),
        ("MNNhan", "V0106713804"),
        ("CallbackBaseUrl", "https://gw.local"));

    public static TvanChannel Hilo() => new()
    {
        ProviderCode = TvanProviderCodes.Hilo,
        ProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        BaseUrl = new Uri("https://uatapitctn.hilo.com.vn"),
        PathTemplate = "/api/einvoicesolution/send",
        Method = HttpMethod.Post,
        ContentType = "application/json",
        BodyTemplate = HiloBody,
        RequestTransforms = [],
        Headers = [new TvanHeaderRule("CallBackUrl", "{{ cred.CallbackBaseUrl }}/api/tvan/callbacks/HILO")],
        Args = new Dictionary<string, string?> { ["PBan"] = "2.1.0", ["MLTDiep"] = "200", ["SLuong"] = "1" },
        Secrets = HiloSecrets,
        ResponseMap = TvanResponseMap.Parse("""
            {
              "result": {
                "format": "json",
                "successPath": "Code",
                "successValues": ["200"],
                "messagePath": "Information",
                "referencePath": "Data.MTDiep"
              }
            }
            """),
        Auth = Auth(TvanAuthSchemes.LoginToken, HiloAuthConfig, HiloSecrets, "https://uatapitctn.hilo.com.vn"),
        MaxAttempts = 1,
        BaseDelayMs = 1,
    };

    public static TvanChannel HiloQuery() => Hilo() with
    {
        PathTemplate = "/api/einvoicesolution/get?MTDiep={{ msg.MTDiep | url_encode }}",
        Method = HttpMethod.Get,
        BodyTemplate = string.Empty,
    };

    private const string MinvoiceAuthConfig = """
        {
          "loginPath": "/Account/Login",
          "bodyTemplate": "{\"username\":\"{{ cred.username }}\",\"password\":\"{{ cred.password }}\",\"ma_dvcs\":\"{{ cred.ma_dvcs }}\"}",
          "tokenPath": "token",
          "successPath": "code",
          "successValues": ["00"],
          "messagePath": "message",
          "headerFormat": "Bear {{ msg.token }};{{ cred.ma_dvcs }}",
          "ttlSeconds": 1800
        }
        """;

    private static readonly TvanSecretBag MinvoiceSecrets = TestData.Secrets(
        ("username", "minvoice"),
        ("password", "secret"),
        ("ma_dvcs", "0107726161"),
        ("mstTcgp", "0106026495"));

    public static TvanChannel Minvoice() => new()
    {
        ProviderCode = TvanProviderCodes.Minvoice,
        ProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        BaseUrl = new Uri("https://testmtvan.minvoice.net"),
        PathTemplate = "/Invoice/Hdoncma",
        Method = HttpMethod.Post,
        ContentType = "application/json",
        BodyTemplate =
            "{\"xmlData\":\"{{ payload }}\",\"mstNnt\":\"{{ msg.MST }}\"," +
            "\"mstTcgp\":\"{{ cred.mstTcgp }}\",\"transId\":\"{{ msg.TransId }}\"}",
        RequestTransforms = TestData.Steps("""[{ "key": "base64" }]"""),
        Headers = [],
        Args = new Dictionary<string, string?>(),
        Secrets = MinvoiceSecrets,
        ResponseMap = TvanResponseMap.Parse("""
            {
              "result": {
                "format": "json",
                "successPath": "code",
                "successValues": ["00"],
                "messagePath": "message",
                "referencePath": "data.maThongdiep"
              }
            }
            """),
        Auth = Auth(TvanAuthSchemes.LoginToken, MinvoiceAuthConfig, MinvoiceSecrets, "https://testmtvan.minvoice.net"),
        MaxAttempts = 1,
        BaseDelayMs = 1,
    };

    private static TvanAuthDescriptor Auth(string scheme, string config, TvanSecretBag secrets, string baseUrl) =>
        new(scheme, Guid.NewGuid(), "0101360697", new Uri(baseUrl), TestData.Json(config), secrets);
}
