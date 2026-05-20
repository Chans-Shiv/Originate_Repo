using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Dataverse;

namespace OrigenateFunction.Pipeline.Steps;

public sealed class ConnectDataverseStep : IPipelineStep
{
    private readonly DataverseConnectivityCheck _check;
    private readonly ILogger<ConnectDataverseStep> _log;

    public ConnectDataverseStep(DataverseConnectivityCheck check, ILogger<ConnectDataverseStep> log)
    {
        _check = check; _log = log;
    }

    public string Name => "Connect to Dataverse (WhoAmI)";

    public async Task ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var userId = await _check.WhoAmIAsync(ct);
        _log.LogInformation("Connected to Dataverse as {UserId}", userId);
    }
}
