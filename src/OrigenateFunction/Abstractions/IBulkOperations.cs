using System.Text.Json;

namespace OrigenateFunction.Abstractions;

public sealed record RowWriteResult(bool Success, string? Error)
{
    public static RowWriteResult Ok() => new(true, null);
    public static RowWriteResult Fail(string err) => new(false, err);
}

public interface IBulkWriter
{
    Task<IReadOnlyList<RowWriteResult>> CreateMultipleAsync(
        string entityLogicalName,
        IReadOnlyList<IDictionary<string, object?>> records,
        CancellationToken ct);
}

public interface IBulkDeleter
{
    Task DeleteBatchAsync(string entitySet, IReadOnlyList<Guid> ids, CancellationToken ct);
}

public interface IPagedReader
{
    IAsyncEnumerable<JsonElement> RetrieveAllAsync(
        string entitySet,
        string? filter,
        IEnumerable<string> select,
        int pageSize,
        CancellationToken ct = default);

    Task<long> CountAsync(string entitySet, string? filter, CancellationToken ct);
}
