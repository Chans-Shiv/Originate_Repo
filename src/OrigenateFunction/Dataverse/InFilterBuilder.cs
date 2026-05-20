namespace OrigenateFunction.Dataverse;

public static class InFilterBuilder
{
    public static string Build(string field, IEnumerable<string> values)
    {
        var quoted = string.Join(",", values.Select(v => "'" + v.Replace("'", "''") + "'"));
        return $"Microsoft.Dynamics.CRM.In(PropertyName='{field}',PropertyValues=[{quoted}])";
    }
}
