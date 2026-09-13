namespace Platform.Application.Http;

public interface IRestHttpClient
{
    Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct);
    Task<HttpResponseMessage> PostAsync(string url, StringContent content, CancellationToken ct = default);
    Task<HttpResponseMessage> PostFormAsync(string url, FormUrlEncodedContent content, CancellationToken ct = default);
    Task<HttpResponseMessage> PostWithBearerAsync(string url, StringContent content, string bearerToken, CancellationToken ct = default);
}
