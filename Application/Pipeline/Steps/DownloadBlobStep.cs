using Microsoft.Extensions.Logging;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;

namespace OrigenateFunction.Application.Pipeline.Steps;

public sealed class DownloadBlobStep : IPipelineStep
{
    private readonly ILogger<DownloadBlobStep> _log;

    public DownloadBlobStep(ILogger<DownloadBlobStep> log) => _log = log;

    public string Name => "Download blob to temp";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        if (!ctx.BlobName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Blob {ctx.BlobName} is not an .xlsx file.");

        ctx.TempXlsxPath = Path.Combine(Path.GetTempPath(), $"origenate-{Guid.NewGuid():N}.xlsx");
        _log.LogInformation("DownloadBlob ▶ {Blob} → {TempPath}", ctx.BlobName, ctx.TempXlsxPath);

        await using var fs = File.Create(ctx.TempXlsxPath);
        await ctx.BlobStream.CopyToAsync(fs, ct);
        await fs.FlushAsync(ct);

        var bytes = new FileInfo(ctx.TempXlsxPath).Length;
        _log.LogInformation("DownloadBlob ✓ {Blob} ({Bytes} bytes)", ctx.BlobName, bytes);
    }
}
