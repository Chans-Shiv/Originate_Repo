using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Pipeline;

namespace OrigenateFunction.Functions;

public sealed class OrigenateBlobTrigger
{
    private readonly PipelineExecutor _pipeline;
    private readonly ILogger<OrigenateBlobTrigger> _log;

    public OrigenateBlobTrigger(PipelineExecutor pipeline, ILogger<OrigenateBlobTrigger> log)
    {
        _pipeline = pipeline;
        _log = log;
    }

    [Function("OrigenateBlobTrigger")]
    public async Task Run(
        [BlobTrigger("originate-landing/{name}.xlsx", Connection = "BlobConnection")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        var fullName = name + ".xlsx";
        _log.LogInformation("Blob trigger fired for {Blob}", fullName);

        var ctx = new PipelineContext { BlobName = fullName, BlobStream = blobStream };
        await _pipeline.RunAsync(ctx, context.CancellationToken);
    }
}
