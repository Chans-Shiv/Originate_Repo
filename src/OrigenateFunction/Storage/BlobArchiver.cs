using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Storage;

public sealed class BlobArchiver : IBlobArchiver
{
    private readonly ContainerClientFactory _containers;
    private readonly ILogger<BlobArchiver> _log;

    public BlobArchiver(ContainerClientFactory containers, ILogger<BlobArchiver> log)
    {
        _containers = containers;
        _log = log;
    }

    public async Task ArchiveAsync(string sourceBlobName, CancellationToken ct)
    {
        var src = _containers.Landing().GetBlobClient(sourceBlobName);
        var targetName = sourceBlobName;
        var dst = _containers.Archive().GetBlobClient(targetName);

        if (await dst.ExistsAsync(ct))
        {
            var stem = Path.GetFileNameWithoutExtension(sourceBlobName);
            var ext = Path.GetExtension(sourceBlobName);
            targetName = $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            dst = _containers.Archive().GetBlobClient(targetName);
        }

        var op = await dst.StartCopyFromUriAsync(src.Uri, cancellationToken: ct);
        await op.WaitForCompletionAsync(ct);

        var props = await dst.GetPropertiesAsync(cancellationToken: ct);
        if (props.Value.CopyStatus != CopyStatus.Success)
            throw new InvalidOperationException($"Archive copy failed: {props.Value.CopyStatusDescription}");

        await src.DeleteIfExistsAsync(cancellationToken: ct);
        _log.LogInformation("Archived {Source} → {Target}", sourceBlobName, targetName);
    }
}
