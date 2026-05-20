namespace OrigenateFunction.Abstractions;

public interface IPipelineStep
{
    string Name { get; }
    Task ExecuteAsync(PipelineContext ctx, CancellationToken ct);
}
