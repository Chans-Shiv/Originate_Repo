using Fhn.Originate.FtbanknewSync.Domain.Entities;

namespace Fhn.Originate.FtbanknewSync.Domain.Models;

public sealed class PipelineContext
{
    public required string BlobName { get; init; }
    public required Stream BlobStream { get; init; }
    // FunctionContext.InvocationId of the BlobTrigger run that produced this context.
    // Carried so downstream steps (dead-letter enqueue, archive) can correlate
    // failures with the original invocation in App Insights.
    public string InvocationId { get; init; } = "";
    public string TempXlsxPath { get; set; } = "";
    public List<FailedRow> FailedRows { get; } = new();
    public List<ExceptionRow> Exceptions { get; } = new();
}

public sealed record FailedRow(int RowNumber, OrigenateRow Source, string Error);
