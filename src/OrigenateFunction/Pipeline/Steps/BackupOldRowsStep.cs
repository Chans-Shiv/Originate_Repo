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

        var buffer = new List<IDictionary<string, object?>>(_opts.InsertBatchSize);
        long total = 0;

        await foreach (var item in _stg.StreamAsync(filter, ColumnMap.StgBusinessFields, ct))
        {
            buffer.Add(_mapper.MapJsonToRecord(item, ColumnMap.StgBusinessFields));
            if (buffer.Count >= _opts.InsertBatchSize)
            {
                total += buffer.Count;
                await _holding.InsertManyAsync(buffer, ct);
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            total += buffer.Count;
            await _holding.InsertManyAsync(buffer, ct);
        }
        _log.LogInformation("Backed up {Count} rows ({Filter}) to HOLDING", total, filter);
    }
}
