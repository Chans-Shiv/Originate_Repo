using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Services;

namespace OrigenateFunction.Functions;

public sealed class OrigenateBlobTrigger
{
    private readonly OrigenateProcessor _processor;
    private readonly ILogger<OrigenateBlobTrigger> _log;

    public OrigenateBlobTrigger(OrigenateProcessor processor, ILogger<OrigenateBlobTrigger> log)
    {
        _processor = processor;
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
        await _processor.ProcessAsync(blobStream, fullName, context.CancellationToken);
    }
}
