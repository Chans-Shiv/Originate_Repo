using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;

namespace OrigenateFunction.Repositories;

public abstract class TableRepositoryBase
{
    private readonly IBulkWriter _writer;
    private readonly IBulkDeleter _deleter;
    private readonly IPagedReader _reader;
    private readonly ILogger _log;
    private readonly OrigenateOptions _opts;

    public abstract string EntityLogicalName { get; }
    public abstract string PrimaryIdField { get; }

    protected TableRepositoryBase(
        IBulkWriter writer, IBulkDeleter deleter, IPagedReader reader,
        IOptions<OrigenateOptions> opts, ILogger log)
    {
        _writer = writer; _deleter = deleter; _reader = reader; _log = log;
        _opts = opts.Value;
    }

    public IAsyncEnumerable<Entity> StreamAsync(FilterExpression? filter, IEnumerable<string> select, CancellationToken ct)
    {
        var query = new QueryExpression(EntityLogicalName)
        {
            ColumnSet = new ColumnSet(select.ToArray()),
            PageInfo = new PagingInfo { Count = _opts.PageSize, PageNumber = 1 }
        };
        if (filter is not null) query.Criteria = filter;
        return _reader.RetrieveAllAsync(query, ct);
    }

    public Task<IReadOnlyList<RowWriteResult>> InsertManyAsync(IReadOnlyList<Entity> entities, CancellationToken ct)
    {
        if (entities.Count == 0) return Task.FromResult<IReadOnlyList<RowWriteResult>>(Array.Empty<RowWriteResult>());
        var retyped = entities.Select(RetypeAsTarget).ToList();
        return _writer.CreateMultipleAsync(retyped, ct);
    }

    public Task<long> CountAsync(FilterExpression? filter, CancellationToken ct)
        => _reader.CountAsync(EntityLogicalName, filter, ct);

    public async Task TruncateAsync(CancellationToken ct)
    {
        _log.LogInformation("EventName=TruncateStart Entity={Logical} PrimaryId={PrimaryId} Threshold={Threshold}",
            EntityLogicalName, PrimaryIdField, _opts.BulkDeleteThreshold);

        var count = await _reader.CountAsync(EntityLogicalName, null, ct);
        if (count == 0)
        {
            _log.LogInformation("EventName=Truncate Entity={Logical} Deleted=0 Strategy=skip", EntityLogicalName);
            return;
        }

        if (count >= _opts.BulkDeleteThreshold)
        {
            _log.LogInformation(
                "EventName=TruncateStrategy Entity={Logical} Count={Count} Strategy=BulkDelete",
                EntityLogicalName, count);
            await _deleter.SubmitAndAwaitBulkDeleteAsync(EntityLogicalName, filter: null, ct);
            _log.LogInformation("EventName=Truncate Entity={Logical} Deleted={Count} Strategy=BulkDelete",
                EntityLogicalName, count);
            return;
        }

        _log.LogInformation(
            "EventName=TruncateStrategy Entity={Logical} Count={Count} Strategy=ExecuteMultiple",
            EntityLogicalName, count);
        await DeleteViaExecuteMultipleAsync(ct);
        _log.LogInformation("EventName=Truncate Entity={Logical} Deleted={Count} Strategy=ExecuteMultiple",
            EntityLogicalName, count);
    }

    private async Task DeleteViaExecuteMultipleAsync(CancellationToken ct)
    {
        var query = new QueryExpression(EntityLogicalName)
        {
            ColumnSet = new ColumnSet(PrimaryIdField),
            PageInfo = new PagingInfo { Count = _opts.PageSize, PageNumber = 1 }
        };

        var ids = new List<Guid>();
        await foreach (var e in _reader.RetrieveAllAsync(query, ct))
            if (e.Id != Guid.Empty) ids.Add(e.Id);

        if (ids.Count == 0) return;

        var batches = new List<IReadOnlyList<Guid>>();
        for (int i = 0; i < ids.Count; i += _opts.DeleteBatchSize)
            batches.Add(ids.Skip(i).Take(_opts.DeleteBatchSize).ToArray());

        _log.LogInformation(
            "EventName=TruncateDeleteStart Entity={Logical} Total={Total} Batches={Batches} Size={Size} Parallel={Par}",
            EntityLogicalName, ids.Count, batches.Count, _opts.DeleteBatchSize, _opts.MaxParallel);

        using var sem = new SemaphoreSlim(_opts.MaxParallel, _opts.MaxParallel);
        var tasks = batches.Select(async (b, idx) =>
        {
            await sem.WaitAsync(ct);
            try
            {
                _log.LogInformation("EventName=TruncateBatch Entity={Logical} Batch={Idx}/{Total} Count={Count}",
                    EntityLogicalName, idx + 1, batches.Count, b.Count);
                await _deleter.DeleteBatchAsync(EntityLogicalName, b, ct);
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    private Entity RetypeAsTarget(Entity src)
    {
        if (string.Equals(src.LogicalName, EntityLogicalName, StringComparison.OrdinalIgnoreCase))
            return src;
        var e = new Entity(EntityLogicalName);
        foreach (var attr in src.Attributes)
            e[attr.Key] = attr.Value;
        return e;
    }
}
