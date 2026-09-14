using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan.Model;

/// <summary>
/// Khai báo cách bóc tách phản hồi của NCC. Ba bước, tất cả đều optional:
/// 1) Envelope  — bóc payload thật ra khỏi vỏ (BKAV bọc trong {"d": "..."}).
/// 2) Transforms — áp ngược chuỗi transform (base64 -> AES -> gunzip).
/// 3) Result     — đọc các path để lấy mã kết quả / thông điệp / mã tham chiếu.
/// </summary>
public sealed record TvanResponseMap
{
    public TvanEnvelopeMap? Envelope { get; init; }

    /// <summary>Ghi theo THỨ TỰ CHIỀU GỬI; pipeline sẽ tự áp ngược.</summary>
    public IReadOnlyList<TransformStep> Transforms { get; init; } = [];

    public TvanResultMap Result { get; init; } = new();

    public static TvanResponseMap Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = document.RootElement;

            return new TvanResponseMap
            {
                Envelope = root.TryGetProperty("envelope", out var envelope)
                    ? new TvanEnvelopeMap(
                        ReadString(envelope, "format") ?? TvanResponseFormats.Json,
                        ReadString(envelope, "payloadPath") ?? string.Empty)
                    : null,
                Transforms = root.TryGetProperty("transforms", out var transforms)
                    ? ParseTransforms(transforms)
                    : [],
                Result = root.TryGetProperty("result", out var result)
                    ? new TvanResultMap
                    {
                        Format = ReadString(result, "format") ?? TvanResponseFormats.Json,
                        SuccessPath = ReadString(result, "successPath"),
                        SuccessValues = ReadArray(result, "successValues"),
                        CodePath = ReadString(result, "codePath"),
                        MessagePath = ReadString(result, "messagePath"),
                        ReferencePath = ReadString(result, "referencePath"),
                    }
                    : new TvanResultMap(),
            };
        }
        catch (JsonException ex)
        {
            throw new TvanConfigurationException($"ResponseMapJson không hợp lệ: {ex.Message}");
        }
    }

    public static IReadOnlyList<TransformStep> ParseTransforms(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return [];

        return array.EnumerateArray()
            .Select(step => new TransformStep(
                step.TryGetProperty("key", out var key)
                    ? key.GetString() ?? throw new TvanConfigurationException("transform.key rỗng.")
                    : throw new TvanConfigurationException("transform thiếu thuộc tính 'key'."),
                step.TryGetProperty("options", out var options) ? options.Clone() : default))
            .ToArray();
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string[] ReadArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(x => x.ToString()).ToArray()
            : [];
}

public sealed record TvanEnvelopeMap(string Format, string PayloadPath);

public sealed record TvanResultMap
{
    public string Format { get; init; } = TvanResponseFormats.Json;

    /// <summary>Null nghĩa là chỉ dựa vào HTTP status để kết luận thành công.</summary>
    public string? SuccessPath { get; init; }

    public string[] SuccessValues { get; init; } = [];
    public string? CodePath { get; init; }
    public string? MessagePath { get; init; }
    public string? ReferencePath { get; init; }
}

/// <summary>Kết quả sau khi diễn giải response.</summary>
public sealed record InterpretedResponse(
    bool IsSuccess,
    string? Code,
    string? Message,
    string? Reference,
    string Payload);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
