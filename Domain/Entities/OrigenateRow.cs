using Microsoft.Xrm.Sdk;

namespace OrigenateFunction.Domain.Entities;

public sealed class OrigenateRow
{
    public int RowNumber { get; init; }
    public string? ApplicationNumber { get; set; }
    public Dictionary<string, object?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Build the payload for STG_ORIGENATE — excludes columns that belong to
    // STG_ORIGENATE_EXCEPTIONS only (PolicyExceptions, PolicyExceptionsReason).
    public Entity ToDataverseEntity()
    {
        var e = new Entity(ColumnMap.StgOrigenateEntityLogical);
        foreach (var kv in Fields)
        {
            if (ColumnMap.ExcludedFromStg.Contains(kv.Key)) continue;
            if (kv.Value is null) continue;
            e[kv.Key] = kv.Value;
        }
        if (!string.IsNullOrWhiteSpace(ApplicationNumber))
            e[ColumnMap.ApplicationNumberField] = ApplicationNumber;
        return e;
    }
}
