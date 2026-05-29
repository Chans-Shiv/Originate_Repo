using Microsoft.Extensions.Logging;
using Fhn.Originate.FtbanknewSync.Domain.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;
using Fhn.Originate.FtbanknewSync.Domain.Entities;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

// Builds typed SDK Entity objects from row models, using the schema cache
// to look up each target column's AttributeMetadata and the coercer to
// convert raw cell values (strings from Excel) into the SDK types
// (OptionSetValue, Money, bool, DateTime, decimal, etc.).
public sealed class EntityBuilder
{
    private readonly DataverseSchemaCache _schema;
    private readonly AttributeCoercer _coercer;
    private readonly ILogger<EntityBuilder> _log;

    public EntityBuilder(DataverseSchemaCache schema, AttributeCoercer coercer, ILogger<EntityBuilder> log)
    {
        _schema = schema; _coercer = coercer; _log = log;
    }

    public async Task<Entity> BuildOrigenateRowAsync(
        OrigenateRow row, string targetLogicalName, CancellationToken ct)
    {
        var attrs = await _schema.GetAttributesAsync(targetLogicalName, ct);
        var e = new Entity(targetLogicalName);

        foreach (var kv in row.Fields)
        {
            if (ColumnMap.ExcludedFromStg.Contains(kv.Key)) continue;
            TrySet(e, attrs, kv.Key, kv.Value, row.RowNumber, targetLogicalName);
        }
        if (!string.IsNullOrWhiteSpace(row.ApplicationNumber))
            TrySet(e, attrs, ColumnMap.ApplicationNumberField, row.ApplicationNumber, row.RowNumber, targetLogicalName);
        return e;
    }

    public async Task<Entity> BuildExceptionRowAsync(ExceptionRow row, CancellationToken ct)
    {
        var attrs = await _schema.GetAttributesAsync(ColumnMap.ExceptionsEntityLogical, ct);
        var e = new Entity(ColumnMap.ExceptionsEntityLogical);
        TrySet(e, attrs, ColumnMap.ApplicationNumberField, row.ApplicationNumber, 0, ColumnMap.ExceptionsEntityLogical);
        TrySet(e, attrs, ColumnMap.PolicyExceptionsField, row.PolicyExceptions, 0, ColumnMap.ExceptionsEntityLogical);
        TrySet(e, attrs, ColumnMap.PolicyExceptionsReasonField, row.PolicyExceptionsReason, 0, ColumnMap.ExceptionsEntityLogical);
        return e;
    }

    // Pre-fetches a target table's attribute metadata (cached for the host's lifetime).
    // Callers that project many rows fetch once, then reuse for each ProjectCoerced call.
    public Task<IReadOnlyDictionary<string, AttributeMetadata>> GetAttributesAsync(
        string targetLogicalName, CancellationToken ct)
        => _schema.GetAttributesAsync(targetLogicalName, ct);

    // Projects a source Dataverse entity into the target table, re-coercing every value
    // to the TARGET column's type. Used when copying between STG and HOLDING, whose
    // columns share business meaning but can have different Dataverse types (e.g. an
    // Integer on one, Decimal/Money on the other) — a raw copy would throw
    // "Incorrect type of attribute value". Values read back from Dataverse arrive as
    // SDK wrappers (Money, OptionSetValue, …); Unwrap turns them back into the scalar
    // the coercer understands before re-coercing to the target type.
    //
    // fieldRenames maps source logical name → target logical name (null = same name).
    public Entity ProjectCoerced(
        Entity source,
        string targetLogicalName,
        IReadOnlyDictionary<string, AttributeMetadata> targetAttrs,
        IEnumerable<string> sourceFields,
        IReadOnlyDictionary<string, string>? fieldRenames)
    {
        var e = new Entity(targetLogicalName);
        foreach (var f in sourceFields)
        {
            if (!source.Contains(f) || source[f] is null) continue;
            var targetField = fieldRenames is not null && fieldRenames.TryGetValue(f, out var renamed)
                ? renamed
                : f;
            TrySet(e, targetAttrs, targetField, Unwrap(source[f]), 0, targetLogicalName);
        }
        return e;
    }

    // Turns Dataverse SDK wrapper types back into the scalar the coercer can re-parse.
    private static object? Unwrap(object? v) => v switch
    {
        Money m => m.Value,
        OptionSetValue o => o.Value,
        AliasedValue a => a.Value,
        _ => v
    };

    private void TrySet(
        Entity entity,
        IReadOnlyDictionary<string, AttributeMetadata> attrs,
        string field, object? raw, int rowNum, string logical)
    {
        if (raw is null) return;
        if (raw is string s && string.IsNullOrWhiteSpace(s)) return;

        if (!attrs.TryGetValue(field, out var meta))
        {
            _log.LogWarning(
                "EventName=UnknownAttribute Entity={Logical} Field={Field} Row={Row}",
                logical, field, rowNum);
            return;
        }

        if (_coercer.TryCoerce(meta, raw, out var coerced, out var err))
        {
            if (coerced is not null) entity[field] = coerced;
        }
        else
        {
            _log.LogWarning(
                "EventName=CoercionFailed Entity={Logical} Field={Field} Type={Type} Row={Row} Value={Value} Error={Err}",
                logical, field, meta.AttributeType, rowNum, raw, err);
        }
    }
}
