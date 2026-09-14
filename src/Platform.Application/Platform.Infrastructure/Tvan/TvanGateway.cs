using System.Text;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan.Http;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Infrastructure.Tvan.Model;
using Platform.Infrastructure.Tvan.Response;
using Platform.Infrastructure.Tvan.Templating;
using Platform.Infrastructure.Tvan.Transforms;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan;

/// <summary>
/// Ráp toàn bộ pipeline: transform -> render -> auth -> gửi -> diễn giải.
/// Không có một câu lệnh điều kiện nào theo tên nhà cung cấp; mọi khác biệt nằm trong metadata.
/// </summary>
internal sealed class TvanGateway(
    ITvanChannelFactory channelFactory,
    IHttpClientFactory httpClientFactory,
    ITemplateRenderer renderer,
    IPayloadTransformPipeline transforms,
    IResponseInterpreter interpreter,
    ILogger<TvanGateway> logger) : ITvanGateway
{
    public async Task<TvanDispatchResult> DispatchAsync(TvanDispatchRequest request, CancellationToken ct)
    {
        var channel = await channelFactory.CreateAsync(
            request.ProviderCode, request.TaxCode, request.OperationCode, ct);

        // 1) Chạy chuỗi transform trên XML gốc (gzip/AES/base64/bọc CommandData...).
        var payloadBytes = await transforms.ForwardAsync(
            channel.RequestTransforms,
            Encoding.UTF8.GetBytes(request.Xml),
            channel.Secrets,
            channel.Args,
            ct);

        var scope = BuildScope(request, channel, payloadBytes);

        // 2) Render URI, body và header từ template lấy trong DB.
        var path = renderer.Render(channel.PathTemplate, scope);
        var uri = new Uri(channel.BaseUrl, path);

        using var httpRequest = new HttpRequestMessage(channel.Method, uri);

        if (channel.Method != HttpMethod.Get && !string.IsNullOrEmpty(channel.BodyTemplate))
        {
            httpRequest.Content = new StringContent(
                renderer.Render(channel.BodyTemplate, scope), Encoding.UTF8, channel.ContentType);
        }

        foreach (var header in channel.Headers)
            httpRequest.Headers.TryAddWithoutValidation(header.Name, renderer.Render(header.ValueTemplate, scope));

        if (!string.IsNullOrEmpty(channel.SoapAction))
            httpRequest.Headers.TryAddWithoutValidation("SOAPAction", channel.SoapAction);

        // 3) Bàn giao channel cho các DelegatingHandler (auth, retry, audit).
        httpRequest.Options.Set(TvanRequestOptions.Channel, channel);
        if (request.TransactionId is { } transactionId)
            httpRequest.Options.Set(TvanRequestOptions.TransactionId, transactionId);

        // 4) Gửi, có timeout riêng theo cấu hình của từng NCC.
        var client = httpClientFactory.CreateClient(TvanHttpClients.Dispatch);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(channel.Timeout);

        using var response = await client.SendAsync(httpRequest, timeout.Token);
        var raw = await response.Content.ReadAsStringAsync(timeout.Token);

        logger.LogInformation("T-VAN {Provider} / {Operation} / {Key} -> HTTP {Status}",
            channel.ProviderCode, request.OperationCode, request.CorrelationKey, (int)response.StatusCode);

        // 5) Diễn giải phản hồi theo ResponseMap.
        var interpreted = await interpreter.InterpretAsync(channel, raw, ct);

        return new TvanDispatchResult(
            Accepted: response.IsSuccessStatusCode && interpreted.IsSuccess,
            ProviderCode: channel.ProviderCode,
            ProviderReference: interpreted.Reference,
            ResultCode: interpreted.Code,
            Message: interpreted.Message,
            HttpStatus: (int)response.StatusCode,
            RawResponse: raw);
    }

    private static TemplateScope BuildScope(
        TvanDispatchRequest request, TvanChannel channel, byte[] payloadBytes)
    {
        // args đóng vai trò giá trị mặc định của endpoint, Fields của request ghi đè lên.
        // Nhờ vậy template viết {{ msg.PBan }} là đủ, không cần biết giá trị đến từ đâu.
        var fields = new Dictionary<string, string?>(channel.Args, StringComparer.OrdinalIgnoreCase);

        foreach (var field in request.Fields)
            fields[field.Key] = field.Value;

        foreach (var reserved in new Dictionary<string, string?>
        {
            ["MTDiep"] = request.CorrelationKey,
            ["TransId"] = request.CorrelationKey,
            ["MST"] = request.TaxCode,
            ["Xml"] = request.Xml,
        })
        {
            fields[reserved.Key] = reserved.Value;
        }

        return new TemplateScope(
            Msg: fields,
            Cred: channel.Secrets.AsScope(),
            Args: channel.Args,
            Payload: Encoding.UTF8.GetString(payloadBytes));
    }
}
