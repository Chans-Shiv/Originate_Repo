using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class TruncateStgStep : IPipelineStep
{
    private readonly StgOrigenateRepository _stg;
    private readonly ILogger<TruncateStgStep> _log;

    public TruncateStgStep(StgOrigenateRepository stg, ILogger<TruncateStgStep> log)
    {
        _stg = stg; _log = log;
    }

    public string Name => "Truncate STG_ORIGENATE";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        _log.LogInformation("EventName=TruncateStgStart Entity={Logical}", _stg.EntityLogicalName);
        await _stg.TruncateAsync(ct);
        _log.LogInformation("EventName=TruncateStg");
    }
}
