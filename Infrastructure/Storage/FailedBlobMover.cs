using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Storage;

public sealed class FailedBlobMover : IFailedBlobMover
{
    private readonly ContainerClientFactory _containers;
    private readonly ILogger<FailedBlobMover> _log;

    public FailedBlobMover(ContainerClientFactory containers, ILogger<FailedBlobMover> log)
    {
        _containers = containers;
        _log = log;
    }

    public async Task MoveToFailedAsync(string sourceBlobName, CancellationToken ct)
    {
        var src = _containers.Landing().GetBlobClient(sourceBlobName);
        var dst = _containers.Failed().GetBlobClient(sourceBlobName);
        var op = await dst.StartCopyFromUriAsync(src.Uri, cancellationToken: ct);
        await op.WaitForCompletionAsync(ct);
        await src.DeleteIfExistsAsync(cancellationToken: ct);
        _log.LogWarning("Moved {Source} to failed container", sourceBlobName);
    }
}
