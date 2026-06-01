using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Fhn.Originate.FtbanknewSync.Configuration;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Storage;

// Factory pattern — single source of truth for resolving each named container.
public sealed class ContainerClientFactory
{
    private readonly BlobServiceClient _client;
    private readonly OrigenateOptions _opts;

    public ContainerClientFactory(BlobServiceClient client, IOptions<OrigenateOptions> opts)
    {
        _client = client; _opts = opts.Value;
    }

    public BlobContainerClient Landing() => _client.GetBlobContainerClient(_opts.MainContainerName);
    public BlobContainerClient Archive() => _client.GetBlobContainerClient(_opts.ArchiveContainerName);
    public BlobContainerClient Failed()  => _client.GetBlobContainerClient(_opts.FailedContainerName);
}
