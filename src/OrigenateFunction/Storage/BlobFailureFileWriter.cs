using System.Text;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Storage;

public sealed class BlobFailureFileWriter : IFailureFileWriter
{
    private readonly ContainerClientFactory _containers;
    private readonly ILogger<BlobFailureFileWriter> _log;

    public BlobFailureFileWriter(ContainerClientFactory containers, ILogger<BlobFailureFileWriter> log)
    {
        _containers = containers;
        _log = log;
    }

    public async Task WriteAsync(string sourceXlsxName, IReadOnlyList<FailedRow> failed, CancellationToken ct)
    {
        if (failed.Count == 0) return;

        var headers = failed
            .SelectMany(f => f.Source.Fields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var ms = new MemoryStream();
        using (var sw = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true))
        {
            sw.Write("RowNumber,");
            sw.Write(string.Join(",", headers.Select(Csv)));
            sw.WriteLine(",Error");

            foreach (var f in failed)
            {
                sw.Write(f.RowNumber);
                sw.Write(',');
                foreach (var h in headers)
                {
                    f.Source.Fields.TryGetValue(h, out var v);
                    sw.Write(Csv(v?.ToString() ?? ""));
                    sw.Write(',');
                }
                sw.WriteLine(Csv(f.Error));
            }
        }
        ms.Position = 0;

        var stem = Path.GetFileNameWithoutExtension(sourceXlsxName);
        var name = $"{stem}-failures-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var dst = _containers.Failed().GetBlobClient(name);
        await dst.UploadAsync(ms, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/csv" }
        }, ct);

        _log.LogInformation("Uploaded failure file {Name} ({Rows} rows)", name, failed.Count);
    }

    private static string Csv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
