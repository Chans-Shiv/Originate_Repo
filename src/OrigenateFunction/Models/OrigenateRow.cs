namespace OrigenateFunction.Models;

public sealed class OrigenateRow
{
    public int RowNumber { get; init; }
    public string? ApplicationNumber { get; set; }
    public Dictionary<string, object?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Build the payload for STG_ORIGENATE — excludes columns that belong to
    // STG_ORIGENATE_EXCEPTIONS only (PolicyExceptions, PolicyExceptionsReason).
    public IDictionary<string, object?> ToDataverseEntity()
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in Fields)
        {
            if (ColumnMap.ExcludedFromStg.Contains(kv.Key)) continue;
            d[kv.Key] = kv.Value;
        }
        if (!string.IsNullOrWhiteSpace(ApplicationNumber))
            d[ColumnMap.ApplicationNumberField] = ApplicationNumber;
        return d;
    }
}
