using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Excel;
using OrigenateFunction.Models;
using OrigenateFunction.Options;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class LoadExcelStep : IPipelineStep
{
    private readonly OpenXmlExcelReader _excel;
    private readonly StgOrigenateRepository _stg;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<LoadExcelStep> _log;

    public LoadExcelStep(
        OpenXmlExcelReader excel, StgOrigenateRepository stg,
        IOptions<OrigenateOptions> opts, ILogger<LoadExcelStep> log)
    {
        _excel = excel; _stg = stg; _opts = opts.Value; _log = log;
    }

    public string Name => "Stream Excel → STG (collect exceptions + per-row failures)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("LoadExcel ▶ {Blob} (batchSize={Size})", ctx.BlobName, _opts.InsertBatchSize);

        var batch = new List<OrigenateRow>(_opts.InsertBatchSize);
        long total = 0;
        int batchNum = 0;

        foreach (var row in _excel.StreamRows(ctx.TempXlsxPath))
        {
            batch.Add(row);
            CollectException(row, ctx);
            if (batch.Count >= _opts.InsertBatchSize)
            {
                batchNum++;
                total += batch.Count;
                _log.LogInformation("LoadExcel: inserting batch {Batch} ({Rows} rows, total so far {Total})",
                    batchNum, batch.Count, total);
                await InsertAsync(batch, ctx, ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            batchNum++;
            total += batch.Count;
            _log.LogInformation("LoadExcel: inserting final batch {Batch} ({Rows} rows, total {Total})",
                batchNum, batch.Count, total);
            await InsertAsync(batch, ctx, ct);
        }
        _log.LogInformation("LoadExcel ✓ {Blob}: {Total} rows attempted across {Batches} batches, {Exceptions} exceptions queued, {Failed} per-row failures",
            ctx.BlobName, total, batchNum, ctx.Exceptions.Count, ctx.FailedRows.Count);
    }

    private static void CollectException(OrigenateRow row, PipelineContext ctx)
    {
        if (string.IsNullOrWhiteSpace(row.ApplicationNumber)) return;
        if (!row.Fields.TryGetValue(ColumnMap.PolicyExceptionsField, out var pe)) return;
        if (pe is not string s || string.IsNullOrWhiteSpace(s)) return;
        row.Fields.TryGetValue(ColumnMap.PolicyExceptionsReasonField, out var pr);
        ctx.Exceptions.Add(new ExceptionRow(row.ApplicationNumber!, s, pr as string));
    }

    private async Task InsertAsync(IReadOnlyList<OrigenateRow> rows, PipelineContext ctx, CancellationToken ct)
    {
        var entities = rows.Select(r => r.ToDataverseEntity()).ToArray();
        var results = await _stg.InsertManyAsync(entities, ct);

        var newFailures = 0;
        string? firstErrThisBatch = null;
        int? firstErrRowNumber = null;
        for (int i = 0; i < rows.Count; i++)
        {
            if (!results[i].Success)
            {
                newFailures++;
                ctx.FailedRows.Add(new FailedRow(rows[i].RowNumber, rows[i], results[i].Error ?? "unknown"));
                if (firstErrThisBatch is null)
                {
                    firstErrThisBatch = results[i].Error;
                    firstErrRowNumber = rows[i].RowNumber;
                }
            }
        }
        if (newFailures > 0)
        {
            _log.LogError("LoadExcel batch had {Failed}/{Total} failures. First error (Excel row {Row}): {Err}",
                newFailures, rows.Count, firstErrRowNumber, firstErrThisBatch);
        }
        else
        {
            _log.LogInformation("LoadExcel batch ✓ {Count} rows inserted", rows.Count);
        }
    }
}
