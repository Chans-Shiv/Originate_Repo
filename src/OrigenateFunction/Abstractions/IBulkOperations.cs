using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace OrigenateFunction.Abstractions;

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
}

public interface IPagedReader
{
    IAsyncEnumerable<Entity> RetrieveAllAsync(
        QueryExpression query,
        CancellationToken ct = default);

    Task<long> CountAsync(string entityLogicalName, FilterExpression? filter, CancellationToken ct);
}
