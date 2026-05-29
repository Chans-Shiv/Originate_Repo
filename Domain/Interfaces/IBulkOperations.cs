using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Fhn.Originate.FtbanknewSync.Domain.Interfaces;

public sealed record RowWriteResult(bool Success, string? Error)
{
    public static RowWriteResult Ok() => new(true, null);
    public static RowWriteResult Fail(string err) => new(false, err);
}

public interface IBulkWriter
{
    Task<IReadOnlyList<RowWriteResult>> CreateMultipleAsync(
        IReadOnlyList<Entity> entities,
        CancellationToken ct);
}

public interface IBulkDeleter
{
    Task DeleteBatchAsync(string entityLogicalName, IReadOnlyList<Guid> ids, CancellationToken ct);

    // Submits a Dataverse BulkDeleteRequest (server-side async job) for every row
    // matching the filter, polls until the AsyncOperation completes, and throws if
    // the job fails or the timeout elapses. Use this for tables with row counts
    // above the configured threshold; below that, prefer DeleteBatchAsync.
    Task SubmitAndAwaitBulkDeleteAsync(
        string entityLogicalName, FilterExpression? filter, CancellationToken ct);
}

public interface IPagedReader
{
    IAsyncEnumerable<Entity> RetrieveAllAsync(
        QueryExpression query,
        CancellationToken ct = default);

    Task<long> CountAsync(string entityLogicalName, FilterExpression? filter, CancellationToken ct);
}
