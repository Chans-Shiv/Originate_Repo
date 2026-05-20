namespace OrigenateFunction.Models;

// Single source of truth for table + column names.
// TODO(user): replace placeholders with real publisher-prefixed logical names
// (e.g., "new_" or "cr1a3_") and confirm Excel header text exactly.
public static class ColumnMap
{
    public const string PublisherPrefix = "new_";

    public const string StgOrigenateEntityLogical = PublisherPrefix + "stg_origenate";
    public const string StgOrigenateEntitySet = PublisherPrefix + "stg_origenates";

    public const string HoldingEntityLogical = PublisherPrefix + "stg_origenate_holding";
    public const string HoldingEntitySet = PublisherPrefix + "stg_origenate_holdings";

    public const string ExceptionsEntityLogical = PublisherPrefix + "stg_origenate_exception";
    public const string ExceptionsEntitySet = PublisherPrefix + "stg_origenate_exceptions";

    // Primary key (GUID) field names — used by retrieve / delete batch
    public const string StgOrigenatePrimaryId = PublisherPrefix + "stg_origenateid";
    public const string HoldingPrimaryId = PublisherPrefix + "stg_origenate_holdingid";
    public const string ExceptionsPrimaryId = PublisherPrefix + "stg_origenate_exceptionid";

    // Business / mapped fields
    public const string ApplicationNumberField = PublisherPrefix + "applicationnumber";
    public const string PolicyExceptionsField = PublisherPrefix + "policyexceptions";
    public const string PolicyExceptionsReasonField = PublisherPrefix + "policyexceptionsreason";

    // Excel header → Dataverse logical column.
    // TODO(user): fill the remaining columns based on the Origenate Excel template.
    public static readonly IReadOnlyDictionary<string, string> ExcelHeaderToDataverse =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Application Number", ApplicationNumberField },
            { "Policy Exceptions", PolicyExceptionsField },
            { "Policy Exceptions Reason", PolicyExceptionsReasonField },
            // { "Customer Name", PublisherPrefix + "customername" },
            // { "Submission Date", PublisherPrefix + "submissiondate" },
        };

    // Subset of business fields copied to HOLDING. By default = all STG fields.
    public static readonly IReadOnlyList<string> StgBusinessFields =
        ExcelHeaderToDataverse.Values.Distinct().ToArray();
}
