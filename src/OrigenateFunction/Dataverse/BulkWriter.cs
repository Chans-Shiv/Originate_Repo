using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Options;

namespace OrigenateFunction.Dataverse;

// Bulk insert via ExecuteMultipleRequest (CreateRequest, ContinueOnError).
// Two-stage retry: full batch → short delay → retry of failed-only records.
// Final per-record results are surfaced to the caller.
public sealed class BulkWriter : IBulkWriter
{
    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly ILogger<BulkWriter> _log;
    private readonly TimeSpan _retryDelay;

    public BulkWriter(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        IOptions<OrigenateOptions> opts,
        ILogger<BulkWriter> log)
    {
        _factory = factory;
        _resilience = resilience;
        _log = log;
        _retryDelay = TimeSpan.FromSeconds(Math.Max(1, opts.Value.RetryDelaySeconds));
    }

    public async Task<IReadOnlyList<RowWriteResult>> CreateMultipleAsync(
        IReadOnlyList<Entity> entities,
        CancellationToken ct)
    {
        if (entities.Count == 0) return Array.Empty<RowWriteResult>();
        var logical = entities[0].LogicalName;

        // Stage 1: full batch
        _log.LogInformation("EventName=BulkInsertBatchStart Entity={Logical} Count={Count}",
            logical, entities.Count);
        var stage1 = await ExecuteOnceAsync(entities, ct);

        if (stage1.Faults.Count == 0)
        {
            _log.LogInformation("EventName=BulkInsertBatch Entity={Logical} Count={Count} Failed=0",
                logical, entities.Count);
            return stage1.Results;
        }

        _log.LogWarning(
            "EventName=BulkInsertBatchPartial Entity={Logical} Count={Count} Failed={Failed} FirstError={Err}",
            logical, entities.Count, stage1.Faults.Count, Truncate(stage1.Faults[0].Error, 500));

        // Stage 2: retry only the failed records after a short delay
        await Task.Delay(_retryDelay, ct);

        var retryEntities = stage1.Faults.Select(f => entities[f.Index]).ToArray();
        _log.LogInformation("EventName=BulkInsertRetryStart Entity={Logical} Count={Count}",
            logical, retryEntities.Length);
        var stage2 = await ExecuteOnceAsync(retryEntities, ct);

        var final = (RowWriteResult[])stage1.Results;
        for (int i = 0; i < retryEntities.Length; i++)
        {
            var origIdx = stage1.Faults[i].Index;
            final[origIdx] = stage2.Results[i];
        }

        var stillFailed = stage2.Faults.Count;
        _log.LogInformation(
            "EventName=BulkInsertBatchFinal Entity={Logical} Count={Count} Failed={Failed}{FirstErr}",
            logical, entities.Count, stillFailed,
            stillFailed > 0 ? $" FirstError={Truncate(stage2.Faults[0].Error, 500)}" : "");

        return final;
    }

    private async Task<StageResult> ExecuteOnceAsync(IReadOnlyList<Entity> entities, CancellationToken ct)
    {
        var req = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = false },
            Requests = new OrganizationRequestCollection()
        };
        foreach (var e in entities) req.Requests.Add(new CreateRequest { Target = e });

        ExecuteMultipleResponse? resp = null;
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            resp = (ExecuteMultipleResponse)await _factory.Client.ExecuteAsync(req, token);
        }, ct);

        var results = new RowWriteResult[entities.Count];
        for (int i = 0; i < results.Length; i++) results[i] = RowWriteResult.Ok();

        var faults = new List<Fault>();
        if (resp?.IsFaulted == true)
        {
            foreach (var item in resp.Responses)
            {
                if (item.Fault is null) continue;
                if (item.RequestIndex < 0 || item.RequestIndex >= entities.Count) continue;
                results[item.RequestIndex] = RowWriteResult.Fail(item.Fault.Message);
                faults.Add(new Fault(item.RequestIndex, item.Fault.Message));
            }
        }
        return new StageResult(results, faults);
    }

    private readonly record struct Fault(int Index, string Error);

    private readonly record struct StageResult(
        IReadOnlyList<RowWriteResult> Results,
        IReadOnlyList<Fault> Faults);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
