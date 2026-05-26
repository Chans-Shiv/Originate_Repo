using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Dataverse;
using OrigenateFunction.Models;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class LoadSchemaStep : IPipelineStep
{
    private readonly DataverseSchemaCache _schema;
    private readonly ILogger<LoadSchemaStep> _log;

    public LoadSchemaStep(DataverseSchemaCache schema, ILogger<LoadSchemaStep> log)
    {
        _schema = schema; _log = log;
    }

    public string Name => "Load Dataverse attribute metadata for STG / HOLDING / EXCEPTIONS";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("EventName=LoadSchemaStart");
        await Task.WhenAll(
            _schema.GetAttributesAsync(ColumnMap.StgOrigenateEntityLogical, ct),
            _schema.GetAttributesAsync(ColumnMap.HoldingEntityLogical, ct),
            _schema.GetAttributesAsync(ColumnMap.ExceptionsEntityLogical, ct));
        _log.LogInformation("EventName=LoadSchema");
    }
}
