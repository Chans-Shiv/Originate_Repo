using System.Diagnostics;
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
        [BlobTrigger("originate-landing/{name}.xlsx", Connection = "AzureWebJobsStorage")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        var fullName = name + ".xlsx";
        var sw = Stopwatch.StartNew();
        _log.LogInformation("==== Blob trigger fired for {Blob} (invocationId={Invocation}) ====",
            fullName, context.InvocationId);

        var ctx = new PipelineContext { BlobName = fullName, BlobStream = blobStream };
        try
        {
            await _pipeline.RunAsync(ctx, context.CancellationToken);
            _log.LogInformation("==== Blob trigger ✓ for {Blob} in {Elapsed} (rows failed: {Failed}, exceptions: {Exceptions}) ====",
                fullName, sw.Elapsed, ctx.FailedRows.Count, ctx.Exceptions.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "==== Blob trigger ✗ for {Blob} after {Elapsed} ====", fullName, sw.Elapsed);
            throw;
        }
    }
}
