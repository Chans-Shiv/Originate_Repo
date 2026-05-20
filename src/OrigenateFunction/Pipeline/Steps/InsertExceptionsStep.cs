using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;
using OrigenateFunction.Repositories;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class InsertExceptionsStep : IPipelineStep
{
    private readonly ExceptionsRepository _exceptions;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<InsertExceptionsStep> _log;

    public InsertExceptionsStep(
        ExceptionsRepository exceptions,
        IOptions<OrigenateOptions> opts,
        ILogger<InsertExceptionsStep> log)
    {
        _exceptions = exceptions; _opts = opts.Value; _log = log;
    }

    public string Name => "Insert STG_ORIGENATE_EXCEPTIONS";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        if (ctx.Exceptions.Count == 0) { _log.LogInformation("No exception rows"); return; }

        for (int i = 0; i < ctx.Exceptions.Count; i += _opts.InsertBatchSize)
        {
            var chunk = ctx.Exceptions.Skip(i).Take(_opts.InsertBatchSize)
                .Select(e => e.ToDataverseEntity()).ToArray();
            var res = await _exceptions.InsertManyAsync(chunk, ct);
            for (int j = 0; j < chunk.Length; j++)
                if (!res[j].Success)
                    _log.LogWarning("Exception insert failed app# {App}: {Err}",
                        ctx.Exceptions[i + j].ApplicationNumber, res[j].Error);
        }
        _log.LogInformation("Inserted {Count} exception rows", ctx.Exceptions.Count);
    }
}
