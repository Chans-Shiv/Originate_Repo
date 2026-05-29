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
// Field-name mapping comes from ColumnMap.ErrorTable_* constants, which are
// placeholders until the real Dataverse schema is finalized. While they still
// contain "<<...>>" markers, WriteOneAsync logs and short-circuits instead of
// attempting an insert against a non-existent entity — that keeps the queue
// buffering the messages until you fill in the real names.
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
        if (!ColumnMap.IsErrorTableConfigured)
        {
            // Throw so the QueueTrigger requeues the message. By the time the schema
            // is filled in and the host restarts, the queue still holds the buffered
            // failures and they'll flow through cleanly. We log at Warning (not Error)
            // because this is an expected pre-launch state, not a regression.
            _log.LogWarning(
                "EventName={EventName} Blob={Blob} Row={Row} Reason=ErrorTablePlaceholdersStillPresent",
                LogEvents.ErrorTableNotConfigured, msg.SourceBlobName, msg.RowNumber);
            throw new InvalidOperationException(
                "Dataverse error-table schema not configured — ColumnMap.ErrorTable_* constants " +
                "still contain '<<...>>' placeholders. Fill them in with real logical names and " +
                "restart the host to drain the dead-letter queue.");
        }

        var entity = new Entity(ColumnMap.ErrorTableEntityLogical);

        if (!string.IsNullOrWhiteSpace(msg.AccountNumber))
            entity[ColumnMap.ErrorTable_AccountNumber] = msg.AccountNumber;

        if (msg.LoanApplicationId.HasValue)
            entity[ColumnMap.ErrorTable_LoanAppId] = msg.LoanApplicationId.Value;

        entity[ColumnMap.ErrorTable_RowNumber]      = msg.RowNumber;
        entity[ColumnMap.ErrorTable_StepName]       = msg.StepName;
        entity[ColumnMap.ErrorTable_ErrorMessage]   = msg.ErrorMessage;
        entity[ColumnMap.ErrorTable_InvocationId]   = msg.InvocationId;
        entity[ColumnMap.ErrorTable_SourceBlobName] = msg.SourceBlobName;
        entity[ColumnMap.ErrorTable_EnqueuedAt]     = msg.EnqueuedAt.UtcDateTime;

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
}
