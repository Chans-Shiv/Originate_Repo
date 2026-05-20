using System.Text;
using OrigenateFunction.Models;

namespace OrigenateFunction.Services;

public sealed class FailureWriter
{
    private readonly BlobService _blobs;
    public FailureWriter(BlobService blobs) => _blobs = blobs;

    public sealed record FailedRow(int RowNumber, OrigenateRow Source, string Error);

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
        await _blobs.UploadFailureFileAsync(name, ms, "text/csv", ct);
    }

    private static string Csv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
