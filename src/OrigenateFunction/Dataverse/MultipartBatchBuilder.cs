using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OrigenateFunction.Dataverse;

// Builder pattern: assemble Dataverse $batch multipart payloads cleanly.
public sealed class MultipartBatchBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    private readonly Uri _baseAddress;
    private readonly string _batchId = $"batch_{Guid.NewGuid():N}";
    private readonly string _changesetId = $"changeset_{Guid.NewGuid():N}";
    private readonly StringBuilder _body = new();
    private int _contentId;

    public MultipartBatchBuilder(Uri baseAddress) => _baseAddress = baseAddress;

    public MultipartBatchBuilder AddCreate(string entitySet, IDictionary<string, object?> record)
    {
        _contentId++;
        AppendChangesetHeader();
        _body.Append("POST ").Append(_baseAddress).Append(entitySet).Append(" HTTP/1.1\r\n");
        _body.Append("Content-Type: application/json\r\n\r\n");
        _body.Append(JsonSerializer.Serialize(record, JsonOpts)).Append("\r\n");
        _body.Append("--").Append(_changesetId).Append("--\r\n");
        return this;
    }

    public MultipartBatchBuilder AddDelete(string entitySet, Guid id)
    {
        _contentId++;
        AppendChangesetHeader();
        _body.Append("DELETE ").Append(_baseAddress).Append(entitySet)
             .Append('(').Append(id).Append(") HTTP/1.1\r\n\r\n");
        _body.Append("--").Append(_changesetId).Append("--\r\n");
        return this;
    }

    private void AppendChangesetHeader()
    {
        _body.Append("--").Append(_batchId).Append("\r\n");
        _body.Append("Content-Type: multipart/mixed; boundary=").Append(_changesetId).Append("\r\n\r\n");
        _body.Append("--").Append(_changesetId).Append("\r\n");
        _body.Append("Content-Type: application/http\r\n");
        _body.Append("Content-Transfer-Encoding: binary\r\n");
        _body.Append("Content-ID: ").Append(_contentId).Append("\r\n\r\n");
    }

    public int OperationCount => _contentId;

    public HttpContent Build()
    {
        _body.Append("--").Append(_batchId).Append("--\r\n");
        var content = new StringContent(_body.ToString(), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/mixed");
        content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", _batchId));
        return content;
    }
}

// Parses $batch responses keyed by Content-ID.
public static class MultipartBatchResponseParser
{
    public static void ApplyResults(string body, Abstractions.RowWriteResult[] results)
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
            if (statusIdx < 0)
            {
                results[idx] = Abstractions.RowWriteResult.Fail("Unparseable response");
                continue;
            }
            var statusStr = blk.AsSpan(statusIdx + 9, 3);
            var ok = statusStr.StartsWith("20") || statusStr.StartsWith("204");
            if (ok) { results[idx] = Abstractions.RowWriteResult.Ok(); continue; }

            var bodyStart = blk.IndexOf("\r\n\r\n", statusIdx, StringComparison.Ordinal);
            var msg = bodyStart > 0 ? blk[(bodyStart + 4)..].Trim() : "HTTP " + statusStr.ToString();
            results[idx] = Abstractions.RowWriteResult.Fail(Truncate(msg, 500));
        }
        for (int i = 0; i < results.Length; i++)
            results[i] ??= Abstractions.RowWriteResult.Fail("Missing response");
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
