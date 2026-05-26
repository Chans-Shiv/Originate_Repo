using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client;
using OrigenateFunction.Options;

namespace OrigenateFunction.Dataverse;

// Owns the ServiceClient + a cached AAD token with single-flight refresh.
// Without caching, every Dataverse call re-runs DefaultAzureCredential — under
// concurrent insert load this triggers MSAL contention and intermittent failures.
public sealed class DataverseConnectionFactory : IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly string _dataverseUrl;
    private readonly ILogger<DataverseConnectionFactory> _log;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private AccessToken? _cachedToken;
    private ServiceClient? _client;

    public DataverseConnectionFactory(
        TokenCredential credential,
        IOptions<OrigenateOptions> opts,
        ILogger<DataverseConnectionFactory> log)
    {
        _credential = credential;
        _log = log;
        _dataverseUrl = opts.Value.DataverseUrl?.TrimEnd('/')
            ?? throw new InvalidOperationException("DataverseUrl is not configured.");
        _scope = _dataverseUrl + "/.default";
    }

    public ServiceClient Client
    {
        get
        {
            if (_client is not null) return _client;
            lock (_tokenLock)
            {
                if (_client is not null) return _client;

                _log.LogInformation("EventName=DataverseConnect Url={Url}", _dataverseUrl);

                var client = new ServiceClient(
                    instanceUrl: new Uri(_dataverseUrl),
                    tokenProviderFunction: _ => GetCachedTokenAsync(),
                    useUniqueInstance: true);

                if (!client.IsReady)
                    throw new InvalidOperationException(
                        $"ServiceClient is not ready: {client.LastError ?? "(no error message)"}");

                client.EnableAffinityCookie = true;
                client.MaxRetryCount = 3;
                _client = client;
            }
            return _client;
        }
    }

    private async Task<string> GetCachedTokenAsync()
    {
        var snapshot = _cachedToken;
        if (snapshot.HasValue && snapshot.Value.ExpiresOn - DateTimeOffset.UtcNow > RefreshSkew)
            return snapshot.Value.Token;

        await _tokenLock.WaitAsync();
        try
        {
            snapshot = _cachedToken;
            if (snapshot.HasValue && snapshot.Value.ExpiresOn - DateTimeOffset.UtcNow > RefreshSkew)
                return snapshot.Value.Token;

            _log.LogInformation("EventName=TokenRefresh Scope={Scope}", _scope);
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { _scope }), default);
            _cachedToken = token;
            _log.LogInformation("EventName=TokenRefreshed ExpiresOn={Expires}", token.ExpiresOn);
            return token.Token;
        }
        finally { _tokenLock.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _tokenLock.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _tokenLock.Dispose();
    }
}
