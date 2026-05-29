using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Domain.Interfaces;

// Persists row-level failures so they can be retried/audited later.
// Implementation: one Storage Queue message per failed row →
// QueueTrigger consumer writes each to the Dataverse error table,
// with a final-attempt blob archive on give-up.
public interface IDeadLetterService
{
    // sourceBlobName: the .xlsx the failures came from (e.g. "loans-2025-05.xlsx")
    // invocationId  : FunctionContext.InvocationId of the producer run, for correlation
    // stepName      : pipeline step that raised the failures (e.g. "LoadExcelStep")
    // failures      : the rows to enqueue — already accumulated on PipelineContext.FailedRows
    Task WriteAsync(
        string sourceBlobName,
        string invocationId,
        string stepName,
        IReadOnlyList<FailedRow> failures,
        CancellationToken ct = default);
}
