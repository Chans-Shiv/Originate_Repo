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
        var batch = new List<OrigenateRow>(_opts.InsertBatchSize);
        long total = 0;

        foreach (var row in _excel.StreamRows(ctx.TempXlsxPath))
        {
            batch.Add(row);
            CollectException(row, ctx);
            if (batch.Count >= _opts.InsertBatchSize)
            {
                total += batch.Count;
                await InsertAsync(batch, ctx, ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            total += batch.Count;
            await InsertAsync(batch, ctx, ct);
        }
        _log.LogInformation("Inserted/attempted {Total} STG rows; {Failed} per-row failures",
            total, ctx.FailedRows.Count);
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
        for (int i = 0; i < rows.Count; i++)
            if (!results[i].Success)
                ctx.FailedRows.Add(new FailedRow(rows[i].RowNumber, rows[i], results[i].Error ?? "unknown"));
    }
}
