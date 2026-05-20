using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class BulkDeleter : IBulkDeleter
{
    private readonly IDataverseGateway _gw;
    private readonly ILogger<BulkDeleter> _log;

    public BulkDeleter(IDataverseGateway gw, ILogger<BulkDeleter> log)
    {
        _gw = gw;
        _log = log;
    }

    public async Task DeleteBatchAsync(string entitySet, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var builder = new MultipartBatchBuilder(_gw.BaseAddress);
        foreach (var id in ids) builder.AddDelete(entitySet, id);

        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Post, "$batch", ct);
        req.Content = builder.Build();

        using var res = await _gw.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Batch delete failed: {Status} {Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException($"Batch delete failed: {(int)res.StatusCode}");
        }
    }
}
