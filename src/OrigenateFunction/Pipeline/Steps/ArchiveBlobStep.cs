using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class ArchiveBlobStep : IPipelineStep
{
    private readonly IBlobArchiver _archiver;
    public ArchiveBlobStep(IBlobArchiver archiver) => _archiver = archiver;
    public string Name => "Archive Excel to originate-processed";
    public Task ExecuteAsync(PipelineContext ctx, CancellationToken ct) => _archiver.ArchiveAsync(ctx.BlobName, ct);
}
