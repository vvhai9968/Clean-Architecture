using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Platform.Infrastructure.Tvan.Transforms;

/// <summary>Nén/giải nén GZIP (BKAV bước 3 chiều gửi).</summary>
public sealed class GzipTransform : IPayloadTransform
{
    public async ValueTask<byte[]> ForwardAsync(byte[] input, TransformContext context, CancellationToken ct)
    {
        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            await gzip.WriteAsync(input, ct);
        }

        return output.ToArray();
    }

    public async ValueTask<byte[]> BackwardAsync(byte[] input, TransformContext context, CancellationToken ct)
    {
        using var source = new MemoryStream(input);
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output, ct);
        return output.ToArray();
    }
}

/// <summary>Base64 encode/decode (BKAV bước cuối, Minvoice xmlData).</summary>
public sealed class Base64Transform : IPayloadTransform
{
    public ValueTask<byte[]> ForwardAsync(byte[] input, TransformContext context, CancellationToken ct) =>
        ValueTask.FromResult(Encoding.ASCII.GetBytes(Convert.ToBase64String(input)));

    public ValueTask<byte[]> BackwardAsync(byte[] input, TransformContext context, CancellationToken ct)
    {
        var text = Encoding.ASCII.GetString(input).Trim();
        try
        {
            return ValueTask.FromResult(Convert.FromBase64String(text));
        }
        catch (FormatException ex)
        {
            throw new TvanProtocolException("Phản hồi không phải chuỗi Base64 hợp lệ.", ex);
        }
    }
}

/// <summary>
/// AES-256 CBC. Key/IV lấy từ một secret dạng <c>&lt;key_base64&gt;:&lt;iv_base64&gt;</c>
/// (chính là PartnerToken mà BKAV cấp cho Partner).
/// </summary>
public sealed class AesCbcTransform : IPayloadTransform
{
    public ValueTask<byte[]> ForwardAsync(byte[] input, TransformContext context, CancellationToken ct) =>
        ValueTask.FromResult(Run(input, context, encrypt: true));

    public ValueTask<byte[]> BackwardAsync(byte[] input, TransformContext context, CancellationToken ct) =>
        ValueTask.FromResult(Run(input, context, encrypt: false));

    private static byte[] Run(byte[] input, TransformContext context, bool encrypt)
    {
        var secretRef = context.OptionOrDefault("secretRef", "PartnerToken");
        var separator = context.OptionOrDefault("separator", ":");
        var parts = context.Secrets.Require(secretRef).Split(separator);

        if (parts.Length != 2)
        {
            throw new TvanConfigurationException(
                $"Secret '{secretRef}' phải có dạng <key_base64>{separator}<iv_base64>.");
        }

        using var aes = Aes.Create();
        aes.Mode = Enum.Parse<CipherMode>(context.OptionOrDefault("mode", nameof(CipherMode.CBC)), ignoreCase: true);
        aes.Padding = Enum.Parse<PaddingMode>(context.OptionOrDefault("padding", nameof(PaddingMode.PKCS7)), ignoreCase: true);
        aes.Key = Convert.FromBase64String(parts[0]);
        aes.IV = Convert.FromBase64String(parts[1]);

        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }
}

/// <summary>
/// Bọc XML nghiệp vụ vào phần tử CommandData của BKAV.
/// XElement tự escape &amp; &lt; &gt; đúng quy tắc trong tài liệu; nháy đơn/nháy kép được
/// escape thêm khi bật option escapeQuotes để khớp bảng quy tắc của Bkav.
/// </summary>
public sealed class BkavCommandDataTransform : IPayloadTransform
{
    public ValueTask<byte[]> ForwardAsync(byte[] input, TransformContext context, CancellationToken ct)
    {
        var argName = context.OptionOrDefault("cmdTypeArg", "CmdType");
        var cmdType = context.Args.GetValueOrDefault(argName)
                      ?? throw new TvanConfigurationException(
                          $"Endpoint BKAV thiếu args.{argName} (mã lệnh CmdType).");

        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var commandObject = Encoding.UTF8.GetString(input);
        if (context.OptionOrDefault("escapeQuotes", "true") == "true")
            commandObject = commandObject.Replace("'", "&apos;").Replace("\"", "&quot;");

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("CommandData",
                new XAttribute(XNamespace.Xmlns + "xsd", xsd),
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XElement("CmdType", cmdType),
                new XElement("CommandObject", commandObject)));

        var xml = document.Declaration + document.ToString(SaveOptions.DisableFormatting);
        return ValueTask.FromResult(Encoding.UTF8.GetBytes(xml));
    }

    public ValueTask<byte[]> BackwardAsync(byte[] input, TransformContext context, CancellationToken ct)
    {
        try
        {
            var root = XDocument.Parse(Encoding.UTF8.GetString(input)).Root
                       ?? throw new TvanProtocolException("CommandData rỗng.");

            // Phải lấy đúng phần tử CommandObject: root.Value sẽ nối cả CmdType vào payload.
            var commandObject = root.Elements()
                                    .FirstOrDefault(x => x.Name.LocalName == "CommandObject")?.Value
                                ?? throw new TvanProtocolException("Không tìm thấy phần tử CommandObject.");

            if (context.OptionOrDefault("escapeQuotes", "true") == "true")
                commandObject = commandObject.Replace("&apos;", "'").Replace("&quot;", "\"");

            return ValueTask.FromResult(Encoding.UTF8.GetBytes(commandObject));
        }
        catch (Exception ex) when (ex is not TvanProtocolException)
        {
            throw new TvanProtocolException("Không bóc được CommandData từ phản hồi BKAV.", ex);
        }
    }
}
