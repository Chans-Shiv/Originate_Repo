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
        var holdingPage = new List<string>(_opts.PageSize);
        long reinserted = 0;

        await foreach (var item in _holding.StreamAsync(null, new[] { ColumnMap.ApplicationNumberField }, ct))
        {
            var app = _mapper.ExtractString(item, ColumnMap.ApplicationNumberField);
            if (!string.IsNullOrEmpty(app)) holdingPage.Add(app);
            if (holdingPage.Count >= _opts.PageSize)
            {
                reinserted += await ReinsertOrphansAsync(holdingPage, ct);
                holdingPage.Clear();
            }
        }
        if (holdingPage.Count > 0)
            reinserted += await ReinsertOrphansAsync(holdingPage, ct);

        _log.LogInformation("Re-inserted {Count} orphan rows into STG", reinserted);
    }

    private async Task<int> ReinsertOrphansAsync(List<string> holdingAppNumbers, CancellationToken ct)
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
        if (orphans.Length == 0) return 0;

        int inserted = 0;
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
                    inserted += buffer.Count;
                    await _stg.InsertManyAsync(buffer, ct);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                inserted += buffer.Count;
                await _stg.InsertManyAsync(buffer, ct);
            }
        }
        return inserted;
    }
}
