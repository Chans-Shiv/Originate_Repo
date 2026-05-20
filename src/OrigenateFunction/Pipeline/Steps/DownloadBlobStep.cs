using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class DownloadBlobStep : IPipelineStep
{
    public string Name => "Download blob to temp";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        if (!ctx.BlobName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Blob {ctx.BlobName} is not an .xlsx file.");

        ctx.TempXlsxPath = Path.Combine(Path.GetTempPath(), $"origenate-{Guid.NewGuid():N}.xlsx");
        await using var fs = File.Create(ctx.TempXlsxPath);
        await ctx.BlobStream.CopyToAsync(fs, ct);
    }
}
