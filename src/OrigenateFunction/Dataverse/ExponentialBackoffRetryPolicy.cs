using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;

namespace OrigenateFunction.Dataverse;

public sealed class ExponentialBackoffRetryPolicy : IHttpRetryPolicy
{
    private readonly ILogger<ExponentialBackoffRetryPolicy> _log;
    private readonly int _maxRetries;

    public ExponentialBackoffRetryPolicy(
        IOptions<OrigenateOptions> opts, ILogger<ExponentialBackoffRetryPolicy> log)
    {
        _log = log;
        _maxRetries = opts.Value.MaxRetries;
    }

    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpRequestMessage>> requestFactory,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);
        for (int attempt = 1; ; attempt++)
        {
            var req = await requestFactory(ct);
            HttpResponseMessage res;
            try { res = await send(req, ct); }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                _log.LogWarning(ex, "Transient HTTP failure (attempt {Attempt}); retrying in {Delay}s",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay = NextDelay(delay);
                continue;
            }

            if ((int)res.StatusCode != 429 && res.StatusCode != HttpStatusCode.ServiceUnavailable)
                return res;
            if (attempt >= _maxRetries) return res;

            var retryAfter = res.Headers.RetryAfter?.Delta ?? delay;
            res.Dispose();
            _log.LogWarning("Throttled HTTP {Status}; backing off {Sec}s (attempt {Attempt})",
                (int)res.StatusCode, retryAfter.TotalSeconds, attempt);
            await Task.Delay(retryAfter, ct);
            delay = NextDelay(delay);
        }
    }

    private static TimeSpan NextDelay(TimeSpan current)
        => TimeSpan.FromSeconds(Math.Min(60, current.TotalSeconds * 2));
}
