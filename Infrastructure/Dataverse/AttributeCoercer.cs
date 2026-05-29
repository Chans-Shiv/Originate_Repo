using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace OrigenateFunction.Infrastructure.Dataverse;

// Coerces a raw cell value (typically a string from Excel) into the SDK type
// expected by the target attribute. Returns false when the value can't be parsed —
// caller decides whether to skip the field or log a warning.
public sealed class AttributeCoercer
{
    public bool TryCoerce(AttributeMetadata meta, object? raw, out object? coerced, out string? error)
    {
        coerced = null;
        error = null;

        if (raw is null) return true;
        var s = raw as string ?? raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return true;
        s = s.Trim();

        switch (meta.AttributeType)
        {
            case AttributeTypeCode.String:
            case AttributeTypeCode.Memo:
            case AttributeTypeCode.EntityName:
                coerced = s;
                return true;

            case AttributeTypeCode.Integer:
                if (int.TryParse(StripNumeric(s), NumberStyles.Integer | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var i))
                { coerced = i; return true; }
                error = "not an integer";
                return false;

            case AttributeTypeCode.BigInt:
                if (long.TryParse(StripNumeric(s), NumberStyles.Integer | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var l))
                { coerced = l; return true; }
                error = "not a bigint";
                return false;

            case AttributeTypeCode.Decimal:
                if (decimal.TryParse(StripNumeric(s), NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                        CultureInfo.InvariantCulture, out var d))
                { coerced = d; return true; }
                error = "not a decimal";
                return false;

            case AttributeTypeCode.Double:
                if (double.TryParse(StripNumeric(s), NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out var dd))
                { coerced = dd; return true; }
                error = "not a double";
                return false;

            case AttributeTypeCode.Money:
                if (decimal.TryParse(StripNumeric(s), NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                        CultureInfo.InvariantCulture, out var m))
                { coerced = new Money(m); return true; }
                error = "not money";
                return false;

            case AttributeTypeCode.Boolean:
                if (TryParseBool(s, out var b)) { coerced = b; return true; }
                error = "not a boolean";
                return false;

            case AttributeTypeCode.DateTime:
                if (TryParseDate(s, out var dt)) { coerced = dt; return true; }
                error = "not a date";
                return false;

            case AttributeTypeCode.Picklist:
            case AttributeTypeCode.State:
            case AttributeTypeCode.Status:
                if (TryParseOptionSet(s, meta, out var opt)) { coerced = new OptionSetValue(opt); return true; }
                error = "no matching option for picklist";
                return false;

            case AttributeTypeCode.Uniqueidentifier:
                if (Guid.TryParse(s, out var g)) { coerced = g; return true; }
                error = "not a guid";
                return false;

            default:
                // Lookup / Customer / Owner / Virtual / etc. — pass through as string;
                // a true reference would need additional metadata (target table + key).
                coerced = s;
                return true;
        }
    }

    private static string StripNumeric(string s)
        => s.Replace("$", "").Replace(",", "").Replace("%", "").Trim();

    private static bool TryParseBool(string s, out bool b)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "true": case "yes": case "y": case "1": case "t":
                b = true; return true;
            case "false": case "no": case "n": case "0": case "f":
                b = false; return true;
        }
        return bool.TryParse(s, out b);
    }

    private static bool TryParseDate(string s, out DateTime dt)
    {
        // Try Excel cell serial number (days since 1900-01-00 quirk)
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && serial > 0 && serial < 200_000)
        {
            dt = DateTime.SpecifyKind(new DateTime(1899, 12, 30).AddDays(serial), DateTimeKind.Utc);
            return true;
        }
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            return true;
        if (DateTime.TryParse(s, CultureInfo.CurrentCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
            return true;
        dt = default;
        return false;
    }

    private static bool TryParseOptionSet(string s, AttributeMetadata meta, out int value)
    {
        // Numeric form first
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        if (meta is EnumAttributeMetadata eam && eam.OptionSet?.Options is { } opts)
        {
            foreach (var o in opts)
            {
                var label = o.Label?.UserLocalizedLabel?.Label;
                if (!string.IsNullOrEmpty(label) && string.Equals(label, s, StringComparison.OrdinalIgnoreCase))
                {
                    value = o.Value ?? 0;
                    return o.Value.HasValue;
                }
            }
        }
        value = 0;
        return false;
    }
}
