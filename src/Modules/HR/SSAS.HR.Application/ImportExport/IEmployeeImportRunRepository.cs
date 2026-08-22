using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Application.ImportExport;

// THE IMPORT RUN RECORD'S REPOSITORY (FP-009, DEC-DOC-0004, DEC-DOC-0006).
//
// Two methods, and there is deliberately no third. There is NO UPDATE and NO DELETE, because the record is
// append-only and the absence of a method is the first of the two protections — the second being
// `TenantDbContext.PreventAppendOnlyMutation`, which refuses a Modified or Deleted entry whatever path
// tracked it.
//
// Nothing here reads a run by identifier, because nothing needs to yet: the run-history route is
// `FR-DOC-0103` and arrives with the read side.
public interface IEmployeeImportRunRepository
{
  // ---- THE REPLAY LOOKUP, WHICH IS THE WHOLE OF `DEC-DOC-0004`.
  //
  // Matched on the NORMALIZED key within the company, because that is the column the unique index is over —
  // `DEC-POS-0030`: EF translates a value-converted property in a projection but not in a predicate, so a
  // lookup written against the display value would either not translate or not use the index.
  //
  // Company-scoped, not tenant-scoped: two companies in one tenant are not obliged to coordinate their key
  // choices, so the same key in a sibling company is a different key and must not be found here.
  Task<EmployeeImportRun?> FindByKeyAsync(
    Guid companyId, string normalizedImportKey, CancellationToken cancellationToken = default);

  Task AddAsync(EmployeeImportRun run, CancellationToken cancellationToken = default);
}
