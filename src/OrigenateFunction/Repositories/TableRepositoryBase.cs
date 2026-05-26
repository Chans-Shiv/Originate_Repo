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
    private readonly int _pageSize;
    private readonly int _deleteBatch;
    private readonly int _maxParallel;

    public abstract string EntityLogicalName { get; }
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

    public IAsyncEnumerable<Entity> StreamAsync(FilterExpression? filter, IEnumerable<string> select, CancellationToken ct)
    {
        var query = new QueryExpression(EntityLogicalName)
        {
            ColumnSet = new ColumnSet(select.ToArray()),
            PageInfo = new PagingInfo { Count = _pageSize, PageNumber = 1 }
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
        _log.LogInformation("EventName=TruncateStart Entity={Logical} PrimaryId={PrimaryId}",
            EntityLogicalName, PrimaryIdField);

        var query = new QueryExpression(EntityLogicalName)
        {
            ColumnSet = new ColumnSet(PrimaryIdField),
            PageInfo = new PagingInfo { Count = _pageSize, PageNumber = 1 }
        };

        var ids = new List<Guid>();
        await foreach (var e in _reader.RetrieveAllAsync(query, ct))
            if (e.Id != Guid.Empty) ids.Add(e.Id);

        _log.LogInformation("EventName=TruncateFoundIds Entity={Logical} Count={Count}", EntityLogicalName, ids.Count);
        if (ids.Count == 0)
        {
            _log.LogInformation("EventName=Truncate Entity={Logical} Deleted=0", EntityLogicalName);
            return;
        }

        var batches = new List<IReadOnlyList<Guid>>();
        for (int i = 0; i < ids.Count; i += _deleteBatch)
            batches.Add(ids.Skip(i).Take(_deleteBatch).ToArray());

        _log.LogInformation(
            "EventName=TruncateDeleteStart Entity={Logical} Total={Total} Batches={Batches} Size={Size} Parallel={Par}",
            EntityLogicalName, ids.Count, batches.Count, _deleteBatch, _maxParallel);

        using var sem = new SemaphoreSlim(_maxParallel, _maxParallel);
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

        _log.LogInformation("EventName=Truncate Entity={Logical} Deleted={Count}", EntityLogicalName, ids.Count);
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
