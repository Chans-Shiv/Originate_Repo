namespace OrigenateFunction.Domain.Models;

// Wire format for one failed Excel row pushed onto the dead-letter Storage Queue.
// Each message is processed independently by DeadLetterProcessorFunction:
//   - Success → one row appended to the Dataverse error table
//   - All retries exhausted → one JSON blob in the error-archive container
//
// Only fields the downstream consumer or human triager actually reads should
// live here — serialization cost is paid per failure and the queue payload limit
// is 64 KB.
public sealed class DeadLetterMessage
{
    // What blob the failure came from (e.g. "loans-2025-05.xlsx").
    public string SourceBlobName { get; set; } = "";

    // FunctionContext.InvocationId of the producer run. Lets the operator
    // correlate the archived record with the original BlobTrigger run in
    // App Insights.
    public string InvocationId { get; set; } = "";

    // Which pipeline step raised the failure (e.g. "LoadExcelStep",
    // "InsertExceptionsStep"). Useful for spotting systemic problems.
    public string StepName { get; set; } = "";

    // 1-based row number in the source workbook. Lets ops jump straight to
    // the offending row in the file.
    public int RowNumber { get; set; }

    // Business identifiers carried from the source row. All optional —
    // very-early failures (e.g. header parse) won't have these populated.
    public string? AccountNumber { get; set; }
    public string? ApplicationNumber { get; set; }
    public Guid? LoanApplicationId { get; set; }

    // The original failure reason, captured at the producer.
    public string ErrorMessage { get; set; } = "";

    // When the producer enqueued this message (for elapsed-time triage).
    public DateTimeOffset EnqueuedAt { get; set; }
}
