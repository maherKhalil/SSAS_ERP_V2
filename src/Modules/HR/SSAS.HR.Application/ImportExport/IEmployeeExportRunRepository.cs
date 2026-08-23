using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Application.ImportExport;

// THE EXPORT RUN RECORD'S REPOSITORY (FP-009, DEC-DOC-0006).
//
// ONE METHOD, and the difference from its import sibling is the design rather than an omission.
//
// The import repository has a lookup because `DEC-DOC-0004`'s replay needs one: an operator whose connection
// dropped asks "did my import happen?", and answering requires finding the original run. **An export has no
// such question.** Re-running an export is an ordinary second export that produces a second file and a
// second record — which is correct, because the first file already left and a replay that returned "you
// already did this" would hide a second extraction rather than prevent one.
//
// No update and no delete, for the reason its sibling states: append-only, with
// `TenantDbContext.PreventAppendOnlyMutation` as the guarantee and the absent method as the courtesy.
public interface IEmployeeExportRunRepository
{
  Task AddAsync(EmployeeExportRun run, CancellationToken cancellationToken = default);
}
