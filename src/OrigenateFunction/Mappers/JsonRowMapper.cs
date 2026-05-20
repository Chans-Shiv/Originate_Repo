using System.Text.Json;

namespace OrigenateFunction.Mappers;

public sealed class JsonRowMapper
{
    public IDictionary<string, object?> MapJsonToRecord(JsonElement source, IEnumerable<string> fields)
    {
        var rec = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields)
            if (source.TryGetProperty(f, out var el) && el.ValueKind != JsonValueKind.Null)
                rec[f] = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
        return rec;
    }

    public string? ExtractString(JsonElement source, string field)
        => source.TryGetProperty(field, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
