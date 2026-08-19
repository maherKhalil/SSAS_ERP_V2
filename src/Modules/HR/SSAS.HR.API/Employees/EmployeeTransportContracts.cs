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
public sealed record CreateEmployeeRequest(
  [property: JsonPropertyName("employeeNumber")] string? EmployeeNumber,
  [property: JsonPropertyName("fullName")] string? FullName,
  [property: JsonPropertyName("employmentDate")] DateTimeOffset? EmploymentDate,
  [property: JsonPropertyName("nationalId")] string? NationalId);

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
  string EmployeeNumber,
  string FullName,
  DateTimeOffset EmploymentDate,
  string Status)
{
  public static EmployeeSummaryResponse From(EmployeeSummary summary) => new(
    summary.EmployeeId,
    summary.CompanyId,
    summary.BranchId,
    summary.EmployeeNumber,
    summary.FullName,
    summary.EmploymentDate,
    summary.Status.ToString());
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
