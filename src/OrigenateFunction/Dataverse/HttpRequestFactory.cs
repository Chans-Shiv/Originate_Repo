using System.Net.Http.Headers;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class HttpRequestFactory
{
    private readonly IDataverseTokenProvider _tokens;
    public HttpRequestFactory(IDataverseTokenProvider tokens) => _tokens = tokens;

    public async Task<HttpRequestMessage> CreateAsync(HttpMethod method, string relativePath, CancellationToken ct)
    {
        var req = new HttpRequestMessage(method, relativePath);
        var token = await _tokens.GetTokenAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    public static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage src)
    {
        var clone = new HttpRequestMessage(src.Method, src.RequestUri);
        foreach (var h in src.Headers) clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (src.Content is not null)
        {
            var bytes = await src.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in src.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }
}
