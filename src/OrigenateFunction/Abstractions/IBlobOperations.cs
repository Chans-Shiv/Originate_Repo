namespace OrigenateFunction.Abstractions;

public interface IBlobArchiver
{
    Task ArchiveAsync(string sourceBlobName, CancellationToken ct);
}

public interface IFailedBlobMover
{
    Task MoveToFailedAsync(string sourceBlobName, CancellationToken ct);
}

public interface IFailureFileWriter
{
    Task WriteAsync(string sourceXlsxName, IReadOnlyList<FailedRow> failed, CancellationToken ct);
}
