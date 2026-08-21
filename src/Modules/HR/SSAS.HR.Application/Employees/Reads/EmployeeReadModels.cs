using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees.Reads;

// WHAT A READ RETURNS (FP-006 api-contracts).
//
// PROJECTIONS, NEVER ENTITIES. Returning Employee itself would hand a caller a tracked aggregate whose
// navigations can pull unscoped rows on access, and would tie the read surface to the write model's shape.
// These are flat records composed in SQL.
//
// CompanyId AND BranchId ARE ON EVERY ROW, deliberately. A caller reading across companies or branches needs
// to know which one each row came from, and surfacing it makes a mis-scoped read visible in its own output
// rather than only in the query plan.
//
// DepartmentId JOINS THEM FROM FP-007 PHASE 3, AND IS NOT A FOURTH SCOPE. It is surfaced for the same
// practical reason CompanyId and BranchId are — a caller needs to know which department each row is in —
// but nothing filters visibility by it. The department's own CODE and NAME are deliberately NOT joined in
// here: that would make an employee read a department read as well, and departments have their own scope.
public sealed record EmployeeDetail(
  Guid EmployeeId,
  Guid CompanyId,
  Guid BranchId,
  Guid DepartmentId,
  string EmployeeNumber,
  string FullName,
  string? NationalId,
  DateTimeOffset EmploymentDate,
  DateTimeOffset? TerminationDate,
  EmployeeStatus Status,
  EmployeeStatusChangeReason StatusChangeReasonCode,
  DateTimeOffset StatusChangedUtc,
  // The concurrency version, required by every mutating contract as `expectedRowVersion` and therefore part
  // of the detail a caller reads before they can write (FP-006 api-contracts, rowversion transport).
  byte[] RowVersion);

// The list row. Narrower than the detail on purpose: a search result set is the widest read in the module,
// and the national identifier — the one sensitive field — is not part of it.
public sealed record EmployeeSummary(
  Guid EmployeeId,
  Guid CompanyId,
  Guid BranchId,
  Guid DepartmentId,
  string EmployeeNumber,
  string FullName,
  DateTimeOffset EmploymentDate,
  EmployeeStatus Status);

// One row of the append-only branch history. SourceBranchId is null for the initial assignment, which
// records where the employee STARTED rather than a move.
public sealed record EmployeeBranchHistoryEntry(
  Guid AssignmentId,
  Guid? SourceBranchId,
  Guid DestinationBranchId,
  DateTimeOffset EffectiveFromUtc,
  EmployeeBranchTransferReason ReasonCode,
  string? ReasonText,
  string TransferredBy);
