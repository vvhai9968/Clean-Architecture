using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Tvan.Model;

namespace Platform.Infrastructure.Tvan.Transforms;

/// <summary>
/// Composite chạy chuỗi transform. Chiều gửi đi theo đúng thứ tự khai báo,
/// chiều nhận về tự đảo ngược — nên cấu hình chỉ cần viết MỘT chiều.
/// </summary>
internal sealed class PayloadTransformPipeline(IServiceProvider provider) : IPayloadTransformPipeline
{
    public async ValueTask<byte[]> ForwardAsync(
        IReadOnlyList<TransformStep> steps, byte[] input,
        TvanSecretBag secrets, IReadOnlyDictionary<string, string?> args, CancellationToken ct)
    {
        var buffer = input;
        foreach (var step in steps)
        {
            buffer = await Resolve(step.Key)
                .ForwardAsync(buffer, new TransformContext(step.Options, secrets, args), ct);
        }

        return buffer;
    }

    public async ValueTask<byte[]> BackwardAsync(
        IReadOnlyList<TransformStep> steps, byte[] input,
        TvanSecretBag secrets, IReadOnlyDictionary<string, string?> args, CancellationToken ct)
    {
        var buffer = input;
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            var step = steps[i];
            buffer = await Resolve(step.Key)
                .BackwardAsync(buffer, new TransformContext(step.Options, secrets, args), ct);
        }

        return buffer;
    }

    private IPayloadTransform Resolve(string key) =>
        provider.GetKeyedService<IPayloadTransform>(key)
        ?? throw new TvanConfigurationException(
            $"Chưa đăng ký transform '{key}'. Thêm AddKeyedSingleton<IPayloadTransform, ...>(\"{key}\").");
}
