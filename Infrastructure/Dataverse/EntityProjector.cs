using Microsoft.Xrm.Sdk;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

// Projects an Entity from one logical table to another by copying the requested
// attributes. Used to copy business fields from STG → HOLDING during the backup
// step.
//
// When STG and HOLDING use different logical names for the same business field
// (e.g. STG's dmt_creditscoreb1 vs HOLDING's dmt_primarycreditscore), pass a
// fieldRenames dictionary keyed by source-field name. Fields not in the map are
// copied with the source's logical name unchanged.
public sealed class EntityProjector
{
    public Entity Project(
        Entity source,
        string targetLogicalName,
        IEnumerable<string> fields,
        IReadOnlyDictionary<string, string>? fieldRenames = null)
    {
        var e = new Entity(targetLogicalName);
        foreach (var f in fields)
        {
            if (!source.Contains(f) || source[f] is null) continue;
            var targetField = fieldRenames is not null && fieldRenames.TryGetValue(f, out var renamed)
                ? renamed
                : f;
            e[targetField] = source[f];
        }
        return e;
    }

    public string? ExtractString(Entity source, string field)
        => source.Contains(field) ? source[field]?.ToString() : null;
}
