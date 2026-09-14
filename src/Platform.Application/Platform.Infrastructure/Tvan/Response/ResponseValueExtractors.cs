using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.DependencyInjection;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan.Response;

/// <summary>Đọc một giá trị ra khỏi payload phản hồi theo đường dẫn khai báo trong metadata.</summary>
public interface IResponseValueExtractor
{
    string? Extract(string payload, string path);
}

public interface IResponseValueExtractorRegistry
{
    IResponseValueExtractor Resolve(string format);
}

internal sealed class ResponseValueExtractorRegistry(IServiceProvider provider) : IResponseValueExtractorRegistry
{
    public IResponseValueExtractor Resolve(string format) =>
        provider.GetKeyedService<IResponseValueExtractor>(format)
        ?? throw new TvanConfigurationException(
            $"Chưa đăng ký extractor cho format '{format}'. Hỗ trợ sẵn: json, xml.");
}

/// <summary>
/// Đường dẫn dạng chấm, có hỗ trợ chỉ số mảng: <c>Data.Response[0].Message</c>.
/// Đủ dùng cho mọi response của BKAV/HILO/Minvoice mà không kéo thêm thư viện JSONPath.
/// </summary>
public sealed partial class JsonValueExtractor : IResponseValueExtractor
{
    private static readonly Regex SegmentPattern = BuildSegmentPattern();

    public string? Extract(string payload, string path)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var current = document.RootElement;

            foreach (Match segment in SegmentPattern.Matches(path))
            {
                var name = segment.Groups["name"].Value;
                if (name.Length > 0)
                {
                    if (current.ValueKind != JsonValueKind.Object ||
                        !current.TryGetProperty(name, out current))
                        return null;
                }

                foreach (Capture index in segment.Groups["index"].Captures)
                {
                    if (current.ValueKind != JsonValueKind.Array) return null;

                    var position = int.Parse(index.Value);
                    if (position >= current.GetArrayLength()) return null;
                    current = current[position];
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => current.GetString(),
                _ => current.ToString(),
            };
        }
        catch (JsonException ex)
        {
            throw new TvanProtocolException($"Phản hồi không phải JSON hợp lệ (path '{path}').", ex);
        }
    }

    [GeneratedRegex(@"(?<name>[^.\[\]]*)(\[(?<index>\d+)\])*", RegexOptions.Compiled)]
    private static partial Regex BuildSegmentPattern();
}

/// <summary>
/// XPath thuần. Với XML có namespace (thông điệp QĐ 1450), viết đường dẫn theo dạng
/// <c>//*[local-name()='MTDiep']</c> để không phải khai báo prefix trong config.
/// </summary>
public sealed class XmlValueExtractor : IResponseValueExtractor
{
    public string? Extract(string payload, string path)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var document = XDocument.Parse(payload);
            var result = document.XPathEvaluate(path);

            return result switch
            {
                IEnumerable<object> nodes => nodes.Select(NodeValue).FirstOrDefault(x => x is not null),
                string text => text,
                double number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bool flag => flag.ToString(),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or XPathException)
        {
            throw new TvanProtocolException($"Phản hồi không phải XML hợp lệ (path '{path}').", ex);
        }
    }

    private static string? NodeValue(object node) => node switch
    {
        XElement element => element.Value,
        XAttribute attribute => attribute.Value,
        XText text => text.Value,
        _ => null,
    };
}
