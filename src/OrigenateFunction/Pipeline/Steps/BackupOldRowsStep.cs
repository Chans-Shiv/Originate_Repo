using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Mappers;
using OrigenateFunction.Models;
using OrigenateFunction.Options;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class BackupOldRowsStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    private readonly HoldingRepository _holding;
    private readonly EntityProjector _projector;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<BackupOldRowsStep> _log;

    public BackupOldRowsStep(
        StgOrigenateRepository stg, HoldingRepository holding,
        EntityProjector projector, IOptions<OrigenateOptions> opts,
        ILogger<BackupOldRowsStep> log)
    {
        _stg = stg; _holding = holding; _projector = projector; _opts = opts.Value; _log = log;
    }

    public string Name => $"Backup rows older than {_opts.AgeThresholdMonths}mo to HOLDING";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-_opts.AgeThresholdMonths);
        var filter = new FilterExpression();
        filter.Conditions.Add(new ConditionExpression("createdon", ConditionOperator.LessThan, cutoff));

        _log.LogInformation("EventName=BackupStart Cutoff={Cutoff:o} AgeMonths={Months} Parallel={Par}",
            cutoff, _opts.AgeThresholdMonths, _opts.MaxParallelBatches);

        using var sem = new SemaphoreSlim(_opts.MaxParallelBatches, _opts.MaxParallelBatches);
        var inFlight = new List<Task<int>>();
        var buffer = new List<Entity>(_opts.InsertBatchSize);
        long total = 0;
        int batchNum = 0;

        await foreach (var item in _stg.StreamAsync(filter, ColumnMap.StgBusinessFields, ct))
        {
            buffer.Add(_projector.Project(item, _holding.EntityLogicalName, ColumnMap.StgBusinessFields));
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
        var allFailures = await Task.WhenAll(inFlight);
        var totalFailures = allFailures.Sum();
        _log.LogInformation("EventName=Backup Total={Total} Batches={Batches} Failed={Failed}",
            total, batchNum, totalFailures);
    }

    private async Task<int> FlushAsync(Entity[] buffer, int batchNum, long runningTotal, SemaphoreSlim sem, CancellationToken ct)
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
            return failed;
        }
        finally { sem.Release(); }
    }
}
