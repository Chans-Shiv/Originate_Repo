using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Dataverse;

public sealed class PagedReader : IPagedReader
{
    private readonly DataverseConnectionFactory _factory;
    private readonly DataverseResiliencePipeline _resilience;
    private readonly ILogger<PagedReader> _log;

    public PagedReader(
        DataverseConnectionFactory factory,
        DataverseResiliencePipeline resilience,
        ILogger<PagedReader> log)
    {
        _factory = factory;
        _resilience = resilience;
        _log = log;
    }

    public async IAsyncEnumerable<Entity> RetrieveAllAsync(
        QueryExpression query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var logical = query.EntityName;
        var page = 0;
        long total = 0;

        _log.LogInformation("EventName=RetrieveAllStart Entity={Logical} PageSize={PageSize}",
            logical, query.PageInfo?.Count ?? 0);

        while (true)
        {
            page++;
            EntityCollection? coll = null;
            await _resilience.Pipeline.ExecuteAsync(async token =>
            {
                coll = await _factory.Client.RetrieveMultipleAsync(query, token);
            }, ct);
            if (coll is null) break;

            total += coll.Entities.Count;
            _log.LogInformation(
                "EventName=RetrieveAllPage Entity={Logical} Page={Page} Rows={Rows} Total={Total} HasMore={More}",
                logical, page, coll.Entities.Count, total, coll.MoreRecords);

            foreach (var e in coll.Entities) yield return e;

            if (!coll.MoreRecords) break;
            query.PageInfo!.PageNumber++;
            query.PageInfo.PagingCookie = coll.PagingCookie;
        }

        _log.LogInformation("EventName=RetrieveAll Entity={Logical} Total={Total} Pages={Pages}",
            logical, total, page);
    }

    public async Task<long> CountAsync(string entityLogicalName, FilterExpression? filter, CancellationToken ct)
    {
        // Stream the id only and count — Dataverse has no built-in cheap count for arbitrary filters.
        var idAttr = entityLogicalName + "id";
        var query = new QueryExpression(entityLogicalName)
        {
            ColumnSet = new ColumnSet(idAttr),
            PageInfo = new PagingInfo { Count = 5000, PageNumber = 1, ReturnTotalRecordCount = true }
        };
        if (filter is not null) query.Criteria = filter;

        EntityCollection? coll = null;
        await _resilience.Pipeline.ExecuteAsync(async token =>
        {
            coll = await _factory.Client.RetrieveMultipleAsync(query, token);
        }, ct);

        long count = coll?.TotalRecordCount ?? 0;
        if (count < 0 && coll is not null)
        {
            // TotalRecordCount returns -1 when not requested or unavailable; fall back to paging.
            count = coll.Entities.Count;
            while (coll!.MoreRecords)
            {
                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = coll.PagingCookie;
                await _resilience.Pipeline.ExecuteAsync(async token =>
                {
                    coll = await _factory.Client.RetrieveMultipleAsync(query, token);
                }, ct);
                count += coll.Entities.Count;
            }
        }

        _log.LogInformation("EventName=Count Entity={Logical} Count={Count}", entityLogicalName, count);
        return count;
    }
}
