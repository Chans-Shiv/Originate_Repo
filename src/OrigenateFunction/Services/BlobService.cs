using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrigenateFunction.Services;

public sealed class BlobService
{
    private readonly BlobServiceClient _client;
    private readonly ILogger<BlobService> _log;
    public string LandingContainer { get; }
    public string ArchiveContainer { get; }
    public string FailedContainer { get; }

    public BlobService(BlobServiceClient client, IConfiguration cfg, ILogger<BlobService> log)
    {
        _client = client;
        _log = log;
        LandingContainer = cfg["MainContainerName"] ?? "originate-landing";
        ArchiveContainer = cfg["ArchiveContainerName"] ?? "originate-processed";
        FailedContainer = cfg["FailedContainerName"] ?? "originate-failed";
    }

    public BlobClient Landing(string name) => _client.GetBlobContainerClient(LandingContainer).GetBlobClient(name);
    public BlobClient Archive(string name) => _client.GetBlobContainerClient(ArchiveContainer).GetBlobClient(name);
    public BlobClient Failed(string name) => _client.GetBlobContainerClient(FailedContainer).GetBlobClient(name);

    public async Task ArchiveAsync(string sourceName, CancellationToken ct)
    {
        var src = Landing(sourceName);
        var targetName = sourceName;
        var dst = Archive(targetName);

        if (await dst.ExistsAsync(ct))
        {
            var stem = Path.GetFileNameWithoutExtension(sourceName);
            var ext = Path.GetExtension(sourceName);
            targetName = $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            dst = Archive(targetName);
        }

        var op = await dst.StartCopyFromUriAsync(src.Uri, cancellationToken: ct);
        await op.WaitForCompletionAsync(ct);

        var props = await dst.GetPropertiesAsync(cancellationToken: ct);
        if (props.Value.CopyStatus != CopyStatus.Success)
            throw new InvalidOperationException($"Archive copy failed: {props.Value.CopyStatusDescription}");

        await src.DeleteIfExistsAsync(cancellationToken: ct);
        _log.LogInformation("Archived {Source} → {Target}", sourceName, targetName);
    }

    public async Task MoveToFailedAsync(string sourceName, CancellationToken ct)
    {
        var src = Landing(sourceName);
        var dst = Failed(sourceName);
        var op = await dst.StartCopyFromUriAsync(src.Uri, cancellationToken: ct);
        await op.WaitForCompletionAsync(ct);
        await src.DeleteIfExistsAsync(cancellationToken: ct);
        _log.LogWarning("Moved {Source} to failed container", sourceName);
    }

    public async Task UploadFailureFileAsync(string name, Stream content, string contentType, CancellationToken ct)
    {
        var dst = Failed(name);
        await dst.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
        _log.LogInformation("Uploaded failure file {Name} ({Bytes} bytes)", name, content.Length);
    }
}
