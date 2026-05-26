using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using OrigenateFunction.Abstractions;

namespace OrigenateFunction.Dataverse;

public sealed class BulkDeleter : IBulkDeleter
{
    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly ILogger<BulkDeleter> _log;

    public BulkDeleter(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        ILogger<BulkDeleter> log)
    {
        _factory = factory;
        _resilience = resilience;
        _log = log;
    }

    public async Task DeleteBatchAsync(string entityLogicalName, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;

        var req = new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = false },
            Requests = new OrganizationRequestCollection()
        };
        foreach (var id in ids)
            req.Requests.Add(new DeleteRequest { Target = new EntityReference(entityLogicalName, id) });

        ExecuteMultipleResponse? resp = null;
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            resp = (ExecuteMultipleResponse)await _factory.Client.ExecuteAsync(req, token);
        }, ct);

        var faulted = 0;
        string? firstErr = null;
        if (resp?.IsFaulted == true)
        {
            foreach (var item in resp.Responses)
            {
                if (item.Fault is null) continue;
                faulted++;
                firstErr ??= item.Fault.Message;
            }
        }

        if (faulted > 0)
            _log.LogWarning(
                "EventName=BulkDeleteBatchPartial Entity={Logical} Count={Count} Failed={Failed} FirstError={Err}",
                entityLogicalName, ids.Count, faulted, Truncate(firstErr ?? "(none)", 500));
        else
            _log.LogInformation(
                "EventName=BulkDeleteBatch Entity={Logical} Count={Count} Failed=0",
                entityLogicalName, ids.Count);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
