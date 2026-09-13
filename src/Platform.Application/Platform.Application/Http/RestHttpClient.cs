namespace Platform.Application.Http;

public class RestHttpClient(IHttpClientFactory httpClientFactory) : IRestHttpClient
{
    public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct)
    {
        var client = CreateClient();
        return await client.GetAsync(url, ct);
    }

    public async Task<HttpResponseMessage> PostAsync(string url, StringContent content, CancellationToken ct = default)
    {
        var client = CreateClient();
        return await client.PostAsync(url, content, ct);
    }

    public async Task<HttpResponseMessage> PostFormAsync(string url, FormUrlEncodedContent content, CancellationToken ct = default)
    {
        var client = CreateClient();
        return await client.PostAsync(url, content, ct);
    }

    public async Task<HttpResponseMessage> PostWithBearerAsync(string url, StringContent content, string bearerToken, CancellationToken ct = default)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        return await client.PostAsync(url, content, ct);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(3);
        return client;
    }
}
