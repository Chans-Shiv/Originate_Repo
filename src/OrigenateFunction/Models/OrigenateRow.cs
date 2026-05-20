namespace OrigenateFunction.Models;

public sealed class OrigenateRow
{
    public int RowNumber { get; init; }
    public string? ApplicationNumber { get; set; }
    public Dictionary<string, object?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, object?> ToDataverseEntity()
    {
        var d = new Dictionary<string, object?>(Fields, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(ApplicationNumber))
            d[ColumnMap.ApplicationNumberField] = ApplicationNumber;
        return d;
    }
}
