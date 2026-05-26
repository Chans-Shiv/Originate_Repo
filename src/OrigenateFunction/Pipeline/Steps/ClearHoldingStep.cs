using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class ClearHoldingStep : IPipelineStep
{
    private readonly HoldingRepository _holding;
    private readonly ILogger<ClearHoldingStep> _log;

    public ClearHoldingStep(HoldingRepository holding, ILogger<ClearHoldingStep> log)
    {
        _holding = holding; _log = log;
    }

    public string Name => "Clear STG_ORIGENATE_HOLDING";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("EventName=ClearHoldingStart Entity={Logical}", _holding.EntityLogicalName);
        await _holding.TruncateAsync(ct);
        _log.LogInformation("EventName=ClearHolding");
    }
}
