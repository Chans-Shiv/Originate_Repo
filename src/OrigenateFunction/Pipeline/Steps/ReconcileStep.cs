using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Dataverse;
using OrigenateFunction.Mappers;
using OrigenateFunction.Models;
using OrigenateFunction.Options;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class ReconcileStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    private readonly HoldingRepository _holding;
    private readonly JsonRowMapper _mapper;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<ReconcileStep> _log;

    public ReconcileStep(
        StgOrigenateRepository stg, HoldingRepository holding,
        JsonRowMapper mapper,
        IOptions<OrigenateOptions> opts, ILogger<ReconcileStep> log)
    {
        _stg = stg; _holding = holding; _mapper = mapper;
        _opts = opts.Value; _log = log;
    }

    public string Name => "Reconcile HOLDING → STG (re-insert orphans)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("Reconcile ▶ scanning HOLDING for orphan ApplicationNumbers");

        var holdingPage = new List<string>(_opts.PageSize);
        long reinserted = 0, totalFailures = 0;

        await foreach (var item in _holding.StreamAsync(null, new[] { ColumnMap.ApplicationNumberField }, ct))
        {
            var app = _mapper.ExtractString(item, ColumnMap.ApplicationNumberField);
            if (!string.IsNullOrEmpty(app)) holdingPage.Add(app);
            if (holdingPage.Count >= _opts.PageSize)
            {
                _log.LogInformation("Reconcile: processing HOLDING page of {Count} app#s", holdingPage.Count);
                var (ins, fail) = await ReinsertOrphansAsync(holdingPage, ct);
                reinserted += ins; totalFailures += fail;
                holdingPage.Clear();
            }
        }
        if (holdingPage.Count > 0)
        {
            _log.LogInformation("Reconcile: processing final HOLDING page of {Count} app#s", holdingPage.Count);
            var (ins, fail) = await ReinsertOrphansAsync(holdingPage, ct);
            reinserted += ins; totalFailures += fail;
        }

        _log.LogInformation("Reconcile ✓ re-inserted {Count} orphan rows into STG ({Failed} insert failures)",
            reinserted, totalFailures);
    }

    private async Task<(int Inserted, int Failed)> ReinsertOrphansAsync(List<string> holdingAppNumbers, CancellationToken ct)
    {
        const int chunkSize = 500;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < holdingAppNumbers.Count; i += chunkSize)
        {
            var slice = holdingAppNumbers.Skip(i).Take(chunkSize).ToArray();
            var filter = InFilterBuilder.Build(ColumnMap.ApplicationNumberField, slice);
            await foreach (var item in _stg.StreamAsync(filter, new[] { ColumnMap.ApplicationNumberField }, ct))
            {
                var s = _mapper.ExtractString(item, ColumnMap.ApplicationNumberField);
                if (s is not null) present.Add(s);
            }
        }

        var orphans = holdingAppNumbers.Where(a => !present.Contains(a)).ToArray();
        _log.LogInformation("Reconcile: page had {Holding} HOLDING rows, {Present} already in STG, {Orphan} orphans",
            holdingAppNumbers.Count, present.Count, orphans.Length);
        if (orphans.Length == 0) return (0, 0);

        int inserted = 0, failed = 0;
        for (int i = 0; i < orphans.Length; i += chunkSize)
        {
            var slice = orphans.Skip(i).Take(chunkSize).ToArray();
            var filter = InFilterBuilder.Build(ColumnMap.ApplicationNumberField, slice);
            var buffer = new List<IDictionary<string, object?>>();
            await foreach (var item in _holding.StreamAsync(filter, ColumnMap.StgBusinessFields, ct))
            {
                buffer.Add(_mapper.MapJsonToRecord(item, ColumnMap.StgBusinessFields));
                if (buffer.Count >= _opts.InsertBatchSize)
                {
                    var (ok, fail) = await FlushAsync(buffer, ct);
                    inserted += ok; failed += fail;
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                var (ok, fail) = await FlushAsync(buffer, ct);
                inserted += ok; failed += fail;
            }
        }
        return (inserted, failed);
    }

    private async Task<(int Ok, int Failed)> FlushAsync(List<IDictionary<string, object?>> buffer, CancellationToken ct)
    {
        var results = await _stg.InsertManyAsync(buffer, ct);
        int failed = 0; string? firstErr = null;
        foreach (var r in results)
        {
            if (!r.Success) { failed++; firstErr ??= r.Error; }
        }
        if (failed > 0)
            _log.LogError("Reconcile orphan-reinsert: {Failed}/{Total} failures. First error: {Err}",
                failed, buffer.Count, firstErr);
        return (buffer.Count - failed, failed);
    }
}
