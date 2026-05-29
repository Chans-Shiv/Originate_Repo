namespace OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;

public interface IPipelineStep
{
    string Name { get; }
    Task ExecuteAsync(PipelineContext ctx, CancellationToken ct);
}
