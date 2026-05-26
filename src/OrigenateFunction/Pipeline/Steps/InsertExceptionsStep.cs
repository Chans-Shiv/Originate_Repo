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
        if (ctx.Exceptions.Count == 0) { _log.LogInformation("InsertExceptions ✓ nothing to insert"); return; }

        _log.LogInformation("InsertExceptions ▶ {Total} rows in batches of {Size}",
            ctx.Exceptions.Count, _opts.InsertBatchSize);

        int batchNum = 0, totalFailures = 0;
        for (int i = 0; i < ctx.Exceptions.Count; i += _opts.InsertBatchSize)
        {
            batchNum++;
            var chunk = ctx.Exceptions.Skip(i).Take(_opts.InsertBatchSize)
                .Select(e => e.ToDataverseEntity()).ToArray();
            _log.LogInformation("InsertExceptions: batch {Batch} ({Rows} rows)", batchNum, chunk.Length);
            var res = await _exceptions.InsertManyAsync(chunk, ct);
            for (int j = 0; j < chunk.Length; j++)
                if (!res[j].Success)
                {
                    totalFailures++;
                    _log.LogWarning("InsertExceptions failed app# {App}: {Err}",
                        ctx.Exceptions[i + j].ApplicationNumber, res[j].Error);
                }
        }
        _log.LogInformation("InsertExceptions ✓ {Total} attempted, {Failed} failures",
            ctx.Exceptions.Count, totalFailures);
    }
}
