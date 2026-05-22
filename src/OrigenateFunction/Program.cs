using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // BlobServiceClient resolution: connection string takes priority (escape hatch
        // for environments where RBAC / firewall / Conditional Access blocks AAD auth).
        // Falls back to identity-based binding (BlobConnection__serviceUri + TokenCredential)
        // when no connection string is provided. Production typically uses identity-based
        // via Managed Identity on the Function App.
        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var cs = cfg["BlobConnection"];
            if (!string.IsNullOrWhiteSpace(cs) && cs.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase))
                return new BlobServiceClient(cs);

            var serviceUri = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobConnectionOptions>>().Value.ServiceUri;
            if (string.IsNullOrWhiteSpace(serviceUri))
                throw new InvalidOperationException(
                    "Storage auth is not configured. Set either a flat connection string at " +
                    "'BlobConnection' OR identity-based 'BlobConnection__serviceUri'.");
            return new BlobServiceClient(new Uri(serviceUri), sp.GetRequiredService<TokenCredential>());
        });

        // Dataverse — interfaces only where there's a real reason to swap (gateway, retry policy, bulk ops)
        services.AddHttpClient<IDataverseGateway, DataverseGateway>();
        services.AddSingleton<IDataverseTokenProvider, DataverseTokenProvider>();
        services.AddSingleton<HttpRequestFactory>();
        services.AddSingleton<IHttpRetryPolicy, ExponentialBackoffRetryPolicy>();
        services.AddSingleton<IBulkWriter, BulkWriter>();
        services.AddSingleton<IBulkDeleter, BulkDeleter>();
        services.AddSingleton<IPagedReader, PagedReader>();
        services.AddSingleton<DataverseConnectivityCheck>();
        services.AddSingleton<JsonRowMapper>();

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
