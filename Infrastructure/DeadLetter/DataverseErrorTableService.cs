using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using OrigenateFunction.Diagnostics;
using OrigenateFunction.Domain.Entities;
using OrigenateFunction.Domain.Models;
using OrigenateFunction.Infrastructure.Dataverse;

namespace OrigenateFunction.Infrastructure.DeadLetter;

// Writes a single failed record (delivered via Storage Queue) into the Dataverse
// error table. Exceptions are intentionally NOT caught — the QueueTrigger runtime
// requeues the message on any thrown exception. After maxDequeueCount delivery
// attempts the give-up branch in DeadLetterProcessorFunction archives the message
// to blob.
//
// The schema (dmt_stg_origenate_errortable) only has columns for AccountNumber,
// ApplicationNumber, ErrorMessage, InvocationId, and Process. Row-level diagnostics
// without a dedicated column (source blob name, row number, enqueue time) are
// prepended to the ErrorMessage body so an operator looking at Dataverse alone has
// enough context to triage. The full structured payload remains in the queue
// message + give-up archive blob.
//
// dmt_ID is intentionally NOT set — it's expected to be an auto-number / primary
// key populated by Dataverse on insert.
public sealed class DataverseErrorTableService
{
    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly ILogger<DataverseErrorTableService> _log;

    public DataverseErrorTableService(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        ILogger<DataverseErrorTableService> log)
    {
        _factory = factory;
        _resilience = resilience;
        _log = log;
    }

    public async Task WriteOneAsync(DeadLetterMessage msg, CancellationToken ct = default)
    {
        var entity = new Entity(ColumnMap.ErrorTableEntityLogical);

        if (!string.IsNullOrWhiteSpace(msg.AccountNumber))
            entity[ColumnMap.ErrorTable_AccountNumber] = msg.AccountNumber;

        if (!string.IsNullOrWhiteSpace(msg.ApplicationNumber))
            entity[ColumnMap.ErrorTable_ApplicationNumber] = msg.ApplicationNumber;

        entity[ColumnMap.ErrorTable_ErrorMessage] = EnrichErrorMessage(msg);
        entity[ColumnMap.ErrorTable_InvocationId] = msg.InvocationId;
        entity[ColumnMap.ErrorTable_Process]      = string.IsNullOrWhiteSpace(msg.StepName) ? "Pipeline" : msg.StepName;

        var req = new CreateRequest { Target = entity };
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            await _factory.Client.ExecuteAsync(req, token);
        }, ct);

        _log.LogInformation(
            "EventName={EventName} Blob={Blob} Step={Step} Row={Row} Account={Account} Table={Table}",
            LogEvents.ErrorTableWritten, msg.SourceBlobName, msg.StepName, msg.RowNumber,
            msg.AccountNumber ?? "", ColumnMap.ErrorTableEntityLogical);
    }

    // Front-loads the context fields the schema doesn't carry. Length-capped to
    // ~3500 chars to stay well under the default Dataverse multi-line text limit
    // (4000) — the original error gets truncated rather than the prefix, so
    // identifying info is never lost.
    private static string EnrichErrorMessage(DeadLetterMessage msg)
    {
        const int MaxLen = 3500;
        var prefix = $"[Blob={msg.SourceBlobName} Row={msg.RowNumber} EnqueuedAt={msg.EnqueuedAt:O}] ";
        var body = msg.ErrorMessage ?? "";

        var combined = prefix + body;
        return combined.Length <= MaxLen ? combined : combined[..MaxLen] + "…";
    }
}
