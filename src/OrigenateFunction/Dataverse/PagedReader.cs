using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class PagedReader : IPagedReader
{
    private readonly IDataverseGateway _gw;
    private readonly ILogger<PagedReader> _log;

    public PagedReader(IDataverseGateway gw, ILogger<PagedReader> log)
    {
        _gw = gw;
        _log = log;
    }

    public async IAsyncEnumerable<JsonElement> RetrieveAllAsync(
        string entitySet, string? filter, IEnumerable<string> select, int pageSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{entitySet}?$select={string.Join(",", select)}";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";

        _log.LogInformation("RetrieveAll → GET {Url} (pageSize={Page})", url, pageSize);

        long page = 0, totalRows = 0;
        while (!string.IsNullOrEmpty(url))
        {
            page++;
            using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Get, url, ct);
            req.Headers.Add("Prefer", $"odata.maxpagesize={pageSize}");
            using var res = await _gw.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync(ct);
                _log.LogError("RetrieveAll ✗ GET {Url} {Status}. Body: {Body}",
                    url, (int)res.StatusCode, Truncate(errBody, 1000));
                res.EnsureSuccessStatusCode();
            }
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            long pageRows = 0;
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                pageRows++;
                yield return item.Clone();
            }
            totalRows += pageRows;
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? AbsoluteToRelative(nl.GetString(), _gw.BaseAddress) : null;
            _log.LogInformation("RetrieveAll page {Page}: {Rows} rows (running total {Total}, hasNext={HasNext})",
                page, pageRows, totalRows, !string.IsNullOrEmpty(url));
        }
        _log.LogInformation("RetrieveAll ✓ {EntitySet}: {Total} rows across {Pages} pages",
            entitySet, totalRows, page);
    }

    public async Task<long> CountAsync(string entitySet, string? filter, CancellationToken ct)
    {
        var url = $"{entitySet}?$count=true&$top=0";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";

        _log.LogInformation("Count → GET {Url}", url);

        using var req = await _gw.CreateAuthorizedRequestAsync(HttpMethod.Get, url, ct);
        using var res = await _gw.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var errBody = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Count ✗ GET {Url} {Status}. Body: {Body}",
                url, (int)res.StatusCode, Truncate(errBody, 1000));
            res.EnsureSuccessStatusCode();
        }
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var count = doc.RootElement.TryGetProperty("@odata.count", out var c) ? c.GetInt64() : 0;
        _log.LogInformation("Count ✓ {Url}: {Count}", url, count);
        return count;
    }

    private static string? AbsoluteToRelative(string? absolute, Uri baseAddress)
    {
        if (string.IsNullOrEmpty(absolute)) return null;
        return Uri.TryCreate(absolute, UriKind.Absolute, out var u)
            ? u.PathAndQuery.Replace(baseAddress.AbsolutePath, "")
            : absolute;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
