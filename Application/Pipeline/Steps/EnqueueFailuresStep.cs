using Microsoft.Extensions.Logging;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;

namespace OrigenateFunction.Application.Pipeline.Steps;

// Pushes every PipelineContext.FailedRow onto the dead-letter Storage Queue, one
// message per row. Runs AFTER UploadFailuresStep (so the per-blob CSV is still
// the first artifact written) and BEFORE ArchiveBlobStep (so the queue captures
// the failures even if archive somehow fails later).
//
// StepName "Pipeline" — we don't know which earlier step produced each failure
// here (could be LoadExcelStep, InsertExceptionsStep, ReconcileStep). Future:
// stamp step name on FailedRow at the producer to get finer-grained attribution.
public sealed class EnqueueFailuresStep : IPipelineStep
{
    private readonly IDeadLetterService _dlq;
    private readonly ILogger<EnqueueFailuresStep> _log;

    public EnqueueFailuresStep(IDeadLetterService dlq, ILogger<EnqueueFailuresStep> log)
    {
        _dlq = dlq; _log = log;
    }

    public string Name => "Enqueue per-row failures to dead-letter queue (if any)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        if (ctx.FailedRows.Count == 0)
        {
            _log.LogInformation("No row failures to enqueue");
            return;
        }

        _log.LogWarning("Enqueuing {Count} per-row failures to dead-letter queue", ctx.FailedRows.Count);
        await _dlq.WriteAsync(
            sourceBlobName: ctx.BlobName,
            invocationId: ctx.InvocationId,
            stepName: "Pipeline",
            failures: ctx.FailedRows,
            ct: ct);
    }
}
