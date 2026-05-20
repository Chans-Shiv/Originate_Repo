namespace OrigenateFunction.Abstractions;

public interface IDataverseGateway
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct);
    Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string relativePath, CancellationToken ct);
    Uri BaseAddress { get; }
}
