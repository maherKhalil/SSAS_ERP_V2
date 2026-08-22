using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.HR.Application.Employees.Reads;

namespace SSAS.HR.Application.ImportExport;

// THE RUN-HISTORY READ SIDE (FR-DOC-0103, FR-DOC-0202).
//
// ================================================================================================
// IT TAKES AN `EmployeeReadScope`, WHICH IS THE PERMISSION (DEC-POS-0018).
// ================================================================================================
//
// `EmployeeReadScope` is constructible only by `EmployeeScopeResolver`, and that resolver checks
// `HR.Employees.View` as its first dimension before it will produce one. So a caller without the read
// permission cannot obtain the argument these methods require — not a check somebody might forget, a type
// they cannot construct. `authorization-model.md` gives both routes exactly that permission, on the ground
// that *"history of employee operations is an employee read"*.
//
// ---- SEPARATE FROM `IEmployeeReadService`, DELIBERATELY.
//
// That interface's surface is pinned by an exact-inventory architecture guard, and its subject is the
// EMPLOYEE — rows about people, scoped by three dimensions. These are rows about OPERATIONS: they name no
// employee, carry no branch, and are scoped by company alone. Folding them in would widen a guarded
// employee-read surface with reads that are not employee reads, and would make "what can this interface
// disclose about a person" a harder question to answer than it currently is.
//
// ---- SCOPED BY COMPANY, NOT BY BRANCH, BECAUSE THE ROWS HAVE NO BRANCH.
//
// An import or an export is performed within a company; branch is a sibling dimension, and neither run
// record carries one. A branch predicate over these tables would have nothing to predicate on — which is why
// the scope's branch set is deliberately unused here rather than silently ignored.
public interface IEmployeeRunHistoryReadService
{
  Task<PagedResult<EmployeeImportRunListItem>> SearchImportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default);

  Task<PagedResult<EmployeeExportRunListItem>> SearchExportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default);
}
