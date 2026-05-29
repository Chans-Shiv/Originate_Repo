using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;
using OrigenateFunction.Infrastructure.Dataverse;
using OrigenateFunction.Domain.Entities;
using OrigenateFunction.Configuration;
using OrigenateFunction.Infrastructure.Repositories;

namespace OrigenateFunction.Application.Pipeline.Steps;

public sealed class ReconcileStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    private readonly HoldingRepository _holding;
    private readonly EntityProjector _projector;
    private readonly EntityBuilder _builder;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<ReconcileStep> _log;

    public ReconcileStep(
        StgOrigenateRepository stg, HoldingRepository holding,
        EntityProjector projector, EntityBuilder builder,
        IOptions<OrigenateOptions> opts, ILogger<ReconcileStep> log)
    {
        _stg = stg; _holding = holding; _projector = projector; _builder = builder;
        _opts = opts.Value; _log = log;
    }

    public string Name => "Reconcile HOLDING → STG (re-insert orphans)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("EventName=ReconcileStart");

        var holdingPage = new List<string>(_opts.PageSize);
        long reinserted = 0, totalFailures = 0;

        await foreach (var item in _holding.StreamAsync(null, new[] { ColumnMap.ApplicationNumberField }, ct))
        {
            var app = _projector.ExtractString(item, ColumnMap.ApplicationNumberField);
            if (!string.IsNullOrEmpty(app)) holdingPage.Add(app);
            if (holdingPage.Count >= _opts.PageSize)
            {
                _log.LogInformation("EventName=ReconcilePage Count={Count}", holdingPage.Count);
                var (ins, fail) = await ReinsertOrphansAsync(holdingPage, ct);
                reinserted += ins; totalFailures += fail;
                holdingPage.Clear();
            }
        }
        if (holdingPage.Count > 0)
        {
            _log.LogInformation("EventName=ReconcilePage Count={Count}", holdingPage.Count);
            var (ins, fail) = await ReinsertOrphansAsync(holdingPage, ct);
            reinserted += ins; totalFailures += fail;
        }

        _log.LogInformation("EventName=Reconcile Reinserted={Count} Failed={Failed}", reinserted, totalFailures);
    }

    private async Task<(int Inserted, int Failed)> ReinsertOrphansAsync(List<string> holdingAppNumbers, CancellationToken ct)
    {
        const int chunkSize = 500;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pre-fetch STG schema once so HOLDING rows can be re-coerced to STG column
        // types on the way back (the two tables can type the same business field
        // differently). Cached for the host lifetime, so re-fetching per page is cheap.
        var stgAttrs = await _builder.GetAttributesAsync(_stg.EntityLogicalName, ct);

        for (int i = 0; i < holdingAppNumbers.Count; i += chunkSize)
        {
            var slice = holdingAppNumbers.Skip(i).Take(chunkSize).ToArray();
            var filter = InFilterBuilder.Build(ColumnMap.ApplicationNumberField, slice);
            await foreach (var item in _stg.StreamAsync(filter, new[] { ColumnMap.ApplicationNumberField }, ct))
            {
                var s = _projector.ExtractString(item, ColumnMap.ApplicationNumberField);
                if (s is not null) present.Add(s);
            }
        }

        var orphans = holdingAppNumbers.Where(a => !present.Contains(a)).ToArray();
        _log.LogInformation("EventName=ReconcileOrphans Holding={Holding} InStg={Present} Orphans={Orphan}",
            holdingAppNumbers.Count, present.Count, orphans.Length);
        if (orphans.Length == 0) return (0, 0);

        int inserted = 0, failed = 0;
        for (int i = 0; i < orphans.Length; i += chunkSize)
        {
            var slice = orphans.Skip(i).Take(chunkSize).ToArray();
            var filter = InFilterBuilder.Build(ColumnMap.ApplicationNumberField, slice);
            var buffer = new List<Entity>();
            await foreach (var item in _holding.StreamAsync(filter, ColumnMap.HoldingBusinessFields, ct))
            {
                // Reading HOLDING (its own logical names) → writing STG: translate field
                // names back via HoldingToStgField so the renamed primary/secondary/CLTV
                // columns land in their STG equivalents.
                buffer.Add(_builder.ProjectCoerced(
                    item,
                    _stg.EntityLogicalName,
                    stgAttrs,
                    ColumnMap.HoldingBusinessFields,
                    ColumnMap.HoldingToStgField));
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

    private async Task<(int Ok, int Failed)> FlushAsync(List<Entity> buffer, CancellationToken ct)
    {
        var results = await _stg.InsertManyAsync(buffer, ct);
        int failed = 0; string? firstErr = null;
        foreach (var r in results)
        {
            if (!r.Success) { failed++; firstErr ??= r.Error; }
        }
        if (failed > 0)
            _log.LogError("EventName=ReconcileBatchPartial Failed={Failed}/{Total} FirstError={Err}",
                failed, buffer.Count, firstErr);
        return (buffer.Count - failed, failed);
    }
}
