namespace OrigenateFunction.Options;

public sealed class OrigenateOptions
{
    public string DataverseUrl { get; set; } = "";
    public string MainContainerName { get; set; } = "originate-landing";
    public string ArchiveContainerName { get; set; } = "originate-processed";
    public string FailedContainerName { get; set; } = "originate-failed";
    public int AgeThresholdMonths { get; set; } = 13;
    public string? AzureTenantId { get; set; }
    public bool UseInteractiveBrowser { get; set; } = true;

    public int InsertBatchSize { get; set; } = 1000;
    public int DeleteBatchSize { get; set; } = 1000;
    public int PageSize { get; set; } = 5000;
    public int MaxParallel { get; set; } = 4;
    public int MaxParallelBatches { get; set; } = 4;
    public int MaxRetries { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 5;

    // Truncate strategy: above this row count, use Dataverse BulkDeleteRequest
    // (server-side async job) instead of per-id ExecuteMultipleRequest.
    public int BulkDeleteThreshold { get; set; } = 10_000;
    public int BulkDeletePollIntervalSeconds { get; set; } = 5;
    public int BulkDeleteTimeoutMinutes { get; set; } = 30;
}


