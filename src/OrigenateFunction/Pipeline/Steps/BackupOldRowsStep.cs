using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly JsonRowMapper _mapper;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<BackupOldRowsStep> _log;

    public BackupOldRowsStep(
        StgOrigenateRepository stg, HoldingRepository holding,
        JsonRowMapper mapper, IOptions<OrigenateOptions> opts,
        ILogger<BackupOldRowsStep> log)
    {
        _stg = stg; _holding = holding; _mapper = mapper; _opts = opts.Value; _log = log;
    }

    public string Name => $"Backup rows older than {ColumnMap.StgBusinessFields.Count} fields × {13}mo to HOLDING";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-_opts.AgeThresholdMonths).ToString("o");
        var filter = $"createdon lt {cutoff}";

        _log.LogInformation("BackupOldRows ▶ filter='{Filter}' (age threshold={Months} months)",
            filter, _opts.AgeThresholdMonths);

        var buffer = new List<IDictionary<string, object?>>(_opts.InsertBatchSize);
        long total = 0;
        int batchNum = 0, totalFailures = 0;

        await foreach (var item in _stg.StreamAsync(filter, ColumnMap.StgBusinessFields, ct))
        {
            buffer.Add(_mapper.MapJsonToRecord(item, ColumnMap.StgBusinessFields));
            if (buffer.Count >= _opts.InsertBatchSize)
            {
                batchNum++;
                total += buffer.Count;
                totalFailures += await FlushAsync(buffer, batchNum, total, ct);
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            batchNum++;
            total += buffer.Count;
            totalFailures += await FlushAsync(buffer, batchNum, total, ct);
        }
        _log.LogInformation("BackupOldRows ✓ {Total} rows queued ({Batches} batches), {Failed} insert failures",
            total, batchNum, totalFailures);
    }

    private async Task<int> FlushAsync(List<IDictionary<string, object?>> buffer, int batchNum, long runningTotal, CancellationToken ct)
    {
        _log.LogInformation("BackupOldRows: inserting batch {Batch} ({Rows} rows → HOLDING, running total {Total})",
            batchNum, buffer.Count, runningTotal);
        var results = await _holding.InsertManyAsync(buffer, ct);
        var failed = 0;
        string? firstErr = null;
        foreach (var r in results)
        {
            if (!r.Success)
            {
                failed++;
                firstErr ??= r.Error;
            }
        }
        if (failed > 0)
            _log.LogError("BackupOldRows batch {Batch} had {Failed}/{Total} HOLDING insert failures. First error: {Err}",
                batchNum, failed, buffer.Count, firstErr);
        return failed;
    }
}
