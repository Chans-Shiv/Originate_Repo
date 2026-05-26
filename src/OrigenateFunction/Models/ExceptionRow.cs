using Microsoft.Xrm.Sdk;

namespace OrigenateFunction.Models;

public sealed record ExceptionRow(
    string ApplicationNumber,
    string? PolicyExceptions,
    string? PolicyExceptionsReason)
{
    public Entity ToDataverseEntity()
    {
        var e = new Entity(ColumnMap.ExceptionsEntityLogical)
        {
            [ColumnMap.ApplicationNumberField] = ApplicationNumber,
        };
        if (PolicyExceptions is not null)
            e[ColumnMap.PolicyExceptionsField] = PolicyExceptions;
        if (PolicyExceptionsReason is not null)
            e[ColumnMap.PolicyExceptionsReasonField] = PolicyExceptionsReason;
        return e;
    }
}
