using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Configuration;
using Polly;
using Polly.Retry;

namespace OrigenateFunction.Infrastructure.Dataverse;

// Polly resilience pipeline tuned for Dataverse throttling (429) and Server Busy (503).
// Wrap any Dataverse SDK call with this — it catches the typical transient classes
// the SDK exposes via exception messages.
public sealed class DataverseResiliencePipeline
{
    public ResiliencePipeline Pipeline { get; }

    public DataverseResiliencePipeline(
        IOptions<OrigenateOptions> opts,
        ILogger<DataverseResiliencePipeline> log)
    {
        var maxRetries = Math.Max(1, opts.Value.MaxRetries);
        Pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(5),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                OnRetry = args =>
                {
                    log.LogWarning(
                        "EventName=DataverseRetry Attempt={Attempt} Delay={Delay}s Exception={Ex}",
                        args.AttemptNumber, args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "(none)");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private static bool IsTransient(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("429", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("503", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Server Busy", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("transient", StringComparison.OrdinalIgnoreCase);
    }
}
