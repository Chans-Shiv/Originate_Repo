using Microsoft.Extensions.Options;
using OrigenateFunction.Abstractions;
using OrigenateFunction.Models;
using OrigenateFunction.Options;

namespace OrigenateFunction.Repositories;

public sealed class StgOrigenateRepository : TableRepositoryBase
{
    public StgOrigenateRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r, IOptions<OrigenateOptions> o) : base(w, d, r, o) { }

    public override string EntityLogicalName => ColumnMap.StgOrigenateEntityLogical;
    public override string EntitySet => ColumnMap.StgOrigenateEntitySet;
    public override string PrimaryIdField => ColumnMap.StgOrigenatePrimaryId;
}

public sealed class HoldingRepository : TableRepositoryBase
{
    public HoldingRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r, IOptions<OrigenateOptions> o) : base(w, d, r, o) { }

    public override string EntityLogicalName => ColumnMap.HoldingEntityLogical;
    public override string EntitySet => ColumnMap.HoldingEntitySet;
    public override string PrimaryIdField => ColumnMap.HoldingPrimaryId;
}

public sealed class ExceptionsRepository : TableRepositoryBase
{
    public ExceptionsRepository(
        IBulkWriter w, IBulkDeleter d, IPagedReader r, IOptions<OrigenateOptions> o) : base(w, d, r, o) { }

    public override string EntityLogicalName => ColumnMap.ExceptionsEntityLogical;
    public override string EntitySet => ColumnMap.ExceptionsEntitySet;
    public override string PrimaryIdField => ColumnMap.ExceptionsPrimaryId;
}
