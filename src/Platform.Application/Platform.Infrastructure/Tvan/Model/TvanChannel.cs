using System.Text.Json;

namespace Platform.Infrastructure.Tvan.Model;

/// <summary>
/// Mô tả bất biến của MỘT lời gọi cụ thể tới một T-VAN, đã resolve xong từ metadata.
/// Thuần POCO nên unit test dựng tay được, không cần EF hay DI.
/// </summary>
public sealed record TvanChannel
{
    public required string ProviderCode { get; init; }
    public required Guid ProviderId { get; init; }
    public required Uri BaseUrl { get; init; }
    public required string PathTemplate { get; init; }
    public required HttpMethod Method { get; init; }
    public required string ContentType { get; init; }
    public string? SoapAction { get; init; }
    public required string BodyTemplate { get; init; }
    public required IReadOnlyList<TransformStep> RequestTransforms { get; init; }
    public required IReadOnlyList<TvanHeaderRule> Headers { get; init; }
    public required TvanResponseMap ResponseMap { get; init; }
    public required TvanAuthDescriptor Auth { get; init; }
    public required IReadOnlyDictionary<string, string?> Args { get; init; }
    public required TvanSecretBag Secrets { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public int MaxAttempts { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 500;
}

/// <param name="Key">Key trong registry IPayloadTransform.</param>
public sealed record TransformStep(string Key, JsonElement Options);

public sealed record TvanHeaderRule(string Name, string ValueTemplate);

public sealed record TvanAuthDescriptor(
    string SchemeKey,
    Guid ProviderId,
    string TaxCode,
    Uri BaseUrl,
    JsonElement Config,
    TvanSecretBag Secrets);

/// <summary>
/// Bọc secret đã giải mã. ToString() bị chặn để secret không lọt vào log/exception message.
/// </summary>
public sealed class TvanSecretBag(IReadOnlyDictionary<string, string?> values)
{
    public static readonly TvanSecretBag Empty = new(new Dictionary<string, string?>());

    public string Require(string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : throw new TvanConfigurationException($"Thiếu secret '{key}' trong TvanCredential.");

    public string? Get(string key) => values.GetValueOrDefault(key);

    public IReadOnlyDictionary<string, string?> AsScope() => values;

    public override string ToString() => "***";
}
