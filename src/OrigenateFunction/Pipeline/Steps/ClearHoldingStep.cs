using OrigenateFunction.Abstractions;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class ClearHoldingStep : IPipelineStep
{
    private readonly HoldingRepository _holding;
    public ClearHoldingStep(HoldingRepository holding) => _holding = holding;
    public string Name => "Clear STG_ORIGENATE_HOLDING";
    public Task ExecuteAsync(PipelineContext ctx, CancellationToken ct) => _holding.TruncateAsync(ct);
}
