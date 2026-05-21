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

        // DefaultAzureCredential chain: with `az login` done, AzureCliCredential
        // satisfies storage. InteractiveBrowserCredential remains as a fallback for
        // Dataverse (different audience; AzureCliCredential covers it too when
        // signed in to the right tenant).
        services.AddSingleton<TokenCredential>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrigenateOptions>>().Value;
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = !opts.UseInteractiveBrowser,
                TenantId = string.IsNullOrWhiteSpace(opts.AzureTenantId) ? null : opts.AzureTenantId
            });
        });

        services.AddSingleton(sp =>
        {
            var blob = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobConnectionOptions>>().Value;
            if (string.IsNullOrWhiteSpace(blob.ServiceUri))
                throw new InvalidOperationException("BlobConnection__serviceUri is not configured.");
            return new BlobServiceClient(new Uri(blob.ServiceUri), sp.GetRequiredService<TokenCredential>());
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
