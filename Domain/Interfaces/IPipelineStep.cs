namespace Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;

public interface IPipelineStep
{
    string Name { get; }
    Task ExecuteAsync(PipelineContext ctx, CancellationToken ct);
}
