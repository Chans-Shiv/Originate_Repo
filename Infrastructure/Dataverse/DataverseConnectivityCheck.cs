using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace OrigenateFunction.Infrastructure.Dataverse;

public sealed class DataverseConnectivityCheck
{
    private readonly DataverseConnectionFactory _factory;
    public DataverseConnectivityCheck(DataverseConnectionFactory factory) => _factory = factory;

    public async Task<Guid> WhoAmIAsync(CancellationToken ct)
    {
        var resp = (WhoAmIResponse)await _factory.Client.ExecuteAsync(new WhoAmIRequest(), ct);
        return resp.UserId;
    }
}
