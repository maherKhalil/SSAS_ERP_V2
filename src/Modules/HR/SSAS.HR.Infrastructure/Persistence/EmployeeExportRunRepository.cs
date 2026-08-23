using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Infrastructure.Persistence;

// THE EXPORT RUN RECORD'S REPOSITORY (FP-009, DEC-DOC-0006).
//
// One method, no lookup and no counterpart to it. An export has no replay question to answer: re-running one
// produces a second file and a second record, which is correct, because the first file already left and a
// replay that reported "you already did this" would hide a second extraction rather than prevent one.
public sealed class EmployeeExportRunRepository(ITenantDbContextAccessor contextAccessor)
  : IEmployeeExportRunRepository
{
  public async Task AddAsync(EmployeeExportRun run, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<EmployeeExportRun>().AddAsync(run, cancellationToken);
  }
}
