using System.Text;
using Platform.Infrastructure.Tvan.Model;
using Platform.Infrastructure.Tvan.Transforms;

namespace Platform.Infrastructure.Tvan.Response;

public interface IResponseInterpreter
{
    Task<InterpretedResponse> InterpretAsync(TvanChannel channel, string raw, CancellationToken ct);
}

/// <summary>
/// Đối xứng với chiều gửi: bóc vỏ envelope, chạy ngược chuỗi transform, rồi đọc các path.
/// Toàn bộ ba bước đều lấy từ metadata nên NCC mới không cần code mới.
/// </summary>
internal sealed class ResponseInterpreter(
    IPayloadTransformPipeline transforms,
    IResponseValueExtractorRegistry extractors) : IResponseInterpreter
{
    public async Task<InterpretedResponse> InterpretAsync(TvanChannel channel, string raw, CancellationToken ct)
    {
        var map = channel.ResponseMap;
        var body = raw;

        if (map.Envelope is { PayloadPath.Length: > 0 } envelope)
        {
            body = extractors.Resolve(envelope.Format).Extract(raw, envelope.PayloadPath)
                   ?? throw new TvanProtocolException(
                       $"Không tìm thấy payload tại '{envelope.PayloadPath}' trong phản hồi của {channel.ProviderCode}.");
        }

        if (map.Transforms.Count > 0)
        {
            var bytes = await transforms.BackwardAsync(
                map.Transforms, Encoding.UTF8.GetBytes(body), channel.Secrets, channel.Args, ct);
            body = Encoding.UTF8.GetString(bytes);
        }

        var result = map.Result;
        var extractor = extractors.Resolve(result.Format);
        var status = result.SuccessPath is null ? null : extractor.Extract(body, result.SuccessPath);

        // SuccessPath null nghĩa là chỉ dựa vào HTTP status — gateway quyết định.
        var isSuccess = result.SuccessPath is null
                        || result.SuccessValues.Contains(status, StringComparer.OrdinalIgnoreCase);

        return new InterpretedResponse(
            IsSuccess: isSuccess,
            Code: result.CodePath is null ? status : extractor.Extract(body, result.CodePath),
            Message: result.MessagePath is null ? null : extractor.Extract(body, result.MessagePath),
            Reference: result.ReferencePath is null ? null : extractor.Extract(body, result.ReferencePath),
            Payload: body);
    }
}
