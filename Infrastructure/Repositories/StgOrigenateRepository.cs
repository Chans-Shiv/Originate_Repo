using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fhn.Originate.FtbanknewSync.Domain.Interfaces;
using Fhn.Originate.FtbanknewSync.Domain.Models;
using Fhn.Originate.FtbanknewSync.Domain.Entities;
using Fhn.Originate.FtbanknewSync.Configuration;

namespace Fhn.Originate.FtbanknewSync.Infrastructure.Repositories;

public sealed class StgOrigenateRepository : TableRepositoryBase
{
    public StgOrigenateRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r,
        IOptions<OrigenateOptions> o, ILogger<StgOrigenateRepository> log)
        : base(w, d, r, o, log) { }

    public override string EntityLogicalName => ColumnMap.StgOrigenateEntityLogical;
    public override string PrimaryIdField => ColumnMap.StgOrigenatePrimaryId;
}

public sealed class HoldingRepository : TableRepositoryBase
{
    public HoldingRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r,
        IOptions<OrigenateOptions> o, ILogger<HoldingRepository> log)
        : base(w, d, r, o, log) { }

    public override string EntityLogicalName => ColumnMap.HoldingEntityLogical;
    public override string PrimaryIdField => ColumnMap.HoldingPrimaryId;
}

public sealed class ExceptionsRepository : TableRepositoryBase
{
    public ExceptionsRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r,
        IOptions<OrigenateOptions> o, ILogger<ExceptionsRepository> log)
        : base(w, d, r, o, log) { }

    public override string EntityLogicalName => ColumnMap.ExceptionsEntityLogical;
    public override string PrimaryIdField => ColumnMap.ExceptionsPrimaryId;
}
