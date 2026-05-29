using Microsoft.Xrm.Sdk.Query;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

public static class InFilterBuilder
{
    public static FilterExpression Build(string attribute, IEnumerable<string> values)
    {
        var arr = values.Cast<object>().ToArray();
        var f = new FilterExpression();
        f.Conditions.Add(new ConditionExpression(attribute, ConditionOperator.In, arr));
        return f;
    }
}
