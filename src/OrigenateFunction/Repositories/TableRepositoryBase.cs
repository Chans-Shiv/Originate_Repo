using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;

namespace OrigenateFunction.Repositories;

public abstract class TableRepositoryBase
{
    private readonly IBulkWriter _writer;
    private readonly IBulkDeleter _deleter;
    private readonly IPagedReader _reader;
    private readonly ILogger _log;
    private readonly int _pageSize;
    private readonly int _deleteBatch;
    private readonly int _maxParallel;

    public abstract string EntityLogicalName { get; }
    public abstract string EntitySet { get; }
    public abstract string PrimaryIdField { get; }

    protected TableRepositoryBase(
        IBulkWriter writer, IBulkDeleter deleter, IPagedReader reader,
        IOptions<OrigenateOptions> opts, ILogger log)
    {
        _writer = writer; _deleter = deleter; _reader = reader; _log = log;
        _pageSize = opts.Value.PageSize;
        _deleteBatch = opts.Value.DeleteBatchSize;
        _maxParallel = opts.Value.MaxParallel;
    }

    public IAsyncEnumerable<JsonElement> StreamAsync(string? filter, IEnumerable<string> select, CancellationToken ct)
        => _reader.RetrieveAllAsync(EntitySet, filter, select, _pageSize, ct);

    public Task<IReadOnlyList<RowWriteResult>> InsertManyAsync(
        IReadOnlyList<IDictionary<string, object?>> records, CancellationToken ct)
        => _writer.CreateMultipleAsync(EntityLogicalName, records, ct);

    public Task<long> CountAsync(string? filter, CancellationToken ct)
        => _reader.CountAsync(EntitySet, filter, ct);

    public async Task TruncateAsync(CancellationToken ct)
    {
        _log.LogInformation("Truncate ▶ {EntitySet} (logical={Logical}, primaryId={PrimaryId})",
            EntitySet, EntityLogicalName, PrimaryIdField);

        var ids = new List<Guid>();
        await foreach (var item in _reader.RetrieveAllAsync(EntitySet, null, new[] { PrimaryIdField }, _pageSize, ct))
        {
            if (item.TryGetProperty(PrimaryIdField, out var idEl) && Guid.TryParse(idEl.GetString(), out var g))
                ids.Add(g);
        }
        _log.LogInformation("Truncate {EntitySet}: found {Count} ids", EntitySet, ids.Count);
        if (ids.Count == 0)
        {
            _log.LogInformation("Truncate ✓ {EntitySet} (nothing to delete)", EntitySet);
            return;
        }

        var batches = new List<IReadOnlyList<Guid>>();
        for (int i = 0; i < ids.Count; i += _deleteBatch)
            batches.Add(ids.Skip(i).Take(_deleteBatch).ToArray());

        _log.LogInformation("Truncate {EntitySet}: deleting {Total} rows in {Batches} batches (size={Size}, parallel={Par})",
            EntitySet, ids.Count, batches.Count, _deleteBatch, _maxParallel);

        using var sem = new SemaphoreSlim(_maxParallel, _maxParallel);
        var done = 0;
        var tasks = batches.Select(async (b, idx) =>
        {
            await sem.WaitAsync(ct);
            try
            {
                _log.LogInformation("Truncate {EntitySet}: batch {Idx}/{Total} ({Count} ids)",
                    EntitySet, idx + 1, batches.Count, b.Count);
                await _deleter.DeleteBatchAsync(EntitySet, b, ct);
                Interlocked.Increment(ref done);
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        _log.LogInformation("Truncate ✓ {EntitySet}: {Deleted}/{Total} rows deleted", EntitySet, ids.Count, ids.Count);
    }
}
