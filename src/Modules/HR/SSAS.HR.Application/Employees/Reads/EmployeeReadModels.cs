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
// THE DEPARTMENT JOINS THEM FROM FP-007 PHASE 3, AND IS NOT A FOURTH SCOPE. It is surfaced for the same
// practical reason CompanyId and BranchId are — a caller needs to know which department each row is in —
// but nothing filters visibility by it.
//
// ---- IT NOW CARRIES THE CODE AND NAME, AND THE OLD ARGUMENT AGAINST THAT IS ANSWERED RATHER THAN DROPPED.
//
// This comment used to say the code and name were deliberately NOT joined, because "that would make an
// employee read a department read as well, and departments have their own scope". Ruled 2026-08-22, and the
// distinction the old reasoning missed is the one that matters:
//
//   * The `employeeCount` on a DEPARTMENT reads ACROSS an aggregate the caller may have no authority over —
//     employees are branch-scoped, the department is not, so counting there can disclose the size of
//     branches the caller cannot see. That one needs its own scope, and it has one.
//   * This LABELS a field the employee record already carries. `Employee.DepartmentId` is already returned;
//     the department it names is in the EMPLOYEE'S OWN COMPANY, which the caller's scope has already
//     admitted, so resolving it to a code and a name discloses nothing the caller could not obtain by
//     reading the department directly with the `View` permission they would need anyway.
//
// So there is no extra permission gate here, by ruling. What is NOT done is any widening: the join is an
// INNER join on the employee's own `DepartmentId`, which is NOT NULL with a real foreign key, so it can
// never add or remove a row — it only decorates the rows the scope already returned.
// The department an employee belongs to, resolved to something a caller can read (FP-007 `api-contracts.md`,
// shipped 2026-08-22). One record rather than three loose fields, because the three are meaningless apart:
// a code without its identifier cannot be followed, and an identifier without a name is what the surface
// already had.
//
// It replaces the bare `DepartmentId` rather than sitting beside it. Two sources for the same identifier is
// how they drift.
public sealed record EmployeeDepartmentSummary(Guid DepartmentId, string Code, string Name);

public sealed record EmployeeDetail(
  Guid EmployeeId,
  Guid CompanyId,
  Guid BranchId,
  EmployeeDepartmentSummary Department,
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
  EmployeeDepartmentSummary Department,
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

// ---- THE POSITION HISTORY ENTRY (FP-008 Phase 4, FR-POS-0212).
//
// The branch entry's shape, with two deliberate differences that come from the model rather than from taste:
//
//   * `ReasonCode` is a nullable STRING, not an enum. A branch transfer's reason is a closed set
//     (`EmployeeBranchTransferReason`); a position change's is free text under the approved Phase 1 model,
//     which is also why neither it nor `ReasonText` rides on the domain event.
//   * `SourcePositionId` is nullable and null marks the INITIAL assignment — the same convention the
//     department history uses, and the reason the stored column is nullable while the event's is not.
public sealed record EmployeePositionHistoryEntry(
  Guid AssignmentId,
  Guid? SourcePositionId,
  Guid DestinationPositionId,
  DateTimeOffset EffectiveFromUtc,
  string? ReasonCode,
  string? ReasonText,
  string ChangedBy);
