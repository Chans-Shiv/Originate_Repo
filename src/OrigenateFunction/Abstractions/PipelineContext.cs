using OrigenateFunction.Models;

namespace OrigenateFunction.Abstractions;

public sealed class PipelineContext
{
    public required string BlobName { get; init; }
    public required Stream BlobStream { get; init; }
    public string TempXlsxPath { get; set; } = "";
    public List<FailedRow> FailedRows { get; } = new();
    public List<ExceptionRow> Exceptions { get; } = new();
}

public sealed record FailedRow(int RowNumber, OrigenateRow Source, string Error);
