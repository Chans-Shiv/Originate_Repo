using System.Text;
using System.Text.Json;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class BulkWriter : IBulkWriter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };
    private readonly IDataverseGateway _gw;
    public BulkWriter(IDataverseGateway gw) => _gw = gw;

    public async Task<IReadOnlyList<RowWriteResult>> CreateMultipleAsync(
        string entityLogicalName,
        IReadOnlyList<IDictionary<string, object?>> records,
        CancellationToken ct)
    {
        if (records.Count == 0) return Array.Empty<RowWriteResult>();

        var targets = records.Select(r =>
        {
            var d = new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)
            {
                ["@odata.type"] = "Microsoft.Dynamics.CRM." + entityLogicalName
            };
            return d;
        }).ToArray();

        var payload = new Dictionary<string, object?> { ["Targets"] = targets };
        var entitySet = Pluralize(entityLogicalName);

        using var req = await _gw.CreateAuthorizedRequestAsync(
            HttpMethod.Post, $"{entitySet}/Microsoft.Dynamics.CRM.CreateMultiple", ct);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var res = await _gw.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
            return Enumerable.Range(0, records.Count).Select(_ => RowWriteResult.Ok()).ToArray();

        return await PerRowBatchFallbackAsync(entityLogicalName, records, ct);
    }

    private async Task<IReadOnlyList<RowWriteResult>> PerRowBatchFallbackAsync(
        string entityLogicalName, IReadOnlyList<IDictionary<string, object?>> records, CancellationToken ct)
    {
        var entitySet = Pluralize(entityLogicalName);
        var builder = new MultipartBatchBuilder(_gw.BaseAddress);
        foreach (var r in records) builder.AddCreate(entitySet, r);

        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Post, "$batch", ct);
        req.Content = builder.Build();

        using var res = await _gw.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        var results = new RowWriteResult[records.Count];
        MultipartBatchResponseParser.ApplyResults(body, results);
        return results;
    }

    private static string Pluralize(string entityLogical)
        => entityLogical.EndsWith("s") ? entityLogical + "es" : entityLogical + "s";
}
