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

        // DefaultAzureCredential chain narrowed to Az CLI + Interactive Browser only.
        // Excluded sources frequently pick up stale tokens (old Visual Studio sign-in,
        // shared token cache, env vars from a prior service principal) and cause
        // 403 AuthorizationPermissionMismatch even when the *intended* account has
        // the right RBAC roles.
        services.AddSingleton<TokenCredential>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrigenateOptions>>().Value;
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeSharedTokenCacheCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeAzureDeveloperCliCredential = true,

                ExcludeInteractiveBrowserCredential = !opts.UseInteractiveBrowser,
                TenantId = string.IsNullOrWhiteSpace(opts.AzureTenantId) ? null : opts.AzureTenantId
            });
        });

        // BlobServiceClient resolution, in priority order:
        //   1. Connection string with AccountKey  → key-auth (escape hatch for RBAC/firewall/CA blocks)
        //   2. "UseDevelopmentStorage=true"      → Azurite local emulator
        //   3. BlobConnection__serviceUri        → identity-based (AAD via TokenCredential)
        // Detects unresolved placeholder text and fails fast with an actionable message.
        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var cs = cfg["BlobConnection"];

            if (LooksLikePlaceholder(cs))
                throw new InvalidOperationException(
                    "BlobConnection in local.settings.json still contains the placeholder " +
                    "'<PASTE_STORAGE_CONNECTION_STRING_HERE>'. Replace it with a real connection " +
                    "string, OR set it to 'UseDevelopmentStorage=true' to use Azurite.");

            if (!string.IsNullOrWhiteSpace(cs) &&
                cs.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
                return new BlobServiceClient(cs);

            if (!string.IsNullOrWhiteSpace(cs) && cs.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase))
                return new BlobServiceClient(cs);

            var serviceUri = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobConnectionOptions>>().Value.ServiceUri;
            if (string.IsNullOrWhiteSpace(serviceUri))
                throw new InvalidOperationException(
                    "Storage auth is not configured. In local.settings.json set ONE of: " +
                    "(a) 'BlobConnection' to a real connection string (DefaultEndpointsProtocol=...;AccountKey=...), " +
                    "(b) 'BlobConnection' to 'UseDevelopmentStorage=true' for Azurite, " +
                    "(c) 'BlobConnection__serviceUri' (+ '__queueServiceUri') for identity-based auth.");
            return new BlobServiceClient(new Uri(serviceUri), sp.GetRequiredService<TokenCredential>());

            static bool LooksLikePlaceholder(string? value)
                => !string.IsNullOrWhiteSpace(value)
                   && (value.StartsWith("<", StringComparison.Ordinal) || value.Contains("PASTE_", StringComparison.OrdinalIgnoreCase));
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
