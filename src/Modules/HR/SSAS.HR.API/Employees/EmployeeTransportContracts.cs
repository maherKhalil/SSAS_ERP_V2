using System.Text.Json.Serialization;
using SSAS.HR.Application.Employees.Reads;

namespace SSAS.HR.API.Employees;

// ==================================================================================================
// THE EMPLOYEE WIRE CONTRACTS (FP-006 api-contracts).
// ==================================================================================================
//
// ---- WHAT IS ABSENT IS THE POINT.
//
// No request type below has a TenantId, CompanyId, BranchId or Status property. That is not validated away;
// it is UNREPRESENTABLE. Strict JSON rejects any field not declared for the contract, so a caller sending
// one gets 400 request.invalid rather than a silently ignored field and a false sense of what they changed.
//
// The single exception is the transfer destination, which is a BUSINESS ARGUMENT authorized server-side
// against live state — never an assertion of the caller's own scope, and never their execution branch
// (SEC-EMP-0203, SEC-EMP-0212).
//
// departmentId is the SECOND such business argument (FP-007 Phase 3). Like the transfer destination it is
// authorized server-side against live state — it must exist in the caller's trusted company and be Active —
// and it asserts nothing about the caller's own scope. It is required, because an employee without a
// department cannot be created from Phase 3 onward.
public sealed record CreateEmployeeRequest(
  [property: JsonPropertyName("employeeNumber")] string? EmployeeNumber,
  [property: JsonPropertyName("fullName")] string? FullName,
  [property: JsonPropertyName("employmentDate")] DateTimeOffset? EmploymentDate,
  [property: JsonPropertyName("nationalId")] string? NationalId,
  [property: JsonPropertyName("departmentId")] Guid? DepartmentId,
  // ---- REQUIRED FROM DAY ONE (FP-008 Phase 3, OD-POS-001, BR-HR-0006).
  //
  // There is no transitional phase in which this is optional: the column ships NOT NULL and the migration
  // asserted the table was empty before it existed, so no caller and no cohort ever had an employee without
  // a position. Nullable on the DTO only so a missing field is reported as `RequestInvalid` rather than
  // deserialized as `Guid.Empty` and refused later with a less useful answer.
  [property: JsonPropertyName("positionId")] Guid? PositionId);

// Only the mutable profile. EmployeeNumber is absent because it is an identifier, and BranchId is absent by
// construction so an ordinary update can never express a transfer (BRULE-EMP-0015).
public sealed record UpdateEmployeeProfileRequest(
  [property: JsonPropertyName("fullName")] string? FullName,
  [property: JsonPropertyName("nationalId")] string? NationalId,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record EmployeeLifecycleRequest(
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record TerminateEmployeeRequest(
  [property: JsonPropertyName("terminationDate")] DateTimeOffset? TerminationDate,
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// Transfer has its OWN request type rather than reusing the lifecycle one: it is the only contract in the
// package carrying a branch identifier, and sharing a shape with the others is how that identifier would
// eventually appear somewhere it must not.
public sealed record TransferEmployeeRequest(
  [property: JsonPropertyName("destinationBranchId")] Guid? DestinationBranchId,
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("reasonText")] string? ReasonText,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// ---- RESPONSES.
//
// CompanyId and BranchId are REPORTED, deliberately: a caller reading across companies or branches needs to
// know which one each row came from, and surfacing it makes a mis-scoped read visible in its own output.
// Nothing here exposes normalized values, audit internals, access assignments or domain-event data.
public sealed record EmployeeResponse(
  Guid EmployeeId,
  Guid CompanyId,
  Guid BranchId,
  // ---- THE DEPARTMENT THE EMPLOYEE BELONGS TO (FP-007 `api-contracts.md`, shipped 2026-08-22).
  //
  // Specified by FP-007 and never built: the value reached the application layer and stopped at the wire.
  // Never null — `Employee.DepartmentId` is NOT NULL behind a real foreign key, so every employee has one.
  //
  // NO EXTRA PERMISSION GATE, by ruling, and the distinction from `Department.employeeCount` is the reason:
  // that field reads ACROSS an aggregate the caller may have no authority over, while this LABELS a field
  // the employee record already carries, in the employee's own company, which the caller's scope has
  // already admitted.
  EmployeeDepartmentResponse Department,
  string EmployeeNumber,
  string FullName,
  string? NationalId,
  DateTimeOffset EmploymentDate,
  DateTimeOffset? TerminationDate,
  string Status,
  string StatusChangeReasonCode,
  DateTimeOffset StatusChangedUtc,
  string RowVersion)
{
  public static EmployeeResponse From(EmployeeDetail detail, string rowVersion) => new(
    detail.EmployeeId,
    detail.CompanyId,
    detail.BranchId,
    EmployeeDepartmentResponse.From(detail.Department),
    detail.EmployeeNumber,
    detail.FullName,
    detail.NationalId,
    detail.EmploymentDate,
    detail.TerminationDate,
    detail.Status.ToString(),
    detail.StatusChangeReasonCode.ToString(),
    detail.StatusChangedUtc,
    rowVersion);
}

// The list row. Narrower than the detail on purpose: search is the widest read in the module, and the
// national identifier — the one sensitive field — is not part of it.
public sealed record EmployeeSummaryResponse(
  Guid EmployeeId,
  Guid CompanyId,
  Guid BranchId,
  // On the list row as well as the detail, because `api-contracts.md` specifies both — and unlike a manager
  // or a member count, this costs one join rather than one query per row.
  EmployeeDepartmentResponse Department,
  string EmployeeNumber,
  string FullName,
  DateTimeOffset EmploymentDate,
  string Status)
{
  public static EmployeeSummaryResponse From(EmployeeSummary summary) => new(
    summary.EmployeeId,
    summary.CompanyId,
    summary.BranchId,
    EmployeeDepartmentResponse.From(summary.Department),
    summary.EmployeeNumber,
    summary.FullName,
    summary.EmploymentDate,
    summary.Status.ToString());
}

// The department sub-object, identical on the detail and the list row. Three fields and no status: whether
// the DEPARTMENT is active is a fact about the department, read from the department surface, and repeating
// it here would give two places to answer the same question and one of them would eventually be stale.
public sealed record EmployeeDepartmentResponse(Guid DepartmentId, string Code, string Name)
{
  public static EmployeeDepartmentResponse From(EmployeeDepartmentSummary department) =>
    new(department.DepartmentId, department.Code, department.Name);
}

public sealed record EmployeePageResponse(
  IReadOnlyCollection<EmployeeSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

public sealed record EmployeeBranchHistoryResponse(
  Guid AssignmentId,
  Guid? SourceBranchId,
  Guid DestinationBranchId,
  DateTimeOffset EffectiveFromUtc,
  string ReasonCode,
  string? ReasonText,
  string TransferredBy)
{
  public static EmployeeBranchHistoryResponse From(EmployeeBranchHistoryEntry entry) => new(
    entry.AssignmentId,
    entry.SourceBranchId,
    entry.DestinationBranchId,
    entry.EffectiveFromUtc,
    entry.ReasonCode.ToString(),
    entry.ReasonText,
    entry.TransferredBy);
}
