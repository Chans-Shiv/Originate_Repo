using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Application.Pipeline;
using OrigenateFunction.Application.Pipeline.Steps;
using OrigenateFunction.Configuration;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;
using OrigenateFunction.Infrastructure.Dataverse;
using OrigenateFunction.Infrastructure.DeadLetter;
using OrigenateFunction.Infrastructure.Excel;
using OrigenateFunction.Infrastructure.Repositories;
using OrigenateFunction.Infrastructure.Storage;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Functions worker installs a default LoggerFilterRule on the App Insights
        // provider that drops every category below Warning — which silences our
        // Information-level structured logs in the terminal. Remove it.
        services.Configure<LoggerFilterOptions>(options =>
        {
            var aiRule = options.Rules.FirstOrDefault(r => r.ProviderName ==
                "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (aiRule is not null) options.Rules.Remove(aiRule);
        });

        services.AddOptions<OrigenateOptions>().Configure<IConfiguration>((o, c) =>
        {
            c.Bind(o);
            // Override DeadLetterMaxAttempts from host.json so it never drifts from
            // the actual Functions runtime requeue count. Single source of truth.
            o.DeadLetterMaxAttempts = ReadHostJsonMaxDequeueCount(o.DeadLetterMaxAttempts);
        });

        // Identity-only storage auth. In Azure the deployed function's Managed
        // Identity picks up the storage roles; locally `az login` / VS sign-in
        // supplies the credential. ManagedIdentityCredential is excluded only in
        // local dev so we don't waste time probing an IMDS endpoint that doesn't
        // exist on a dev box (and can't accidentally pick up a stray VM/proxy
        // identity that lacks the storage RBAC roles).
        //
        // InteractiveBrowserCredential is opt-in (UseInteractiveBrowser=true) for
        // locked-down workstations where `az login` isn't viable.
        services.AddSingleton<TokenCredential>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OrigenateOptions>>().Value;
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential    = IsLocalDev(),
                ExcludeInteractiveBrowserCredential = !opts.UseInteractiveBrowser,
                TenantId = string.IsNullOrWhiteSpace(opts.AzureTenantId) ? null : opts.AzureTenantId
            });
        });

        // BlobServiceClient — identity-based, resolves from StorageAccountUrl. The
        // Functions runtime's own storage clients (BlobTrigger control queue, QueueTrigger
        // listener) use the AzureWebJobsStorage connection string with AccountKey, so the
        // runtime + RBAC concerns are decoupled. Caller principal still needs "Storage
        // Blob Data Contributor" on the account for THIS client's operations.
        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var blobUri = cfg["StorageAccountUrl"];
            RequireUri(blobUri, "StorageAccountUrl");
            return new BlobServiceClient(new Uri(blobUri!), sp.GetRequiredService<TokenCredential>());
        });

        // QueueServiceClient — derived from StorageAccountUrl (.blob. → .queue.), so blob
        // and queue stay on the same account by construction.
        //
        // MessageEncoding = Base64 matches the WebJobs QueueTrigger extension's default
        // decoding; without it, queue-trigger consumers would log "Message decoding has
        // failed!" and dead-letter every message.
        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var blobUri = cfg["StorageAccountUrl"];
            RequireUri(blobUri, "StorageAccountUrl");
            var queueUri = new Uri(blobUri!.Replace(".blob.core.windows.net", ".queue.core.windows.net"));
            return new QueueServiceClient(
                queueUri,
                sp.GetRequiredService<TokenCredential>(),
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        });

        // Dead-letter — Storage Queue producer (one msg per failed Excel row) +
        // QueueTrigger consumer that writes each message to the Dataverse error
        // table, with a give-up blob archive when retries exhaust.
        services.AddSingleton<IDeadLetterService>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OrigenateOptions>>().Value;
            var queue = sp.GetRequiredService<QueueServiceClient>().GetQueueClient(opts.DeadLetterQueueName);
            return new QueueDeadLetterService(queue, sp.GetRequiredService<ILogger<QueueDeadLetterService>>());
        });
        services.AddSingleton<DataverseErrorTableService>();
        services.AddSingleton<ErrorArchiveBlobWriter>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OrigenateOptions>>().Value;
            var container = sp.GetRequiredService<BlobServiceClient>()
                              .GetBlobContainerClient(opts.ErrorArchiveContainerName);
            return new ErrorArchiveBlobWriter(container, sp.GetRequiredService<ILogger<ErrorArchiveBlobWriter>>());
        });

        // Dataverse — ServiceClient SDK with cached token + Polly resilience pipeline.
        services.AddSingleton<DataverseConnectionFactory>();
        services.AddSingleton<DataverseResiliencePipeline>();
        services.AddSingleton<DataverseSchemaCache>();
        services.AddSingleton<AttributeCoercer>();
        services.AddSingleton<IBulkWriter, BulkWriter>();
        services.AddSingleton<IBulkDeleter, BulkDeleter>();
        services.AddSingleton<IPagedReader, PagedReader>();
        services.AddSingleton<DataverseConnectivityCheck>();
        services.AddSingleton<EntityProjector>();
        services.AddSingleton<EntityBuilder>();

        services.AddSingleton<StgOrigenateRepository>();
        services.AddSingleton<HoldingRepository>();
        services.AddSingleton<ExceptionsRepository>();

        // Storage — three responsibilities, three classes
        services.AddSingleton<ContainerClientFactory>();
        services.AddSingleton<IBlobArchiver, BlobArchiver>();
        services.AddSingleton<IFailedBlobMover, FailedBlobMover>();
        services.AddSingleton<IFailureFileWriter, BlobFailureFileWriter>();

        services.AddSingleton<OpenXmlExcelReader>();

        // Pipeline steps (order = registration order)
        services.AddSingleton<IPipelineStep, DownloadBlobStep>();
        services.AddSingleton<IPipelineStep, ConnectDataverseStep>();
        services.AddSingleton<IPipelineStep, LoadSchemaStep>();
        services.AddSingleton<IPipelineStep, ClearHoldingStep>();
        services.AddSingleton<IPipelineStep, BackupOldRowsStep>();
        services.AddSingleton<IPipelineStep, TruncateStgStep>();
        services.AddSingleton<IPipelineStep, LoadExcelStep>();
        services.AddSingleton<IPipelineStep, InsertExceptionsStep>();
        services.AddSingleton<IPipelineStep, ReconcileStep>();
        services.AddSingleton<IPipelineStep, UploadFailuresStep>();
        services.AddSingleton<IPipelineStep, EnqueueFailuresStep>();
        services.AddSingleton<IPipelineStep, ArchiveBlobStep>();

        services.AddSingleton<PipelineExecutor>();
    })
    .Build();

await host.RunAsync();

// Core Tools sets AZURE_FUNCTIONS_ENVIRONMENT=Development for `func start`; the
// deployed Function App leaves it unset (or set to Production), so this cleanly
// separates the two without inspecting connection-string shape.
static bool IsLocalDev() =>
    string.Equals(
        Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"),
        "Development",
        StringComparison.OrdinalIgnoreCase);

// Reads extensions.queues.maxDequeueCount from host.json so DeadLetterMaxAttempts
// is sourced from the same file the Functions runtime uses — no chance for the
// two to drift. Falls back to the OrigenateOptions default if the file or key
// is missing.
static int ReadHostJsonMaxDequeueCount(int fallback)
{
    var path = Path.Combine(AppContext.BaseDirectory, "host.json");
    if (!File.Exists(path)) return fallback;

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (doc.RootElement.TryGetProperty("extensions", out var ext)
        && ext.TryGetProperty("queues", out var queues)
        && queues.TryGetProperty("maxDequeueCount", out var max)
        && max.TryGetInt32(out var value))
    {
        return value;
    }
    return fallback;
}

static void RequireUri(string? value, string keyName)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException(
            $"{keyName} is not configured. Set it in local.settings.json to your blob endpoint, " +
            $"e.g. 'https://<account>.blob.core.windows.net'. The signed-in identity must hold " +
            $"'Storage Blob Data Contributor' + 'Storage Queue Data Contributor' on the account.");
    if (value.Contains('<') || value.Contains('>') ||
        value.Contains("PASTE_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("STORAGE_ACCOUNT", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"{keyName} still contains a placeholder ('{value}'). " +
            $"Replace '<STORAGE_ACCOUNT>' with your real Azure Storage account name.");
    if (!Uri.IsWellFormedUriString(value, UriKind.Absolute))
        throw new InvalidOperationException(
            $"{keyName}='{value}' is not a well-formed absolute URI.");
}
