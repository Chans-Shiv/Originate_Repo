namespace OrigenateFunction.Models;

public sealed record ExceptionRow(
    string ApplicationNumber,
    string? PolicyExceptions,
    string? PolicyExceptionsReason)
{
    public IDictionary<string, object?> ToDataverseEntity() => new Dictionary<string, object?>
    {
        [ColumnMap.ApplicationNumberField] = ApplicationNumber,
        [ColumnMap.PolicyExceptionsField] = PolicyExceptions,
        [ColumnMap.PolicyExceptionsReasonField] = PolicyExceptionsReason,
    };
}
