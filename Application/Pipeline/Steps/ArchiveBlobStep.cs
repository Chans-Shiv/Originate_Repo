using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Application.Pipeline.Steps;

public sealed class ArchiveBlobStep : IPipelineStep
{
    private readonly IBlobArchiver _archiver;
    private readonly ILogger<ArchiveBlobStep> _log;

    public ArchiveBlobStep(IBlobArchiver archiver, ILogger<ArchiveBlobStep> log)
    {
        _archiver = archiver; _log = log;
    }

    public string Name => "Archive Excel to originate-processed";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("ArchiveBlob ▶ {Blob}", ctx.BlobName);
        await _archiver.ArchiveAsync(ctx.BlobName, ct);
        _log.LogInformation("ArchiveBlob ✓ {Blob} moved to processed container", ctx.BlobName);
    }
}
