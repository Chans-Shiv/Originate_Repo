using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;
using Fhn.Originate.FtbanknewSync.Configuration;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

public sealed class BulkDeleter : IBulkDeleter
{
    // AsyncOperation statecode/statuscode constants
    // statecode: 0=Ready, 1=Suspended, 2=Locked, 3=Completed
    // statuscode (when statecode=3): 30=Succeeded, 31=Failed, 32=Canceled
    private const int StateCompleted = 3;
    private const int StatusSucceeded = 30;
    private const int StatusFailed = 31;
    private const int StatusCanceled = 32;

    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly OrigenateOptions _opts;
    private readonly ILogger<BulkDeleter> _log;

    public BulkDeleter(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        IOptions<OrigenateOptions> opts,
        ILogger<BulkDeleter> log)
    {
        _factory = factory;
        _resilience = resilience;
        _opts = opts.Value;
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

    public async Task SubmitAndAwaitBulkDeleteAsync(
        string entityLogicalName, FilterExpression? filter, CancellationToken ct)
    {
        var query = new QueryExpression(entityLogicalName) { ColumnSet = new ColumnSet(false) };
        if (filter is not null) query.Criteria = filter;

        var jobName = $"Truncate_{entityLogicalName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var req = new BulkDeleteRequest
        {
            JobName = jobName,
            QuerySet = new[] { query },
            StartDateTime = DateTime.UtcNow,
            ToRecipients = Array.Empty<Guid>(),
            CCRecipients = Array.Empty<Guid>(),
            RecurrencePattern = string.Empty,
            SendEmailNotification = false
        };

        _log.LogInformation("EventName=BulkDeleteSubmit Entity={Logical} Job={Job}", entityLogicalName, jobName);

        BulkDeleteResponse? resp = null;
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            resp = (BulkDeleteResponse)await _factory.Client.ExecuteAsync(req, token);
        }, ct);

        if (resp is null) throw new InvalidOperationException("BulkDeleteRequest returned null response.");
        var jobId = resp.JobId;
        _log.LogInformation("EventName=BulkDeleteSubmitted Entity={Logical} JobId={JobId}", entityLogicalName, jobId);

        await PollUntilCompleteAsync(entityLogicalName, jobId, ct);
    }

    private async Task PollUntilCompleteAsync(string entityLogicalName, Guid jobId, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(_opts.BulkDeleteTimeoutMinutes);
        var interval = TimeSpan.FromSeconds(Math.Max(1, _opts.BulkDeletePollIntervalSeconds));
        var columns = new ColumnSet("statecode", "statuscode", "message", "completedon", "friendlymessage");
        var pollNum = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException(
                    $"BulkDelete job {jobId} for {entityLogicalName} did not complete within " +
                    $"{_opts.BulkDeleteTimeoutMinutes} minutes.");

            await Task.Delay(interval, ct);
            pollNum++;

            Entity? op = null;
            await _resilience.Pipeline.ExecuteAsync(async token =>
            {
                op = await _factory.Client.RetrieveAsync("asyncoperation", jobId, columns, token);
            }, ct);
            if (op is null) continue;

            var state = (op["statecode"] as OptionSetValue)?.Value ?? -1;
            var status = (op["statuscode"] as OptionSetValue)?.Value ?? -1;

            _log.LogInformation(
                "EventName=BulkDeletePoll Entity={Logical} JobId={JobId} Poll={Poll} State={State} Status={Status}",
                entityLogicalName, jobId, pollNum, state, status);

            if (state != StateCompleted) continue;

            if (status == StatusSucceeded)
            {
                _log.LogInformation(
                    "EventName=BulkDelete Entity={Logical} JobId={JobId} Polls={Polls} Status=Succeeded",
                    entityLogicalName, jobId, pollNum);
                return;
            }

            var msg = (op.GetAttributeValue<string>("message")
                       ?? op.GetAttributeValue<string>("friendlymessage")
                       ?? "(no message)").Trim();
            if (status == StatusFailed)
                throw new InvalidOperationException(
                    $"BulkDelete job {jobId} for {entityLogicalName} FAILED: {Truncate(msg, 1000)}");
            if (status == StatusCanceled)
                throw new InvalidOperationException(
                    $"BulkDelete job {jobId} for {entityLogicalName} was CANCELED: {Truncate(msg, 1000)}");

            throw new InvalidOperationException(
                $"BulkDelete job {jobId} for {entityLogicalName} ended in unexpected status {status}: {Truncate(msg, 1000)}");
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
