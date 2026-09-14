using Platform.Infrastructure.Tvan.Model;

namespace Platform.Infrastructure.Tvan.Http;

/// <summary>
/// Kênh truyền dữ liệu giữa gateway và các DelegatingHandler.
/// Dùng HttpRequestMessage.Options thay vì AsyncLocal hay scoped state để handler
/// vẫn là hàm thuần: test được, an toàn khi chạy song song.
/// </summary>
public static class TvanRequestOptions
{
    public static readonly HttpRequestOptionsKey<TvanChannel> Channel = new("tvan.channel");
    public static readonly HttpRequestOptionsKey<Guid> TransactionId = new("tvan.transactionId");
    public static readonly HttpRequestOptionsKey<int> AttemptNo = new("tvan.attemptNo");
}

internal static class HttpRequestMessageExtensions
{
    /// <summary>Nhân bản request để retry — HttpRequestMessage chỉ gửi được một lần.</summary>
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage source, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri) { Version = source.Version };

        if (source.Content is not null)
        {
            var bytes = await source.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in source.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in (IDictionary<string, object?>)source.Options)
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;

        return clone;
    }
}
