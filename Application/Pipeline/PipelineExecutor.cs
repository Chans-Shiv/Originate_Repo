using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Application.Pipeline;

// Strategy pattern: orchestrator iterates over IPipelineStep — closed for modification.
public sealed class PipelineExecutor
{
    private readonly IReadOnlyList<IPipelineStep> _steps;
    private readonly IFailedBlobMover _failedMover;
    private readonly ILogger<PipelineExecutor> _log;

    public PipelineExecutor(
        IEnumerable<IPipelineStep> steps,
        IFailedBlobMover failedMover,
        ILogger<PipelineExecutor> log)
    {
        _steps = steps.ToArray();
        _failedMover = failedMover;
        _log = log;
    }

    public async Task RunAsync(PipelineContext ctx, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        _log.LogInformation("Pipeline starting for {Blob} ({Steps} steps)", ctx.BlobName, _steps.Count);

        try
        {
            foreach (var step in _steps)
            {
                var sw = Stopwatch.StartNew();
                _log.LogInformation("Step ▶ {Step}", step.Name);
                await step.ExecuteAsync(ctx, ct);
                _log.LogInformation("Step ✓ {Step} in {Elapsed}", step.Name, sw.Elapsed);
            }
            _log.LogInformation("Pipeline complete for {Blob} in {Elapsed}", ctx.BlobName, total.Elapsed);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pipeline failed for {Blob}; moving to failed container", ctx.BlobName);
            try { await _failedMover.MoveToFailedAsync(ctx.BlobName, ct); }
            catch (Exception moveEx) { _log.LogError(moveEx, "Could not move blob to failed container"); }
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(ctx.TempXlsxPath))
            {
                try { if (File.Exists(ctx.TempXlsxPath)) File.Delete(ctx.TempXlsxPath); }
                catch { /* best-effort */ }
            }
        }
    }
}
