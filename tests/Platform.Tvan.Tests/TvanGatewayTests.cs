using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan;
using Platform.Infrastructure.Tvan.Auth;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Infrastructure.Tvan.Model;
using Platform.Infrastructure.Tvan.Response;
using Platform.Infrastructure.Tvan.Templating;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>
/// Ba nhà cung cấp có đặc thù kỹ thuật hoàn toàn khác nhau đi qua CÙNG một TvanGateway.
/// Nếu các test dưới đây còn xanh mà không có class nào mang tên nhà cung cấp trong lõi,
/// thì thiết kế metadata-driven đang thật sự hoạt động.
/// </summary>
public class TvanGatewayTests
{
    [Fact]
    public async Task Bkav_sends_soap_envelope_with_encrypted_payload_and_no_auth_header()
    {
        var transport = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"d":""}""");
        var gateway = Build(TvanChannelSamples.Bkav(), transport);

        await gateway.DispatchAsync(Request("BKAV", TvanOperations.SendInvoiceWithCode), default);

        var request = transport.Requests.Single();
        var body = transport.Bodies.Single()!;

        Assert.Equal("http://tempuri.org/ExecuteCommand", request.Headers.GetValues("SOAPAction").Single());
        Assert.False(request.Headers.Contains("Authorization"));
        Assert.Equal("text/xml", request.Content!.Headers.ContentType!.MediaType);

        var envelope = XDocument.Parse(body);
        var guid = envelope.Descendants().Single(x => x.Name.LocalName == "PartnerGUID").Value;
        var encrypted = envelope.Descendants().Single(x => x.Name.LocalName == "EncryptedCommandData").Value;

        Assert.Equal(TestData.BkavPartnerGuid, guid);
        Assert.DoesNotContain("InvoiceDataWS", encrypted);

        // Giải mã ngược đúng chuỗi transform phải ra lại CommandData với CmdType của endpoint.
        var decrypted = await TestData.Pipeline().BackwardAsync(
            TestData.Steps(TvanChannelSamples.BkavTransforms),
            Encoding.UTF8.GetBytes(encrypted),
            TestData.Secrets(("PartnerToken", TestData.BkavPartnerToken)),
            new Dictionary<string, string?> { ["CmdType"] = "501" },
            default);

        Assert.Equal(TestData.InvoiceXml, Encoding.UTF8.GetString(decrypted));
    }

    [Fact]
    public async Task Hilo_logs_in_then_sends_bearer_token_and_callback_header()
    {
        var login = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"Code":200,"Information":"ok","Data":{"accessToken":"JWT-ABC"}}""");
        var transport = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"Code":200,"Information":"Thanh cong","Data":{"MTDiep":"V0106713804AAA"}}""");

        var gateway = Build(TvanChannelSamples.Hilo(), transport, login);

        var result = await gateway.DispatchAsync(Request("HILO", TvanOperations.SendInvoiceWithCode), default);

        var request = transport.Requests.Single();
        Assert.Equal("Bearer JWT-ABC", request.Headers.GetValues("Authorization").Single());
        Assert.Equal("https://gw.local/api/tvan/callbacks/HILO", request.Headers.GetValues("CallBackUrl").Single());

        // XML đi thẳng vào JSON, không mã hoá — đúng đặc thù HILO.
        using var body = System.Text.Json.JsonDocument.Parse(transport.Bodies.Single()!);
        Assert.Equal(TestData.InvoiceXml, body.RootElement.GetProperty("XmlData")[0].GetProperty("Xml").GetString());
        Assert.Equal(200, body.RootElement.GetProperty("MLTDiep").GetInt32());

        Assert.True(result.Accepted);
        Assert.Equal("V0106713804AAA", result.ProviderReference);
    }

    [Fact]
    public async Task Minvoice_uses_the_same_auth_strategy_with_a_different_header_format()
    {
        var login = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"code":"00","token":"TK-9"}""");
        var transport = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"code":"00","message":null,"data":{"maThongdiep":"V0106026495XYZ"}}""");

        var gateway = Build(TvanChannelSamples.Minvoice(), transport, login);

        var result = await gateway.DispatchAsync(Request("MINVOICE", TvanOperations.SendInvoiceWithCode), default);

        // Cùng LoginTokenAuthStrategy như HILO, chỉ khác headerFormat trong metadata.
        Assert.Equal("Bear TK-9;0107726161",
            transport.Requests.Single().Headers.GetValues("Authorization").Single());

        using var body = System.Text.Json.JsonDocument.Parse(transport.Bodies.Single()!);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(TestData.InvoiceXml)),
            body.RootElement.GetProperty("xmlData").GetString());

        Assert.True(result.Accepted);
        Assert.Equal("V0106026495XYZ", result.ProviderReference);
    }

    [Fact]
    public async Task Business_error_code_is_reported_as_not_accepted_without_retrying()
    {
        var login = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"code":"00","token":"TK-9"}""");
        var transport = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"code":"06","message":"Ten hoac mat khau khong dung","data":null}""");

        var gateway = Build(TvanChannelSamples.Minvoice(), transport, login);

        var result = await gateway.DispatchAsync(Request("MINVOICE", TvanOperations.SendInvoiceWithCode), default);

        Assert.False(result.Accepted);
        Assert.Equal("06", result.ResultCode);
        // Lỗi nghiệp vụ KHÔNG được gửi lại: hoá đơn trùng trên CQT nguy hiểm hơn gửi hụt.
        Assert.False(result.IsTransient);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task Expired_token_triggers_one_relogin_and_one_retry()
    {
        var loginCount = 0;
        var login = new StubHttpMessageHandler((_, _) =>
        {
            loginCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"code":"00","token":"TK-{{loginCount}}"}"""),
            };
        });

        var transport = new StubHttpMessageHandler((_, attempt) => attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"code":"00","data":{"maThongdiep":"OK"}}"""),
            });

        var gateway = Build(TvanChannelSamples.Minvoice(), transport, login);

        var result = await gateway.DispatchAsync(Request("MINVOICE", TvanOperations.SendInvoiceWithCode), default);

        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(2, loginCount);
        Assert.Equal("Bear TK-2;0107726161", transport.Requests[1].Headers.GetValues("Authorization").Single());
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task Transient_server_error_is_retried_up_to_max_attempts()
    {
        var login = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"code":"00","token":"TK"}""");
        var transport = new StubHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("{}") });

        var channel = TvanChannelSamples.Minvoice() with { MaxAttempts = 3, BaseDelayMs = 1 };
        var gateway = Build(channel, transport, login);

        var result = await gateway.DispatchAsync(Request("MINVOICE", TvanOperations.SendInvoiceWithCode), default);

        Assert.Equal(3, transport.Requests.Count);
        Assert.False(result.Accepted);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public async Task Query_endpoint_renders_the_correlation_key_into_the_query_string()
    {
        var login = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"Code":200,"Data":{"accessToken":"JWT"}}""");
        var transport = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK, """{"Code":200,"Data":{"MTDiep":"V01"}}""");

        var gateway = Build(TvanChannelSamples.HiloQuery(), transport, login);

        await gateway.DispatchAsync(Request("HILO", TvanOperations.QueryResult) with { Xml = string.Empty }, default);

        var uri = transport.Requests.Single().RequestUri!;
        Assert.Equal("/api/einvoicesolution/get", uri.AbsolutePath);
        Assert.Contains("MTDiep=V0101360697ABC", uri.Query);
        Assert.Null(transport.Requests.Single().Content);
    }

    // --- helpers -------------------------------------------------------------

    private static TvanDispatchRequest Request(string providerCode, string operationCode) => new()
    {
        ProviderCode = providerCode,
        TaxCode = "0101360697",
        OperationCode = operationCode,
        Xml = TestData.InvoiceXml,
        CorrelationKey = "V0101360697ABC",
    };

    private static ITvanGateway Build(
        TvanChannel channel, HttpMessageHandler transport, HttpMessageHandler? login = null)
    {
        var renderer = new TokenTemplateRenderer();
        var extractors = TestData.Extractors();
        var pipeline = TestData.Pipeline();

        var services = new AuthServiceProvider();
        var httpClientFactory = new TestHttpClientFactory(transport, services, login);

        services.Strategies[TvanAuthSchemes.None] = new NoneAuthStrategy();
        services.Strategies[TvanAuthSchemes.LoginToken] = new LoginTokenAuthStrategy(
            httpClientFactory,
            renderer,
            extractors,
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<LoginTokenAuthStrategy>.Instance);

        return new TvanGateway(
            new StubChannelFactory(channel),
            httpClientFactory,
            renderer,
            pipeline,
            new ResponseInterpreter(pipeline, extractors),
            NullLogger<TvanGateway>.Instance);
    }

    private sealed class StubChannelFactory(TvanChannel channel) : ITvanChannelFactory
    {
        public Task<TvanChannel> CreateAsync(
            string providerCode, string taxCode, string operationCode, CancellationToken ct) =>
            Task.FromResult(channel);
    }

    private sealed class AuthServiceProvider : IServiceProvider, IKeyedServiceProvider
    {
        public Dictionary<string, ITvanAuthStrategy> Strategies { get; } = [];

        public object? GetService(Type serviceType) => null;

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            serviceType == typeof(ITvanAuthStrategy) && serviceKey is string key
                ? Strategies.GetValueOrDefault(key)
                : null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException($"Không có {serviceType.Name} key {serviceKey}.");
    }
}
