using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using OrigenateFunction.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<TokenCredential>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var tenantId = cfg["AzureTenantId"];
            var useInteractive = string.Equals(cfg["UseInteractiveBrowser"], "true", StringComparison.OrdinalIgnoreCase);
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = !useInteractive,
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId
            };
            return new DefaultAzureCredential(options);
        });

        services.AddSingleton(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var uri = cfg["BlobConnection:blobServiceUri"] ?? cfg["BlobConnection__blobServiceUri"];
            if (string.IsNullOrWhiteSpace(uri))
                throw new InvalidOperationException("BlobConnection__blobServiceUri is not configured.");
            return new BlobServiceClient(new Uri(uri), sp.GetRequiredService<TokenCredential>());
        });

        services.AddSingleton<DataverseTokenProvider>();
        services.AddSingleton<DataverseClient>();
        services.AddSingleton<BlobService>();
        services.AddSingleton<ExcelReader>();
        services.AddSingleton<FailureWriter>();
        services.AddSingleton<OrigenateProcessor>();
    })
    .Build();

await host.RunAsync();
