using System.Text.Json;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class DataverseConnectivityCheck
{
    private readonly IDataverseGateway _gw;
    public DataverseConnectivityCheck(IDataverseGateway gw) => _gw = gw;

    public async Task<string> WhoAmIAsync(CancellationToken ct)
    {
        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Get, "WhoAmI", ct);
        using var res = await _gw.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("UserId").GetString() ?? "";
    }
}
