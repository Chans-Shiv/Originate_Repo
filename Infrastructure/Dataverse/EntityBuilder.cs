using Microsoft.Extensions.Logging;
using OrigenateFunction.Domain.Models;
using Microsoft.Xrm.Sdk;
using OrigenateFunction.Infrastructure.Dataverse;
using OrigenateFunction.Domain.Entities;

namespace OrigenateFunction.Infrastructure.Dataverse;

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

    private void TrySet(
        Entity entity,
        IReadOnlyDictionary<string, Microsoft.Xrm.Sdk.Metadata.AttributeMetadata> attrs,
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
