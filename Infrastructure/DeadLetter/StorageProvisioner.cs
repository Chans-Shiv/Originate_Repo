using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Configuration;

namespace OrigenateFunction.Infrastructure.DeadLetter;

// Runs once at host startup, BEFORE function listeners begin polling, and
// ensures the dead-letter queue + error-archive container exist. Without
// this, the DeadLetterProcessor QueueTrigger listener polls a missing queue
// every few seconds and logs 404 QueueNotFound until a producer happens to
// fire and lazily creates it — which drowns out every other log in the
// terminal during local dev.
//
// CreateIfNotExistsAsync is idempotent: it's a no-op once the resources
// already exist, so this is safe to run on every host start. Failures are
// logged but not thrown — we don't want a transient storage blip at startup
// to kill the entire host (and silence the BlobTrigger too).
public sealed class StorageProvisioner : IHostedService
{
    private readonly QueueServiceClient _queues;
    private readonly BlobServiceClient _blobs;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<StorageProvisioner> _log;

    public StorageProvisioner(
        QueueServiceClient queues,
        BlobServiceClient blobs,
        IOptions<OrigenateOptions> opts,
        ILogger<StorageProvisioner> log)
    {
        _queues = queues;
        _blobs = blobs;
        _opts = opts.Value;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await EnsureQueueAsync(_opts.DeadLetterQueueName, ct);
        await EnsureContainerAsync(_opts.ErrorArchiveContainerName, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task EnsureQueueAsync(string name, CancellationToken ct)
    {
        try
        {
            await _queues.GetQueueClient(name).CreateIfNotExistsAsync(cancellationToken: ct);
            _log.LogInformation("EventName=QueueEnsured Queue={Queue}", name);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "EventName=QueueEnsureFailed Queue={Queue} — listener will keep logging 404 until this succeeds. " +
                "Check Storage Queue Data Contributor role.", name);
        }
    }

    private async Task EnsureContainerAsync(string name, CancellationToken ct)
    {
        try
        {
            await _blobs.GetBlobContainerClient(name).CreateIfNotExistsAsync(cancellationToken: ct);
            _log.LogInformation("EventName=ContainerEnsured Container={Container}", name);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "EventName=ContainerEnsureFailed Container={Container} — give-up archive writes will fail. " +
                "Check Storage Blob Data Contributor role.", name);
        }
    }
}
