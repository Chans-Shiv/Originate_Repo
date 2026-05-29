using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Diagnostics;
using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.DeadLetter;

// Durable archive for records that exhausted all queue-retry attempts.
//
// One blob per record at: errors/yyyy/MM/dd/{invocationId}-{row}.json
//   - Date partition: makes "what failed yesterday" a prefix listing
//   - One blob per record: no concurrent-write conflicts, no append-blob block
//     limit (50K), and no need to re-upload a file to remove an entry
//   - Stable filename: a repeated failure overwrites cleanly so the operator
//     always sees the most recent attempt's error
//
// Container is created on first use. Identity-based auth (DefaultAzureCredential)
// uses the same TokenCredential the rest of the app shares — one role grant
// covers all storage clients.
public sealed class ErrorArchiveBlobWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly BlobContainerClient _container;
    private readonly ILogger<ErrorArchiveBlobWriter> _log;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _containerEnsured;

    public ErrorArchiveBlobWriter(BlobContainerClient container, ILogger<ErrorArchiveBlobWriter> log)
    {
        _container = container;
        _log = log;
    }

    public async Task WriteAsync(DeadLetterMessage msg, Exception giveUpError, CancellationToken ct)
    {
        await EnsureContainerAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var blobName = $"errors/{now:yyyy}/{now:MM}/{now:dd}/{msg.InvocationId}-{msg.RowNumber}.json";

        // Wrap the queue payload with the final exception details so the archived
        // blob is self-contained for triage. ToString() carries the full stack +
        // inner exceptions; .Message alone hides the root cause.
        var payload = new
        {
            msg.SourceBlobName,
            msg.InvocationId,
            msg.StepName,
            msg.RowNumber,
            msg.AccountNumber,
            msg.LoanApplicationId,
            OriginalError = msg.ErrorMessage,
            msg.EnqueuedAt,
            GivenUpAt = now,
            FinalAttemptError = giveUpError.ToString(),
            ExceptionType = giveUpError.GetType().FullName,
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var blob = _container.GetBlobClient(blobName);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _log.LogInformation(
            "EventName={EventName} BlobName={BlobName} SourceBlob={SourceBlob} Step={Step} Row={Row}",
            LogEvents.FailureArchived, blobName, msg.SourceBlobName, msg.StepName, msg.RowNumber);
    }

    // Only flip _containerEnsured after a successful create — a transient failure
    // (RBAC propagation, network blip) shouldn't permanently poison subsequent
    // writes by skipping the create + uploading to a missing container.
    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured) return;
        await _ensureLock.WaitAsync(ct);
        try
        {
            if (_containerEnsured) return;
            await _container.CreateIfNotExistsAsync(cancellationToken: ct);
            _containerEnsured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
