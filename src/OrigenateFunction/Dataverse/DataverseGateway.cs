using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;

namespace OrigenateFunction.Dataverse;

// SRP: owns the HttpClient, knows the base URL, applies the retry policy + token.
public sealed class DataverseGateway : IDataverseGateway
{
    private readonly HttpClient _http;
    private readonly HttpRequestFactory _factory;
    private readonly IHttpRetryPolicy _retry;

    public DataverseGateway(
        HttpClient http,
        HttpRequestFactory factory,
        IHttpRetryPolicy retry,
        IOptions<OrigenateOptions> opts)
    {
        _factory = factory;
        _retry = retry;
        var baseUrl = (opts.Value.DataverseUrl ?? throw new InvalidOperationException("DataverseUrl missing"))
            .TrimEnd('/') + "/api/data/v9.2/";
        http.BaseAddress = new Uri(baseUrl);
        http.Timeout = TimeSpan.FromMinutes(5);
        http.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        http.DefaultRequestHeaders.Add("OData-Version", "4.0");
        _http = http;
    }

    public Uri BaseAddress => _http.BaseAddress!;

    public Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string path, CancellationToken ct)
        => _factory.CreateAsync(method, path, ct);

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        return await _retry.ExecuteAsync(
            requestFactory: async _ => await HttpRequestFactory.CloneAsync(request),
            send: (req, c) => _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, c),
            ct: ct);
    }
}
