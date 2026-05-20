using Azure.Core;
using Microsoft.Extensions.Configuration;

namespace OrigenateFunction.Services;

public sealed class DataverseTokenProvider
{
    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AccessToken? _cached;

    public DataverseTokenProvider(TokenCredential credential, IConfiguration cfg)
    {
        _credential = credential;
        var url = cfg["DataverseUrl"]
            ?? throw new InvalidOperationException("DataverseUrl is not configured.");
        _scope = url.TrimEnd('/') + "/.default";
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_cached is { } t && t.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            return t.Token;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is { } t2 && t2.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                return t2.Token;
            var ctx = new TokenRequestContext(new[] { _scope });
            _cached = await _credential.GetTokenAsync(ctx, ct);
            return _cached.Value.Token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
