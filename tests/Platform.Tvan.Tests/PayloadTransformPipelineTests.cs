using System.Text;
using System.Xml.Linq;

namespace Platform.Tvan.Tests;

public class PayloadTransformPipelineTests
{
    private const string BkavChain = """
        [
          { "key": "bkav-command-data", "options": { "cmdTypeArg": "CmdType" } },
          { "key": "gzip" },
          { "key": "aes-cbc", "options": { "secretRef": "PartnerToken", "mode": "CBC", "padding": "PKCS7" } },
          { "key": "base64" }
        ]
        """;

    private static readonly Dictionary<string, string?> Args = new() { ["CmdType"] = "501" };

    [Fact]
    public async Task Bkav_chain_round_trips_exactly()
    {
        var pipeline = TestData.Pipeline();
        var steps = TestData.Steps(BkavChain);
        var secrets = TestData.Secrets(("PartnerToken", TestData.BkavPartnerToken));

        var encoded = await pipeline.ForwardAsync(
            steps, Encoding.UTF8.GetBytes(TestData.InvoiceXml), secrets, Args, default);

        var decoded = await pipeline.BackwardAsync(steps, encoded, secrets, Args, default);

        Assert.Equal(TestData.InvoiceXml, Encoding.UTF8.GetString(decoded));
    }

    [Fact]
    public async Task Bkav_chain_output_is_base64_and_hides_the_plaintext()
    {
        var pipeline = TestData.Pipeline();

        var encoded = await pipeline.ForwardAsync(
            TestData.Steps(BkavChain),
            Encoding.UTF8.GetBytes(TestData.InvoiceXml),
            TestData.Secrets(("PartnerToken", TestData.BkavPartnerToken)),
            Args,
            default);

        var text = Encoding.ASCII.GetString(encoded);

        Assert.True(Convert.TryFromBase64String(text, new byte[text.Length], out _));
        Assert.DoesNotContain("InvoiceDataWS", text);
    }

    [Fact]
    public async Task CommandData_carries_CmdType_from_endpoint_args()
    {
        var pipeline = TestData.Pipeline();
        var steps = TestData.Steps("""[{ "key": "bkav-command-data" }]""");

        var wrapped = await pipeline.ForwardAsync(
            steps, Encoding.UTF8.GetBytes("<Invoice/>"), TvanSecretBagFactory.Empty, Args, default);

        var document = XDocument.Parse(Encoding.UTF8.GetString(wrapped));

        Assert.Equal("501", document.Root!.Element("CmdType")!.Value);
        Assert.Equal("<Invoice/>", document.Root.Element("CommandObject")!.Value);
    }

    [Fact]
    public async Task Missing_CmdType_fails_as_configuration_error_not_runtime_crash()
    {
        var pipeline = TestData.Pipeline();

        var error = await Assert.ThrowsAsync<TvanConfigurationException>(async () =>
            await pipeline.ForwardAsync(
                TestData.Steps("""[{ "key": "bkav-command-data" }]"""),
                Encoding.UTF8.GetBytes("<Invoice/>"),
                TvanSecretBagFactory.Empty,
                new Dictionary<string, string?>(),
                default));

        Assert.Contains("CmdType", error.Message);
    }

    [Fact]
    public async Task Minvoice_chain_is_base64_only()
    {
        var pipeline = TestData.Pipeline();

        var encoded = await pipeline.ForwardAsync(
            TestData.Steps("""[{ "key": "base64" }]"""),
            Encoding.UTF8.GetBytes(TestData.InvoiceXml),
            TvanSecretBagFactory.Empty,
            new Dictionary<string, string?>(),
            default);

        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(TestData.InvoiceXml)),
            Encoding.ASCII.GetString(encoded));
    }

    [Fact]
    public async Task Unregistered_transform_key_is_a_configuration_error()
    {
        var pipeline = TestData.Pipeline();

        var error = await Assert.ThrowsAsync<TvanConfigurationException>(async () =>
            await pipeline.ForwardAsync(
                TestData.Steps("""[{ "key": "rsa-sign" }]"""),
                [1, 2, 3],
                TvanSecretBagFactory.Empty,
                new Dictionary<string, string?>(),
                default));

        Assert.Contains("rsa-sign", error.Message);
    }
}

internal static class TvanSecretBagFactory
{
    public static Platform.Infrastructure.Tvan.Model.TvanSecretBag Empty =>
        Platform.Infrastructure.Tvan.Model.TvanSecretBag.Empty;
}
