using System.Text.Json;
using Platform.Infrastructure.Tvan.Model;

namespace Platform.Infrastructure.Tvan.Transforms;

/// <summary>
/// Một bước biến đổi payload, có chiều thuận (gửi đi) và chiều nghịch (nhận về).
/// Thêm primitive mới cho NCC mới = thêm một class implement interface này + một dòng đăng ký DI.
/// </summary>
public interface IPayloadTransform
{
    ValueTask<byte[]> ForwardAsync(byte[] input, TransformContext context, CancellationToken ct);
    ValueTask<byte[]> BackwardAsync(byte[] input, TransformContext context, CancellationToken ct);
}

public sealed record TransformContext(
    JsonElement Options,
    TvanSecretBag Secrets,
    IReadOnlyDictionary<string, string?> Args)
{
    public string? Option(string name) =>
        Options.ValueKind == JsonValueKind.Object && Options.TryGetProperty(name, out var value)
            ? value.ToString()
            : null;

    public string OptionOrDefault(string name, string fallback) => Option(name) ?? fallback;
}

/// <summary>Chạy một chuỗi transform theo cấu hình. Chiều nghịch tự động đảo thứ tự.</summary>
public interface IPayloadTransformPipeline
{
    ValueTask<byte[]> ForwardAsync(
        IReadOnlyList<TransformStep> steps, byte[] input,
        TvanSecretBag secrets, IReadOnlyDictionary<string, string?> args, CancellationToken ct);

    ValueTask<byte[]> BackwardAsync(
        IReadOnlyList<TransformStep> steps, byte[] input,
        TvanSecretBag secrets, IReadOnlyDictionary<string, string?> args, CancellationToken ct);
}
