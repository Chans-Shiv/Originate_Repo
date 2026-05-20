namespace OrigenateFunction.Abstractions;

public interface IHttpRetryPolicy
{
    Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpRequestMessage>> requestFactory,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken ct);
}

public interface IDataverseTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}
