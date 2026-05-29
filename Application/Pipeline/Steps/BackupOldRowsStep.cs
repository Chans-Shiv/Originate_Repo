using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;
using OrigenateFunction.Infrastructure.Dataverse;
using OrigenateFunction.Domain.Entities;
using OrigenateFunction.Configuration;
using OrigenateFunction.Infrastructure.Repositories;

namespace OrigenateFunction.Application.Pipeline.Steps;

public sealed class BackupOldRowsStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    private readonly HoldingRepository _holding;
    private readonly EntityBuilder _builder;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<BackupOldRowsStep> _log;

    public BackupOldRowsStep(
        StgOrigenateRepository stg, HoldingRepository holding,
        EntityBuilder builder, IOptions<OrigenateOptions> opts,
        ILogger<BackupOldRowsStep> log)
    {
        _stg = stg; _holding = holding; _builder = builder; _opts = opts.Value; _log = log;
    }

    public string Name => $"Backup rows older than {_opts.AgeThresholdMonths}mo to HOLDING";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-_opts.AgeThresholdMonths);
        // "13 months or more old" → createdon ≤ (now − 13 months)
        var filter = new FilterExpression();
        filter.Conditions.Add(new ConditionExpression("createdon", ConditionOperator.LessEqual, cutoff));

        _log.LogInformation("EventName=BackupStart Cutoff={Cutoff:o} AgeMonths={Months} Parallel={Par}",
            cutoff, _opts.AgeThresholdMonths, _opts.MaxParallelBatches);

        // Pre-fetch HOLDING schema once so each row can be re-coerced to HOLDING's
        // column types (STG and HOLDING share business fields but can differ in type).
        var holdingAttrs = await _builder.GetAttributesAsync(_holding.EntityLogicalName, ct);

        using var sem = new SemaphoreSlim(_opts.MaxParallelBatches, _opts.MaxParallelBatches);
        var inFlight = new List<Task<BatchOutcome>>();
        var buffer = new List<Entity>(_opts.InsertBatchSize);
        long total = 0;
        int batchNum = 0;

        await foreach (var item in _stg.StreamAsync(filter, ColumnMap.StgBusinessFields, ct))
        {
            buffer.Add(_builder.ProjectCoerced(
                item,
                _holding.EntityLogicalName,
                holdingAttrs,
                ColumnMap.StgBusinessFields,
                ColumnMap.StgToHoldingField));
            if (buffer.Count >= _opts.InsertBatchSize)
            {
                batchNum++;
                total += buffer.Count;
                inFlight.Add(FlushAsync(buffer.ToArray(), batchNum, total, sem, ct));
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            batchNum++;
            total += buffer.Count;
            inFlight.Add(FlushAsync(buffer.ToArray(), batchNum, total, sem, ct));
        }
        var batchResults = await Task.WhenAll(inFlight);
        var totalFailures = batchResults.Sum(r => r.Failed);
        var firstError = batchResults.Select(r => r.FirstError).FirstOrDefault(e => e is not null);

        _log.LogInformation("EventName=Backup Total={Total} Batches={Batches} Failed={Failed}",
            total, batchNum, totalFailures);

        // CRITICAL: if even one row failed to reach HOLDING, abort the whole pipeline.
        // The next step (TruncateStgStep) wipes STG — letting it run after an
        // incomplete backup would lose the un-backed-up rows permanently. Throwing
        // here stops the pipeline before truncate, leaves STG intact, and moves the
        // blob to the failed container for retry. ClearHoldingStep runs first and is
        // idempotent, so a retry cleanly re-clears the partial backup and starts over.
        if (totalFailures > 0)
            throw new InvalidOperationException(
                $"HOLDING backup incomplete: {totalFailures} of {total} row(s) failed to insert. " +
                $"Aborting before STG truncate to prevent data loss. First error: {firstError ?? "(none)"}");
    }

    private async Task<BatchOutcome> FlushAsync(Entity[] buffer, int batchNum, long runningTotal, SemaphoreSlim sem, CancellationToken ct)
    {
        await sem.WaitAsync(ct);
        try
        {
            _log.LogInformation("EventName=BackupBatchStart Batch={Batch} Rows={Rows} Running={Total}",
                batchNum, buffer.Length, runningTotal);
            var results = await _holding.InsertManyAsync(buffer, ct);
            var failed = 0;
            string? firstErr = null;
            foreach (var r in results)
            {
                if (!r.Success) { failed++; firstErr ??= r.Error; }
            }
            if (failed > 0)
                _log.LogError("EventName=BackupBatchPartial Batch={Batch} Failed={Failed}/{Total} FirstError={Err}",
                    batchNum, failed, buffer.Length, firstErr);
            else
                _log.LogInformation("EventName=BackupBatch Batch={Batch} Rows={Count} Failed=0", batchNum, buffer.Length);
            return new BatchOutcome(failed, firstErr);
        }
        finally { sem.Release(); }
    }

    private readonly record struct BatchOutcome(int Failed, string? FirstError);
}
