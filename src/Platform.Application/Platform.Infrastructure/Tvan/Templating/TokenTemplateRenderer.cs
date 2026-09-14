using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Infrastructure.Tvan.Templating;

/// <summary>
/// Template engine tối giản, không phụ thuộc thư viện ngoài.
/// Cú pháp: <c>{{ scope.key | filter | filter }}</c> hoặc <c>{{ payload }}</c>.
/// <para>
/// Cố tình KHÔNG hỗ trợ vòng lặp/điều kiện: template lấy từ DB nên phải là dữ liệu thuần,
/// không được là nơi nhét logic. Nghiệp vụ phức tạp thì viết transform, đừng viết vào template.
/// </para>
/// </summary>
public sealed partial class TokenTemplateRenderer : ITemplateRenderer
{
    private static readonly Regex TokenPattern = BuildTokenPattern();

    public string Render(string template, TemplateScope scope)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return TokenPattern.Replace(template, match =>
        {
            var parts = match.Groups["expr"].Value
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var value = Resolve(parts[0], scope);
            for (var i = 1; i < parts.Length; i++)
                value = ApplyFilter(parts[i], value);

            return value;
        });
    }

    private static string Resolve(string path, TemplateScope scope)
    {
        if (string.Equals(path, "payload", StringComparison.OrdinalIgnoreCase))
            return scope.Payload;

        var separator = path.IndexOf('.');
        if (separator <= 0)
            throw new TvanConfigurationException(
                $"Biểu thức template '{path}' không hợp lệ. Dùng dạng msg.X, cred.X, args.X hoặc payload.");

        var root = path[..separator];
        var key = path[(separator + 1)..];

        var source = root.ToLowerInvariant() switch
        {
            "msg" => scope.Msg,
            "cred" => scope.Cred,
            "args" => scope.Args,
            _ => throw new TvanConfigurationException(
                $"Scope '{root}' không tồn tại. Chỉ có msg, cred, args, payload."),
        };

        // Thiếu biến thì render rỗng thay vì ném lỗi: nhiều trường của QĐ 1450 là optional.
        return source.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static string ApplyFilter(string filter, string value) => filter.ToLowerInvariant() switch
    {
        "raw" => value,
        "json_escape" => JsonEscape(value),
        "json_string" => JsonSerializer.Serialize(value),
        "xml_escape" => SecurityElement.Escape(value) ?? string.Empty,
        "url_encode" => Uri.EscapeDataString(value),
        "base64" => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
        "upper" => value.ToUpperInvariant(),
        "lower" => value.ToLowerInvariant(),
        "trim" => value.Trim(),
        _ => throw new TvanConfigurationException($"Filter '{filter}' chưa được hỗ trợ."),
    };

    /// <summary>Escape để nhúng vào GIỮA một chuỗi JSON đã có sẵn dấu nháy bao ngoài.</summary>
    private static string JsonEscape(string value)
    {
        var quoted = JsonSerializer.Serialize(value);
        return quoted.Length >= 2 ? quoted[1..^1] : quoted;
    }

    [GeneratedRegex(@"\{\{\s*(?<expr>[^{}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex BuildTokenPattern();
}
