using System.Runtime.CompilerServices;
using System.Text.Json;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class PagedReader : IPagedReader
{
    private readonly IDataverseGateway _gw;
    public PagedReader(IDataverseGateway gw) => _gw = gw;

    public async IAsyncEnumerable<JsonElement> RetrieveAllAsync(
        string entitySet, string? filter, IEnumerable<string> select, int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{entitySet}?$select={string.Join(",", select)}";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";

        while (!string.IsNullOrEmpty(url))
        {
            using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Get, url, ct);
            req.Headers.Add("Prefer", $"odata.maxpagesize={pageSize}");
            using var res = await _gw.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
                yield return item.Clone();
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? AbsoluteToRelative(nl.GetString(), _gw.BaseAddress) : null;
        }
    }

    public async Task<long> CountAsync(string entitySet, string? filter, CancellationToken ct)
    {
        var url = $"{entitySet}?$count=true&$top=0";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";
        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Get, url, ct);
        using var res = await _gw.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("@odata.count", out var c) ? c.GetInt64() : 0;
    }

    private static string? AbsoluteToRelative(string? absolute, Uri baseAddress)
    {
        if (string.IsNullOrEmpty(absolute)) return null;
        return Uri.TryCreate(absolute, UriKind.Absolute, out var u)
            ? u.PathAndQuery.Replace(baseAddress.AbsolutePath, "")
            : absolute;
    }
}
