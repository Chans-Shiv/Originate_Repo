using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrigenateFunction.Services;

public sealed class DataverseClient
{
    private readonly HttpClient _http;
    private readonly DataverseTokenProvider _tokens;
    private readonly ILogger<DataverseClient> _log;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };

    public DataverseClient(DataverseTokenProvider tokens, IConfiguration cfg, ILogger<DataverseClient> log)
    {
        _tokens = tokens;
        _log = log;
        var baseUrl = (cfg["DataverseUrl"] ?? throw new InvalidOperationException("DataverseUrl missing"))
            .TrimEnd('/') + "/api/data/v9.2/";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        _http.DefaultRequestHeaders.Add("OData-Version", "4.0");
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var req = new HttpRequestMessage(method, path);
        var token = await _tokens.GetTokenAsync(ct);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    public async Task<string> WhoAmIAsync(CancellationToken ct)
    {
        using var req = await NewRequestAsync(HttpMethod.Get, "WhoAmI", ct);
        using var res = await SendWithRetryAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("UserId").GetString() ?? "";
    }

    // -------- Paged retrieve --------
    public async IAsyncEnumerable<JsonElement> RetrieveAllAsync(
        string entitySet,
        string? filter,
        IEnumerable<string> select,
        int pageSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{entitySet}?$select={string.Join(",", select)}";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";

        while (!string.IsNullOrEmpty(url))
        {
            using var req = await NewRequestAsync(HttpMethod.Get, url, ct);
            req.Headers.Add("Prefer", $"odata.maxpagesize={pageSize}");
            using var res = await SendWithRetryAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
                yield return item.Clone();
            url = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl)
                ? AbsoluteToRelative(nl.GetString())
                : null;
        }
    }

    public async Task<long> CountAsync(string entitySet, string? filter, CancellationToken ct)
    {
        var url = $"{entitySet}?$count=true&$top=0";
        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&$filter={Uri.EscapeDataString(filter)}";
        using var req = await NewRequestAsync(HttpMethod.Get, url, ct);
        using var res = await SendWithRetryAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("@odata.count", out var c) ? c.GetInt64() : 0;
    }

    // -------- CreateMultiple --------
    // Returns per-row results: success flag + error message for the failed ones.
    public async Task<IReadOnlyList<RowResult>> CreateMultipleAsync(
        string entityLogicalName,
        IReadOnlyList<IDictionary<string, object?>> records,
        CancellationToken ct)
    {
        if (records.Count == 0) return Array.Empty<RowResult>();

        var targets = records.Select(r =>
        {
            var d = new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)
            {
                ["@odata.type"] = "Microsoft.Dynamics.CRM." + entityLogicalName
            };
            return d;
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["Targets"] = targets
        };
        var json = JsonSerializer.Serialize(payload, Json);

        using var req = await NewRequestAsync(HttpMethod.Post,
            $"{Pluralize(entityLogicalName)}/Microsoft.Dynamics.CRM.CreateMultiple", ct);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var res = await SendWithRetryAsync(req, ct);
        try
        {
            if (res.IsSuccessStatusCode)
                return records.Select(_ => RowResult.Ok()).ToArray();

            // Fall back to per-row $batch so we know which rows failed.
            return await BatchCreateAsync(entityLogicalName, records, ct);
        }
        finally { res.Dispose(); }
    }

    // -------- Per-row $batch fallback (used when CreateMultiple rejects with row faults) --------
    public async Task<IReadOnlyList<RowResult>> BatchCreateAsync(
        string entityLogicalName,
        IReadOnlyList<IDictionary<string, object?>> records,
        CancellationToken ct)
    {
        var results = new RowResult[records.Count];
        var entitySet = Pluralize(entityLogicalName);
        var batchId = $"batch_{Guid.NewGuid():N}";
        var changesetId = $"changeset_{Guid.NewGuid():N}";

        var sb = new StringBuilder();
        for (int i = 0; i < records.Count; i++)
        {
            sb.Append("--").Append(batchId).Append("\r\n");
            sb.Append("Content-Type: multipart/mixed; boundary=").Append(changesetId).Append("\r\n\r\n");
            sb.Append("--").Append(changesetId).Append("\r\n");
            sb.Append("Content-Type: application/http\r\n");
            sb.Append("Content-Transfer-Encoding: binary\r\n");
            sb.Append("Content-ID: ").Append(i + 1).Append("\r\n\r\n");
            sb.Append("POST ").Append(_http.BaseAddress).Append(entitySet).Append(" HTTP/1.1\r\n");
            sb.Append("Content-Type: application/json\r\n\r\n");
            sb.Append(JsonSerializer.Serialize(records[i], Json)).Append("\r\n");
            sb.Append("--").Append(changesetId).Append("--\r\n");
        }
        sb.Append("--").Append(batchId).Append("--\r\n");

        using var req = await NewRequestAsync(HttpMethod.Post, "$batch", ct);
        req.Content = new StringContent(sb.ToString(), Encoding.UTF8);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/mixed");
        req.Content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", batchId));

        using var res = await SendWithRetryAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        ParseBatchResponse(body, results);
        return results;
    }

    private static void ParseBatchResponse(string body, RowResult[] results)
    {
        var blocks = body.Split(new[] { "Content-ID:" }, StringSplitOptions.None);
        for (int i = 1; i < blocks.Length; i++)
        {
            var blk = blocks[i];
            var idEnd = blk.IndexOf("\r\n", StringComparison.Ordinal);
            if (idEnd <= 0) continue;
            if (!int.TryParse(blk.AsSpan(0, idEnd).Trim(), out var contentId)) continue;
            var idx = contentId - 1;
            if (idx < 0 || idx >= results.Length) continue;

            var statusIdx = blk.IndexOf("HTTP/1.1 ", StringComparison.Ordinal);
            if (statusIdx < 0) { results[idx] = RowResult.Fail("Unparseable response"); continue; }
            var statusStr = blk.AsSpan(statusIdx + 9, 3);
            var ok = statusStr.StartsWith("20") || statusStr.StartsWith("204");
            if (ok) { results[idx] = RowResult.Ok(); continue; }

            var bodyStart = blk.IndexOf("\r\n\r\n", statusIdx, StringComparison.Ordinal);
            var msg = bodyStart > 0 ? blk[(bodyStart + 4)..].Trim() : "HTTP " + statusStr.ToString();
            results[idx] = RowResult.Fail(Truncate(msg, 500));
        }
        for (int i = 0; i < results.Length; i++)
            results[i] ??= RowResult.Fail("Missing response");
    }

    // -------- Bulk delete via $batch --------
    public async Task DeleteBatchAsync(string entitySet, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        var batchId = $"batch_{Guid.NewGuid():N}";
        var changesetId = $"changeset_{Guid.NewGuid():N}";

        var sb = new StringBuilder();
        for (int i = 0; i < ids.Count; i++)
        {
            sb.Append("--").Append(batchId).Append("\r\n");
            sb.Append("Content-Type: multipart/mixed; boundary=").Append(changesetId).Append("\r\n\r\n");
            sb.Append("--").Append(changesetId).Append("\r\n");
            sb.Append("Content-Type: application/http\r\n");
            sb.Append("Content-Transfer-Encoding: binary\r\n");
            sb.Append("Content-ID: ").Append(i + 1).Append("\r\n\r\n");
            sb.Append("DELETE ").Append(_http.BaseAddress).Append(entitySet)
              .Append('(').Append(ids[i]).Append(") HTTP/1.1\r\n\r\n");
            sb.Append("--").Append(changesetId).Append("--\r\n");
        }
        sb.Append("--").Append(batchId).Append("--\r\n");

        using var req = await NewRequestAsync(HttpMethod.Post, "$batch", ct);
        req.Content = new StringContent(sb.ToString(), Encoding.UTF8);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("multipart/mixed");
        req.Content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", batchId));

        using var res = await SendWithRetryAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var b = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Batch delete failed: {(int)res.StatusCode} {b}");
        }
    }

    // -------- In() filter helper --------
    public static string BuildInFilter(string field, IEnumerable<string> values)
    {
        var quoted = string.Join(",", values.Select(v => "'" + v.Replace("'", "''") + "'"));
        return $"Microsoft.Dynamics.CRM.In(PropertyName='{field}',PropertyValues=[{quoted}])";
    }

    // -------- HTTP send with retry on 429/503 --------
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage res;
            try
            {
                res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex) when (attempt < 5)
            {
                _log.LogWarning(ex, "Transient HTTP failure (attempt {Attempt}); retrying in {Delay}s", attempt, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
                req = await CloneAsync(req);
                continue;
            }

            if ((int)res.StatusCode != 429 && res.StatusCode != HttpStatusCode.ServiceUnavailable)
                return res;
            if (attempt >= 5) return res;

            var retryAfter = res.Headers.RetryAfter?.Delta ?? delay;
            res.Dispose();
            _log.LogWarning("Dataverse throttled (HTTP {Status}); backing off {Sec}s (attempt {Attempt})",
                (int)res.StatusCode, retryAfter.TotalSeconds, attempt);
            await Task.Delay(retryAfter, ct);
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
            req = await CloneAsync(req);
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage src)
    {
        var clone = new HttpRequestMessage(src.Method, src.RequestUri);
        foreach (var h in src.Headers) clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (src.Content is not null)
        {
            var bytes = await src.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in src.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return clone;
    }

    private string? AbsoluteToRelative(string? absolute)
    {
        if (string.IsNullOrEmpty(absolute)) return null;
        if (Uri.TryCreate(absolute, UriKind.Absolute, out var u))
            return _http.BaseAddress is null ? absolute : u.PathAndQuery.Replace(_http.BaseAddress.AbsolutePath, "");
        return absolute;
    }

    private static string Pluralize(string entityLogical)
        => entityLogical.EndsWith("s") ? entityLogical + "es" : entityLogical + "s";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public sealed record RowResult(bool Success, string? Error)
    {
        public static RowResult Ok() => new(true, null);
        public static RowResult Fail(string err) => new(false, err);
    }
}
