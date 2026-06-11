using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;
using Fhn.Originate.FtbanknewSync.Infrastructure.Excel;
using Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;
using Fhn.Originate.FtbanknewSync.Domain.Entities;
using Fhn.Originate.FtbanknewSync.Configuration;
using Fhn.Originate.FtbanknewSync.Infrastructure.Repositories;

namespace Fhn.Originate.FtbanknewSync.Application.Pipeline.Steps;

public sealed class LoadExcelStep : IPipelineStep
{
    private readonly OpenXmlExcelReader _excel;
    private readonly StgOrigenateRepository _stg;
    private readonly EntityBuilder _builder;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<LoadExcelStep> _log;

    public LoadExcelStep(
        OpenXmlExcelReader excel, StgOrigenateRepository stg, EntityBuilder builder,
        IOptions<OrigenateOptions> opts, ILogger<LoadExcelStep> log)
    {
        _excel = excel; _stg = stg; _builder = builder; _opts = opts.Value; _log = log;
    }

    public string Name => "Stream Excel → STG (collect exceptions + per-row failures)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("EventName=LoadExcelStart Blob={Blob} BatchSize={Size} Parallel={Par}",
            ctx.BlobName, _opts.InsertBatchSize, _opts.MaxParallelBatches);

        using var sem = new SemaphoreSlim(_opts.MaxParallelBatches, _opts.MaxParallelBatches);
        var failureLock = new object();
        var inFlight = new List<Task>();

        var batch = new List<OrigenateRow>(_opts.InsertBatchSize);
        long total = 0;
        int batchNum = 0;

        // Dispatch a batch under the concurrency gate, releasing the slot when it finishes.
        async Task DispatchAsync(OrigenateRow[] rows, int n)
        {
            try { await InsertAsync(rows, n, ctx, failureLock, ct); }
            finally { sem.Release(); }
        }

        // BACKPRESSURE: acquire a slot BEFORE handing off each batch. Once all
        // MaxParallelBatches slots are taken, this await parks the foreach — which stops
        // pulling from the Excel reader — so at most MaxParallelBatches × InsertBatchSize
        // rows are ever resident. Peak memory stays flat regardless of file size; the
        // reader can never run ahead and materialize the whole file.
        foreach (var row in _excel.StreamRows(ctx.TempXlsxPath))
        {
            batch.Add(row);
            CollectException(row, ctx);
            if (batch.Count >= _opts.InsertBatchSize)
            {
                await sem.WaitAsync(ct);
                var rows = batch.ToArray();
                batch.Clear();
                total += rows.Length;
                inFlight.Add(DispatchAsync(rows, ++batchNum));
            }
        }
        if (batch.Count > 0)
        {
            await sem.WaitAsync(ct);
            var rows = batch.ToArray();
            total += rows.Length;
            inFlight.Add(DispatchAsync(rows, ++batchNum));
        }
        await Task.WhenAll(inFlight);

        _log.LogInformation(
            "EventName=LoadExcel Blob={Blob} Total={Total} Batches={Batches} Exceptions={Exceptions} Failed={Failed}",
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

    private async Task InsertAsync(
        OrigenateRow[] rows, int batchNum, PipelineContext ctx,
        object failureLock, CancellationToken ct)
    {
        _log.LogInformation("EventName=LoadExcelBatchStart Batch={Batch} Rows={Rows}", batchNum, rows.Length);

        var entities = new Entity[rows.Length];
        for (int i = 0; i < rows.Length; i++)
            entities[i] = await _builder.BuildOrigenateRowAsync(rows[i], _stg.EntityLogicalName, ct);

        var results = await _stg.InsertManyAsync(entities, ct);

        var newFailures = 0;
        string? firstErr = null;
        int? firstErrRowNumber = null;
        for (int i = 0; i < rows.Length; i++)
        {
            if (!results[i].Success)
            {
                newFailures++;
                lock (failureLock)
                    ctx.FailedRows.Add(new FailedRow(rows[i].RowNumber, rows[i], results[i].Error ?? "unknown"));
                if (firstErr is null)
                {
                    firstErr = results[i].Error;
                    firstErrRowNumber = rows[i].RowNumber;
                }
            }
        }
        if (newFailures > 0)
            _log.LogError("EventName=LoadExcelBatchPartial Batch={Batch} Failed={Failed}/{Total} ExcelRow={Row} FirstError={Err}",
                batchNum, newFailures, rows.Length, firstErrRowNumber, firstErr);
        else
            _log.LogInformation("EventName=LoadExcelBatch Batch={Batch} Rows={Count} Failed=0", batchNum, rows.Length);
    }
}
