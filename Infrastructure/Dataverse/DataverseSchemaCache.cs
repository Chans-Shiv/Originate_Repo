using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

// One async fetch per table for the lifetime of the host; subsequent callers
// re-use the cached Task. Keys = entity logical name (case-insensitive).
public sealed class DataverseSchemaCache
{
    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly ILogger<DataverseSchemaCache> _log;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyDictionary<string, AttributeMetadata>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public DataverseSchemaCache(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        ILogger<DataverseSchemaCache> log)
    {
        _factory = factory;
        _resilience = resilience;
        _log = log;
    }

    public Task<IReadOnlyDictionary<string, AttributeMetadata>> GetAttributesAsync(
        string entityLogicalName, CancellationToken ct)
        => _cache.GetOrAdd(entityLogicalName, name => LoadAsync(name, ct));

    private async Task<IReadOnlyDictionary<string, AttributeMetadata>> LoadAsync(
        string entityLogicalName, CancellationToken ct)
    {
        _log.LogInformation("EventName=SchemaLoadStart Entity={Logical}", entityLogicalName);

        RetrieveEntityResponse? resp = null;
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            resp = (RetrieveEntityResponse)await _factory.Client.ExecuteAsync(
                new RetrieveEntityRequest
                {
                    LogicalName = entityLogicalName,
                    EntityFilters = EntityFilters.Attributes,
                    RetrieveAsIfPublished = true
                }, token);
        }, ct);

        var attrs = resp?.EntityMetadata?.Attributes ?? Array.Empty<AttributeMetadata>();
        var dict = attrs
            .Where(a => !string.IsNullOrEmpty(a.LogicalName))
            .ToDictionary(a => a.LogicalName!, a => a, StringComparer.OrdinalIgnoreCase);

        _log.LogInformation("EventName=SchemaLoad Entity={Logical} AttrCount={Count}",
            entityLogicalName, dict.Count);
        return dict;
    }
}
