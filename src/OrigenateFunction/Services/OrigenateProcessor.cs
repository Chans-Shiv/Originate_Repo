using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Models;

namespace OrigenateFunction.Services;

public sealed class OrigenateProcessor
{
    private readonly DataverseClient _dv;
    private readonly BlobService _blobs;
    private readonly ExcelReader _excel;
    private readonly FailureWriter _failures;
    private readonly ILogger<OrigenateProcessor> _log;
    private readonly int _ageMonths;

    private const int InsertBatchSize = 500;
    private const int DeleteBatchSize = 500;
    private const int PageSize = 5000;
    private const int MaxParallel = 4;

    public OrigenateProcessor(
        DataverseClient dv, BlobService blobs, ExcelReader excel, FailureWriter failures,
        IConfiguration cfg, ILogger<OrigenateProcessor> log)
    {
        _dv = dv; _blobs = blobs; _excel = excel; _failures = failures; _log = log;
        _ageMonths = int.TryParse(cfg["AgeThresholdMonths"], out var m) ? m : 13;
    }

    public async Task ProcessAsync(Stream blobStream, string blobName, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _log.LogInformation("Starting pipeline for {Blob}", blobName);

        if (!blobName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("Skipping non-xlsx blob {Blob}", blobName);
            return;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"origenate-{Guid.NewGuid():N}.xlsx");
        try
        {
            await using (var fs = File.Create(tempPath))
                await blobStream.CopyToAsync(fs, ct);

            await _dv.WhoAmIAsync(ct);
            _log.LogInformation("Dataverse WhoAmI OK");

            await ClearHoldingAsync(ct);
            await BackupOldRowsAsync(ct);

            var failedRows = new List<FailureWriter.FailedRow>();
            var exceptions = new List<ExceptionRow>();

            await TruncateStgAsync(ct);
            await LoadExcelToStgAsync(tempPath, blobName, failedRows, exceptions, ct);
            await InsertExceptionsAsync(exceptions, failedRows, ct);
            await ReconcileHoldingToStgAsync(ct);

            if (failedRows.Count > 0)
            {
                _log.LogWarning("{Count} row failures; uploading failure CSV", failedRows.Count);
                await _failures.WriteAsync(blobName, failedRows, ct);
            }

            await _blobs.ArchiveAsync(blobName, ct);

            _log.LogInformation("Pipeline complete for {Blob} in {Elapsed}", blobName, sw.Elapsed);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pipeline failed for {Blob}; moving to failed container", blobName);
            try { await _blobs.MoveToFailedAsync(blobName, ct); }
            catch (Exception moveEx) { _log.LogError(moveEx, "Could not move blob to failed container"); }
            throw;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort */ }
        }
    }

    // ---- Step 3 ----
    private async Task ClearHoldingAsync(CancellationToken ct)
    {
        _log.LogInformation("Step 3: clearing {Table}", ColumnMap.HoldingEntityLogical);
        var ids = new List<Guid>();
        await foreach (var item in _dv.RetrieveAllAsync(
            ColumnMap.HoldingEntitySet, filter: null,
            select: new[] { ColumnMap.HoldingPrimaryId }, pageSize: PageSize, ct))
        {
            if (item.TryGetProperty(ColumnMap.HoldingPrimaryId, out var idEl)
                && Guid.TryParse(idEl.GetString(), out var g))
                ids.Add(g);
        }
        await ExecuteDeleteBatchesAsync(ColumnMap.HoldingEntitySet, ids, ct);
        var remaining = await _dv.CountAsync(ColumnMap.HoldingEntitySet, null, ct);
        _log.LogInformation("Step 3: deleted {Count}; remaining {Remaining}", ids.Count, remaining);
    }

    // ---- Step 4 ----
    private async Task BackupOldRowsAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMonths(-_ageMonths).ToString("o");
        var filter = $"createdon lt {cutoff}";
        _log.LogInformation("Step 4: copying STG rows with {Filter} → HOLDING", filter);

        var buffer = new List<IDictionary<string, object?>>(InsertBatchSize);
        long total = 0;

        await foreach (var item in _dv.RetrieveAllAsync(
            ColumnMap.StgOrigenateEntitySet, filter,
            select: ColumnMap.StgBusinessFields, PageSize, ct))
        {
            var rec = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in ColumnMap.StgBusinessFields)
            {
                if (item.TryGetProperty(field, out var el) && el.ValueKind != JsonValueKind.Null)
                    rec[field] = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
            }
            buffer.Add(rec);
            if (buffer.Count >= InsertBatchSize)
            {
                total += buffer.Count;
                await InsertBatchAsync(ColumnMap.HoldingEntityLogical, buffer, failedRows: null, ct);
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
        {
            total += buffer.Count;
            await InsertBatchAsync(ColumnMap.HoldingEntityLogical, buffer, failedRows: null, ct);
        }
        _log.LogInformation("Step 4: backed up {Count} rows to HOLDING", total);
    }

    // ---- Step 5a ----
    private async Task TruncateStgAsync(CancellationToken ct)
    {
        _log.LogInformation("Step 5a: truncating {Table}", ColumnMap.StgOrigenateEntityLogical);
        var ids = new List<Guid>();
        await foreach (var item in _dv.RetrieveAllAsync(
            ColumnMap.StgOrigenateEntitySet, null,
            new[] { ColumnMap.StgOrigenatePrimaryId }, PageSize, ct))
        {
            if (item.TryGetProperty(ColumnMap.StgOrigenatePrimaryId, out var idEl)
                && Guid.TryParse(idEl.GetString(), out var g))
                ids.Add(g);
        }
        await ExecuteDeleteBatchesAsync(ColumnMap.StgOrigenateEntitySet, ids, ct);
        _log.LogInformation("Step 5a: deleted {Count} rows", ids.Count);
    }

    // ---- Step 5b + 6 collection ----
    private async Task LoadExcelToStgAsync(
        string xlsxPath, string blobName,
        List<FailureWriter.FailedRow> failedRows, List<ExceptionRow> exceptions,
        CancellationToken ct)
    {
        _log.LogInformation("Step 5b/6: streaming Excel and inserting into STG");
        var batch = new List<OrigenateRow>(InsertBatchSize);
        long total = 0;

        foreach (var row in _excel.StreamRows(xlsxPath))
        {
            batch.Add(row);

            if (!string.IsNullOrWhiteSpace(row.ApplicationNumber)
                && row.Fields.TryGetValue(ColumnMap.PolicyExceptionsField, out var pe)
                && pe is string s && !string.IsNullOrWhiteSpace(s))
            {
                row.Fields.TryGetValue(ColumnMap.PolicyExceptionsReasonField, out var pr);
                exceptions.Add(new ExceptionRow(row.ApplicationNumber!, s, pr as string));
            }

            if (batch.Count >= InsertBatchSize)
            {
                total += batch.Count;
                await InsertOrigenateBatchAsync(batch, failedRows, ct);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            total += batch.Count;
            await InsertOrigenateBatchAsync(batch, failedRows, ct);
        }
        _log.LogInformation("Step 5b: inserted/attempted {Total} STG rows; {Failed} failures",
            total, failedRows.Count);
    }

    private async Task InsertOrigenateBatchAsync(
        IReadOnlyList<OrigenateRow> rows, List<FailureWriter.FailedRow> failedRows, CancellationToken ct)
    {
        var entities = rows.Select(r => r.ToDataverseEntity()).ToArray();
        var results = await _dv.CreateMultipleAsync(ColumnMap.StgOrigenateEntityLogical, entities, ct);
        for (int i = 0; i < rows.Count; i++)
            if (!results[i].Success)
                failedRows.Add(new FailureWriter.FailedRow(rows[i].RowNumber, rows[i], results[i].Error ?? "unknown"));
    }

    private async Task InsertBatchAsync(
        string entityLogical, IReadOnlyList<IDictionary<string, object?>> records,
        List<FailureWriter.FailedRow>? failedRows, CancellationToken ct)
    {
        var results = await _dv.CreateMultipleAsync(entityLogical, records, ct);
        if (failedRows is null) return;
        for (int i = 0; i < records.Count; i++)
            if (!results[i].Success)
                _log.LogWarning("Insert failed on {Entity}: {Error}", entityLogical, results[i].Error);
    }

    // ---- Step 6 (insert exception rows) ----
    private async Task InsertExceptionsAsync(
        IReadOnlyList<ExceptionRow> exceptions, List<FailureWriter.FailedRow> failedRows, CancellationToken ct)
    {
        if (exceptions.Count == 0) { _log.LogInformation("Step 6: no exception rows to insert"); return; }
        _log.LogInformation("Step 6: inserting {Count} exception rows", exceptions.Count);

        for (int i = 0; i < exceptions.Count; i += InsertBatchSize)
        {
            var chunk = exceptions.Skip(i).Take(InsertBatchSize).Select(e => e.ToDataverseEntity()).ToArray();
            var res = await _dv.CreateMultipleAsync(ColumnMap.ExceptionsEntityLogical, chunk, ct);
            for (int j = 0; j < chunk.Length; j++)
                if (!res[j].Success)
                    _log.LogWarning("Exception insert failed for app# {App}: {Err}",
                        exceptions[i + j].ApplicationNumber, res[j].Error);
        }
    }

    // ---- Step 7 ----
    private async Task ReconcileHoldingToStgAsync(CancellationToken ct)
    {
        _log.LogInformation("Step 7: reconcile HOLDING → STG (re-insert orphans)");
        var holdingPage = new List<string>(PageSize);
        long reinserted = 0;

        await foreach (var item in _dv.RetrieveAllAsync(
            ColumnMap.HoldingEntitySet, null,
            new[] { ColumnMap.ApplicationNumberField }, PageSize, ct))
        {
            if (item.TryGetProperty(ColumnMap.ApplicationNumberField, out var el)
                && el.ValueKind == JsonValueKind.String
                && el.GetString() is { Length: > 0 } app)
                holdingPage.Add(app);

            if (holdingPage.Count >= PageSize)
            {
                reinserted += await ReinsertOrphansAsync(holdingPage, ct);
                holdingPage.Clear();
            }
        }
        if (holdingPage.Count > 0)
            reinserted += await ReinsertOrphansAsync(holdingPage, ct);

        _log.LogInformation("Step 7: re-inserted {Count} orphan rows into STG", reinserted);
    }

    private async Task<int> ReinsertOrphansAsync(List<string> holdingAppNumbers, CancellationToken ct)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int chunk = 500;
        for (int i = 0; i < holdingAppNumbers.Count; i += chunk)
        {
            var slice = holdingAppNumbers.Skip(i).Take(chunk).ToArray();
            var filter = DataverseClient.BuildInFilter(ColumnMap.ApplicationNumberField, slice);
            await foreach (var item in _dv.RetrieveAllAsync(
                ColumnMap.StgOrigenateEntitySet, filter,
                new[] { ColumnMap.ApplicationNumberField }, PageSize, ct))
            {
                if (item.TryGetProperty(ColumnMap.ApplicationNumberField, out var el)
                    && el.GetString() is { } s)
                    present.Add(s);
            }
        }

        var orphans = holdingAppNumbers.Where(a => !present.Contains(a)).ToArray();
        if (orphans.Length == 0) return 0;

        var inserted = 0;
        for (int i = 0; i < orphans.Length; i += chunk)
        {
            var slice = orphans.Skip(i).Take(chunk).ToArray();
            var filter = DataverseClient.BuildInFilter(ColumnMap.ApplicationNumberField, slice);
            var buffer = new List<IDictionary<string, object?>>();
            await foreach (var item in _dv.RetrieveAllAsync(
                ColumnMap.HoldingEntitySet, filter,
                ColumnMap.StgBusinessFields, PageSize, ct))
            {
                var rec = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in ColumnMap.StgBusinessFields)
                    if (item.TryGetProperty(f, out var el) && el.ValueKind != JsonValueKind.Null)
                        rec[f] = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
                buffer.Add(rec);
                if (buffer.Count >= InsertBatchSize)
                {
                    inserted += buffer.Count;
                    await InsertBatchAsync(ColumnMap.StgOrigenateEntityLogical, buffer, null, ct);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                inserted += buffer.Count;
                await InsertBatchAsync(ColumnMap.StgOrigenateEntityLogical, buffer, null, ct);
            }
        }
        return inserted;
    }

    // ---- shared batched delete with parallelism ----
    private async Task ExecuteDeleteBatchesAsync(string entitySet, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var batches = new List<IReadOnlyList<Guid>>();
        for (int i = 0; i < ids.Count; i += DeleteBatchSize)
            batches.Add(ids.Skip(i).Take(DeleteBatchSize).ToArray());

        using var sem = new SemaphoreSlim(MaxParallel, MaxParallel);
        var tasks = batches.Select(async b =>
        {
            await sem.WaitAsync(ct);
            try { await _dv.DeleteBatchAsync(entitySet, b, ct); }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);
    }
}
