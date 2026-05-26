using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class BulkWriter : IBulkWriter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };
    private readonly IDataverseGateway _gw;
    private readonly ILogger<BulkWriter> _log;

    public BulkWriter(IDataverseGateway gw, ILogger<BulkWriter> log)
    {
        _gw = gw;
        _log = log;
    }

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
        var url = $"{entitySet}/Microsoft.Dynamics.CRM.CreateMultiple";

        _log.LogInformation("CreateMultiple → POST {Url} ({Count} records, logical={Logical})",
            url, records.Count, entityLogicalName);

        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Post, url, ct);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

        using var res = await _gw.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
        {
            _log.LogInformation("CreateMultiple ✓ {Url} {Status} ({Count} records)",
                url, (int)res.StatusCode, records.Count);
            return Enumerable.Range(0, records.Count).Select(_ => RowWriteResult.Ok()).ToArray();
        }

        var errBody = await res.Content.ReadAsStringAsync(ct);
        _log.LogWarning("CreateMultiple ✗ {Url} {Status}; falling back to per-row $batch. Body: {Body}",
            url, (int)res.StatusCode, Truncate(errBody, 1000));

        return await PerRowBatchFallbackAsync(entityLogicalName, records, ct);
    }

    private async Task<IReadOnlyList<RowWriteResult>> PerRowBatchFallbackAsync(
        string entityLogicalName, IReadOnlyList<IDictionary<string, object?>> records, CancellationToken ct)
    {
        var entitySet = Pluralize(entityLogicalName);
        var builder = new MultipartBatchBuilder(_gw.BaseAddress);
        foreach (var r in records) builder.AddCreate(entitySet, r);

        _log.LogInformation("Per-row $batch fallback → POST $batch ({Count} creates against {EntitySet})",
            records.Count, entitySet);

        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Post, "$batch", ct);
        req.Content = builder.Build();

        using var res = await _gw.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            _log.LogError("$batch fallback ✗ {Status}. Body: {Body}",
                (int)res.StatusCode, Truncate(body, 1000));

        var results = new RowWriteResult[records.Count];
        MultipartBatchResponseParser.ApplyResults(body, results);

        var failed = results.Count(r => r != null && !r.Success);
        if (failed > 0)
        {
            var firstErr = results.FirstOrDefault(r => r != null && !r.Success)?.Error;
            _log.LogWarning("$batch fallback completed: {Ok} ok, {Failed} failed. First error: {Err}",
                results.Length - failed, failed, Truncate(firstErr ?? "(none)", 500));
        }
        else
        {
            _log.LogInformation("$batch fallback ✓ all {Count} records inserted", records.Count);
        }

        return results;
    }

    private static string Pluralize(string entityLogical)
        => entityLogical.EndsWith("s") ? entityLogical + "es" : entityLogical + "s";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
