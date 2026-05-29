using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using OrigenateFunction.Diagnostics;
using OrigenateFunction.Domain.Entities;
using OrigenateFunction.Domain.Interfaces;
using OrigenateFunction.Domain.Models;

namespace OrigenateFunction.Infrastructure.DeadLetter;

// Durable buffer in front of the Dataverse error table.
//
// Why a queue instead of writing to Dataverse directly:
//   A transient Dataverse outage at the moment we record a failure would silently
//   drop recovery context. Buffering each failure on a Storage Queue makes the
//   path retryable end-to-end: the QueueTrigger function reads, attempts the
//   insert, and on any exception lets the runtime requeue. After N delivery
//   attempts (host.json extensions.queues.maxDequeueCount) we ourselves archive
//   the record to blob — nothing is ever silently lost.
//
// One queue message per failed row (deliberate):
//   Each retry stays atomic — a transient Dataverse failure on row 47 of 100 only
//   replays row 47. Storage Queue is cheap enough that even 10k messages/day is
//   well under a cent.
public sealed class QueueDeadLetterService : IDeadLetterService
{
    private readonly QueueClient _queue;
    private readonly ILogger<QueueDeadLetterService> _log;
    private int _queueEnsured;

    public QueueDeadLetterService(QueueClient queue, ILogger<QueueDeadLetterService> log)
    {
        _queue = queue;
        _log = log;
    }

    public async Task WriteAsync(
        string sourceBlobName,
        string invocationId,
        string stepName,
        IReadOnlyList<FailedRow> failures,
        CancellationToken ct = default)
    {
        if (failures.Count == 0) return;

        // CreateIfNotExistsAsync is idempotent but we still skip it after the first
        // successful create per process — saves a round-trip on every batch.
        if (Interlocked.Exchange(ref _queueEnsured, 1) == 0)
            await _queue.CreateIfNotExistsAsync(cancellationToken: ct);

        var now = DateTimeOffset.UtcNow;
        int enqueued = 0, errored = 0;

        foreach (var f in failures)
        {
            // Pull identifiers from the source row's already-coerced field dictionary.
            // ApplicationNumber comes from OrigenateRow's dedicated property
            // (PolicyExceptions and similar rows go through a different builder path
            // and won't have this populated). LoanApplicationId stays null because
            // it's only known once Dataverse has assigned one — pre-insert failures
            // can't know it.
            f.Source.Fields.TryGetValue(ColumnMap.PublisherPrefix + "accountnumber", out var accountNumberObj);

            var msg = new DeadLetterMessage
            {
                SourceBlobName = sourceBlobName,
                InvocationId = invocationId,
                StepName = stepName,
                RowNumber = f.RowNumber,
                AccountNumber = accountNumberObj?.ToString(),
                ApplicationNumber = f.Source.ApplicationNumber,
                LoanApplicationId = null,
                ErrorMessage = f.Error,
                EnqueuedAt = now,
            };

            var json = JsonSerializer.Serialize(msg);

            try
            {
                await _queue.SendMessageAsync(json, ct);
                enqueued++;

                _log.LogInformation(
                    "EventName={EventName} Blob={Blob} Step={Step} Row={Row} Account={Account} InvocationId={InvocationId}",
                    LogEvents.DeadLetterEnqueued, sourceBlobName, stepName,
                    msg.RowNumber, msg.AccountNumber ?? "", invocationId);
            }
            catch (Exception ex)
            {
                errored++;

                // Queue enqueue failed — last-ditch AI log so the record is at least
                // recorded somewhere. The producer pipeline doesn't retry; this is
                // the boundary at which durability gives up.
                _log.LogError(ex,
                    "EventName={EventName} Blob={Blob} Step={Step} Row={Row} Error={Error}",
                    LogEvents.DeadLetterEnqueueFailed, sourceBlobName, stepName,
                    msg.RowNumber, msg.ErrorMessage);
            }
        }

        _log.LogInformation(
            "EventName={EventName} Blob={Blob} Step={Step} Total={Total} Enqueued={Enqueued} Errored={Errored}",
            LogEvents.DeadLetterBatchEnqueued, sourceBlobName, stepName,
            failures.Count, enqueued, errored);
    }
}
