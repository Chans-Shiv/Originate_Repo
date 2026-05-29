namespace OrigenateFunction.Diagnostics;

// Canonical event-name vocabulary for structured logs. Every EventName= value
// the app emits comes from here, so dashboards and KQL queries that filter on
// these names can't drift from the producers via typos.
//
// Naming: PascalCase. Present-tense for state transitions ("Started"/"Completed"),
// past-tense for one-shots ("Archived"/"Enqueued"). Add new events here first,
// then reference the constant — never inline a literal at the call site.
public static class LogEvents
{
    // Pipeline orchestration
    public const string PipelineStarted   = nameof(PipelineStarted);
    public const string PipelineCompleted = nameof(PipelineCompleted);
    public const string PipelineFailed    = nameof(PipelineFailed);
    public const string StepStarted       = nameof(StepStarted);
    public const string StepCompleted     = nameof(StepCompleted);
    public const string StepFailed        = nameof(StepFailed);

    // Dataverse bulk insert
    public const string BulkInsertBatchStart   = nameof(BulkInsertBatchStart);
    public const string BulkInsertBatch        = nameof(BulkInsertBatch);
    public const string BulkInsertBatchPartial = nameof(BulkInsertBatchPartial);
    public const string BulkInsertBatchFinal   = nameof(BulkInsertBatchFinal);
    public const string BulkInsertRetryStart   = nameof(BulkInsertRetryStart);

    // Dataverse truncate / bulk delete
    public const string TruncateStart        = nameof(TruncateStart);
    public const string Truncate             = nameof(Truncate);
    public const string TruncateStrategy     = nameof(TruncateStrategy);
    public const string TruncateDeleteStart  = nameof(TruncateDeleteStart);
    public const string TruncateBatch        = nameof(TruncateBatch);
    public const string BulkDeleteSubmit     = nameof(BulkDeleteSubmit);
    public const string BulkDeleteSubmitted  = nameof(BulkDeleteSubmitted);
    public const string BulkDeletePoll       = nameof(BulkDeletePoll);
    public const string BulkDelete           = nameof(BulkDelete);
    public const string BulkDeleteBatch      = nameof(BulkDeleteBatch);
    public const string BulkDeleteBatchPartial = nameof(BulkDeleteBatchPartial);

    // Schema cache / coercion
    public const string SchemaWarmed       = nameof(SchemaWarmed);
    public const string UnknownAttribute   = nameof(UnknownAttribute);
    public const string CoercionFailed     = nameof(CoercionFailed);

    // Dead-letter pipeline (NEW)
    public const string DeadLetterEnqueued       = nameof(DeadLetterEnqueued);
    public const string DeadLetterEnqueueFailed  = nameof(DeadLetterEnqueueFailed);
    public const string DeadLetterBatchEnqueued  = nameof(DeadLetterBatchEnqueued);
    public const string DeadLetterDequeued       = nameof(DeadLetterDequeued);
    public const string DeadLetterGivenUp        = nameof(DeadLetterGivenUp);
    public const string ErrorTableWritten        = nameof(ErrorTableWritten);
    public const string ErrorTableWriteFailed    = nameof(ErrorTableWriteFailed);
    public const string ErrorTableNotConfigured  = nameof(ErrorTableNotConfigured);
    public const string FailureArchived          = nameof(FailureArchived);
    public const string FailureArchiveWriteFailed = nameof(FailureArchiveWriteFailed);
}
