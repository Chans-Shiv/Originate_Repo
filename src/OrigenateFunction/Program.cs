using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Dataverse;
using OrigenateFunction.Excel;
using OrigenateFunction.Mappers;
using OrigenateFunction.Options;
using OrigenateFunction.Pipeline;
using OrigenateFunction.Pipeline.Steps;
using OrigenateFunction.Repositories;
using OrigenateFunction.Storage;

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

        services.AddOptions<OrigenateOptions>().Configure<IConfiguration>((o, c) => c.Bind(o));
        services.AddOptions<BlobConnectionOptions>()
            .Configure<IConfiguration>((o, c) => c.GetSection("BlobConnection").Bind(o));

        // DefaultAzureCredential chain switches based on IS_LOCAL_DEV:
        //   local  → Az CLI + Interactive Browser; Managed Identity excluded
        //   prod   → Managed Identity (and Workload Identity for AKS); CLI/Browser excluded
        // Other sources (Env, SharedTokenCache, VS, VSCode, PowerShell, AZD) are always
        // excluded — they frequently pick up stale tokens or wrong-tenant principals and
        // cause 403 AuthorizationPermissionMismatch even when the *intended* account has
        // the right RBAC roles.
        services.AddSingleton<TokenCredential>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrigenateOptions>>().Value;
            var cfg = sp.GetRequiredService<IConfiguration>();
            var isLocal = string.Equals(cfg["IS_LOCAL_DEV"], "true", StringComparison.OrdinalIgnoreCase);

            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeAzureDeveloperCliCredential = true,

                // Local: developer auth only (CLI + optional interactive browser).
                // Prod:  managed identity (and workload identity for AKS); no human sign-in.
                ExcludeAzureCliCredential          = !isLocal,
                ExcludeInteractiveBrowserCredential = !isLocal || !opts.UseInteractiveBrowser,
                ExcludeManagedIdentityCredential    =  isLocal,
                ExcludeWorkloadIdentityCredential   =  isLocal,

                TenantId = string.IsNullOrWhiteSpace(opts.AzureTenantId) ? null : opts.AzureTenantId
            });
        });

        // BlobServiceClient — identity-based only. Resolves via BlobConnection__serviceUri
        // (e.g. "https://<account>.blob.core.windows.net") and authenticates with the
        // shared TokenCredential (DefaultAzureCredential chain: Az CLI / Interactive Browser).
        // The caller principal must hold "Storage Blob Data Contributor" on the account.
        services.AddSingleton(sp =>
        {
            var serviceUri = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobConnectionOptions>>().Value.ServiceUri;
            if (string.IsNullOrWhiteSpace(serviceUri) || LooksLikePlaceholder(serviceUri))
                throw new InvalidOperationException(
                    "BlobConnection__serviceUri is not configured. In local.settings.json set " +
                    "'BlobConnection__serviceUri' to your blob endpoint, e.g. " +
                    "'https://<account>.blob.core.windows.net'. The signed-in identity must hold " +
                    "'Storage Blob Data Contributor' on the storage account.");

            return new BlobServiceClient(new Uri(serviceUri), sp.GetRequiredService<TokenCredential>());

            static bool LooksLikePlaceholder(string? value)
                => !string.IsNullOrWhiteSpace(value)
                   && (value.StartsWith('<') || value.Contains("PASTE_", StringComparison.OrdinalIgnoreCase));
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
        services.AddSingleton<IPipelineStep, ArchiveBlobStep>();

        services.AddSingleton<PipelineExecutor>();
    })
    .Build();

await host.RunAsync();
