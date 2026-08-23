using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Infrastructure.Persistence;

// THE IMPORT RUN RECORD'S REPOSITORY (FP-009, DEC-DOC-0004, DEC-DOC-0006).
//
// Two methods and no third: append-only means no update and no remove, and the absence of a method here is
// the first of the two protections. The second is `TenantDbContext.PreventAppendOnlyMutation`, which refuses
// a Modified or Deleted entry for any `IAppendOnlyEntity` whatever path tracked it — which is why the
// absence of a method is a convenience for the reader rather than the guarantee itself.
public sealed class EmployeeImportRunRepository(ITenantDbContextAccessor contextAccessor)
  : IEmployeeImportRunRepository
{
  public async Task<EmployeeImportRun?> FindByKeyAsync(
    Guid companyId, string normalizedImportKey, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // ---- THE PREDICATE IS OVER THE NORMALIZED COLUMN, WHICH IS WHAT THE UNIQUE INDEX IS OVER.
    //
    // `DEC-POS-0030`: EF translates a value-converted property in a PROJECTION but not in a PREDICATE, so a
    // lookup written against `ImportKey.Value` would not use the index — and `UX_EmployeeImportRuns_Company_Key`
    // is the thing that makes a replay find the ORIGINAL run rather than a row that happens to look like it.
    //
    // AsNoTracking because this is a fact being read, never an aggregate being mutated: the run this returns
    // is reported back to the caller verbatim and must not become a tracked entity a later save could touch.
    //
    // Company-scoped, matching the index. A tenant-wide lookup would find a sibling company's run and report
    // its counts to a caller who has no authority over it.
    return await context.Set<EmployeeImportRun>()
      .AsNoTracking()
      .Where(run => run.CompanyId == companyId && run.NormalizedImportKey == normalizedImportKey)
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task AddAsync(EmployeeImportRun run, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<EmployeeImportRun>().AddAsync(run, cancellationToken);
  }
}
