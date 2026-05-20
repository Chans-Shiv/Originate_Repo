using OrigenateFunction.Abstractions;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class TruncateStgStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    public TruncateStgStep(StgOrigenateRepository stg) => _stg = stg;
    public string Name => "Truncate STG_ORIGENATE";
    public Task ExecuteAsync(PipelineContext ctx, CancellationToken ct) => _stg.TruncateAsync(ct);
}
