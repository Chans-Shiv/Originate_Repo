using Microsoft.Xrm.Sdk;

namespace OrigenateFunction.Infrastructure.Dataverse;

// Projects an Entity from one logical table to another by copying the requested attributes.
// Used to copy business fields from STG → HOLDING during the backup step.
public sealed class EntityProjector
{
    public Entity Project(Entity source, string targetLogicalName, IEnumerable<string> fields)
    {
        var e = new Entity(targetLogicalName);
        foreach (var f in fields)
            if (source.Contains(f) && source[f] is not null)
                e[f] = source[f];
        return e;
    }

    public string? ExtractString(Entity source, string field)
        => source.Contains(field) ? source[field]?.ToString() : null;
}
