using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Tvan.Http;
using Platform.Infrastructure.Tvan.Model;
using Platform.Infrastructure.Tvan.Response;
using Platform.Infrastructure.Tvan.Transforms;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>Transport giả — bắt lại request và trả về phản hồi dựng sẵn.</summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        return responder(request, Requests.Count);
    }

    public static StubHttpMessageHandler Returning(HttpStatusCode status, string body) =>
        new((_, _) => new HttpResponseMessage(status) { Content = new StringContent(body) });
}

/// <summary>
/// IHttpClientFactory tối giản, ráp đúng chuỗi handler như production
/// để test cả hành vi của DelegatingHandler chứ không chỉ của gateway.
/// </summary>
internal sealed class TestHttpClientFactory(
    HttpMessageHandler dispatchTransport,
    IServiceProvider services,
    HttpMessageHandler? loginTransport = null) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        if (name == TvanHttpClients.Login)
            return new HttpClient(loginTransport ?? dispatchTransport);

        var audit = new TvanAuditDelegatingHandler(
            new NullScopeFactory(), NullLogger<TvanAuditDelegatingHandler>.Instance)
        {
            InnerHandler = dispatchTransport,
        };

        var auth = new TvanAuthDelegatingHandler(services, NullLogger<TvanAuthDelegatingHandler>.Instance)
        {
            InnerHandler = audit,
        };

        var retry = new TvanRetryDelegatingHandler(NullLogger<TvanRetryDelegatingHandler>.Instance)
        {
            InnerHandler = auth,
        };

        return new HttpClient(retry);
    }

    private sealed class NullScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Audit không được gọi khi request không mang TransactionId.");
    }
}

internal static class TestData
{
    /// <summary>PartnerToken mẫu trong tài liệu BKAV (key_base64:iv_base64).</summary>
    public const string BkavPartnerToken =
        "54dSxtErH+vsKKfL4PKaoerNYE6dwzmpzkLAxity8F4=:+bRSEW7FUEnzLy9xjuP5wA==";

    public const string BkavPartnerGuid = "8bd1e2d0-ae76-4aeb-a603-57eac7e2d159";

    public const string InvoiceXml =
        "<ArrayOfInvoiceDataWS><InvoiceDataWS><Invoice><BuyerName>Cong ty A &amp; B</BuyerName></Invoice></InvoiceDataWS></ArrayOfInvoiceDataWS>";

    public static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    public static IPayloadTransformPipeline Pipeline() =>
        new PayloadTransformPipeline(new TransformServiceProvider());

    public static IResponseValueExtractorRegistry Extractors() =>
        new ResponseValueExtractorRegistry(new TransformServiceProvider());

    public static TvanSecretBag Secrets(params (string Key, string Value)[] pairs) =>
        new(pairs.ToDictionary(x => x.Key, x => (string?)x.Value));

    public static IReadOnlyList<TransformStep> Steps(string json) =>
        TvanResponseMap.ParseTransforms(Json(json));
}

/// <summary>
/// Registry keyed tối giản — chứng minh các transform/extractor không phụ thuộc DI container thật.
/// </summary>
internal sealed class TransformServiceProvider : IServiceProvider, IKeyedServiceProvider
{
    public object? GetService(Type serviceType) => null;

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (serviceType == typeof(IPayloadTransform))
        {
            return (serviceKey as string) switch
            {
                TvanTransforms.Gzip => new GzipTransform(),
                TvanTransforms.Base64 => new Base64Transform(),
                TvanTransforms.AesCbc => new AesCbcTransform(),
                TvanTransforms.BkavCommandData => (IPayloadTransform)new BkavCommandDataTransform(),
                _ => null,
            };
        }

        if (serviceType == typeof(IResponseValueExtractor))
        {
            return (serviceKey as string) switch
            {
                TvanResponseFormats.Json => new JsonValueExtractor(),
                TvanResponseFormats.Xml => (IResponseValueExtractor)new XmlValueExtractor(),
                _ => null,
            };
        }

        return null;
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey)
        ?? throw new InvalidOperationException($"Không có service {serviceType.Name} với key {serviceKey}.");
}
