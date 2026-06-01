using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fhn.Originate.FtbanknewSync.Configuration;
using Fhn.Originate.FtbanknewSync.Diagnostics;
using Fhn.Originate.FtbanknewSync.Domain.Models;
using Fhn.Originate.FtbanknewSync.Infrastructure.DeadLetter;

namespace Fhn.Originate.FtbanknewSync.Functions;

// Drains the dead-letter Storage Queue into the Dataverse error table.
//
// One queue message = one failed Excel row. Retry semantics:
//   - Attempts 1..(MaxAttempts-1): rethrow on failure so the runtime requeues
//   - Final attempt (dequeueCount >= MaxAttempts): catch, archive the record
//     to blob + App Insights, and return normally so the runtime deletes the
//     message. Nothing should ever land in the {queue}-poison sibling queue
//     because we explicitly handle the give-up case ourselves.
//
// MaxAttempts comes from extensions.queues.maxDequeueCount in host.json — read
// at startup by Program.cs and stored on OrigenateOptions.DeadLetterMaxAttempts.
// Single source of truth; the runtime and our own code agree.
public sealed class DeadLetterProcessorFunction
{
    private readonly DataverseErrorTableService _writer;
    private readonly ErrorArchiveBlobWriter _archive;
    private readonly int _maxAttempts;
    private readonly ILogger<DeadLetterProcessorFunction> _log;

    public DeadLetterProcessorFunction(
        DataverseErrorTableService writer,
        ErrorArchiveBlobWriter archive,
        IOptions<OrigenateOptions> opts,
        ILogger<DeadLetterProcessorFunction> log)
    {
        _writer = writer;
        _archive = archive;
        _maxAttempts = opts.Value.DeadLetterMaxAttempts;
        _log = log;
    }

    [Function("DeadLetterProcessor")]
    public async Task RunAsync(
        [QueueTrigger("%DeadLetterQueueName%", Connection = "OrigenateStorage")] DeadLetterMessage msg,
        int dequeueCount,
        CancellationToken ct)
    {
        _log.LogInformation(
            "EventName={EventName} Blob={Blob} Step={Step} Row={Row} Account={Account} DequeueCount={DequeueCount}",
            LogEvents.DeadLetterDequeued, msg.SourceBlobName, msg.StepName, msg.RowNumber,
            msg.AccountNumber ?? "", dequeueCount);

        try
        {
            await _writer.WriteOneAsync(msg, ct);
        }
        catch (Exception ex) when (dequeueCount >= _maxAttempts)
        {
            // Final attempt — persist a durable artifact and ACK the message.
            // Wrapped in its own try/catch so a blob-write failure can't escape
            // and put us in an infinite requeue loop.
            _log.LogError(ex,
                "EventName={EventName} Blob={Blob} Step={Step} Row={Row} DequeueCount={DequeueCount} Error={Error}",
                LogEvents.DeadLetterGivenUp, msg.SourceBlobName, msg.StepName, msg.RowNumber,
                dequeueCount, ex.Message);

            try
            {
                await _archive.WriteAsync(msg, ex, ct);
            }
            catch (Exception archiveEx)
            {
                // Absolute last-ditch — App Insights is our only remaining sink.
                // We deliberately do NOT rethrow; rethrowing here would consume a
                // dequeue attempt without a successful archive, and the next
                // delivery would loop straight back into this same branch.
                _log.LogCritical(archiveEx,
                    "EventName={EventName} Blob={Blob} Step={Step} Row={Row} OriginalError={OriginalError}",
                    LogEvents.FailureArchiveWriteFailed, msg.SourceBlobName, msg.StepName, msg.RowNumber,
                    msg.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Not the final attempt — log and rethrow so the runtime requeues.
            _log.LogWarning(ex,
                "EventName={EventName} Blob={Blob} Step={Step} Row={Row} DequeueCount={DequeueCount} Error={Error}",
                LogEvents.ErrorTableWriteFailed, msg.SourceBlobName, msg.StepName, msg.RowNumber,
                dequeueCount, ex.Message);
            throw;
        }
    }
}
