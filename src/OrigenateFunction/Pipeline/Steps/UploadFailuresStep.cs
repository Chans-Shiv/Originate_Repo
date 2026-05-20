using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class UploadFailuresStep : IPipelineStep
{
    private readonly IFailureFileWriter _writer;
    private readonly ILogger<UploadFailuresStep> _log;

    public UploadFailuresStep(IFailureFileWriter writer, ILogger<UploadFailuresStep> log)
    {
        _writer = writer; _log = log;
    }

    public string Name => "Upload per-row failure CSV (if any)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        if (ctx.FailedRows.Count == 0) { _log.LogInformation("No row failures"); return; }
        _log.LogWarning("{Count} per-row failures; writing CSV", ctx.FailedRows.Count);
        await _writer.WriteAsync(ctx.BlobName, ctx.FailedRows, ct);
    }
}
