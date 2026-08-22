using System.Text.Json.Serialization;
using SSAS.HR.Application.Departments.Reads;

namespace SSAS.HR.API.Departments;

// ==================================================================================================
// THE DEPARTMENT WIRE SHAPES (FP-007 api-contracts).
// ==================================================================================================
//
// ---- WHAT IS ABSENT IS THE POINT.
//
// No request type below has a TenantId or a Status property. Tenant is execution context and is never
// accepted from a caller; status changes through the activate and deactivate ROUTES, so an update cannot
// express one. Strict JSON rejects any undeclared field, so sending one is a 400 rather than a silently
// ignored value and a false belief about what changed.
//
// CompanyId is absent for the same reason it is absent from the employee contracts: it is an ambient
// dimension carried by X-Company-Id and established once per request against live state.
//
// ---- BUT parentDepartmentId AND employeeId ARE PRESENT, AND THAT IS NOT AN EXCEPTION TO THE RULE.
//
// They are BUSINESS ARGUMENTS authorized server-side against live state — the same category as the
// employee transfer destination. They assert nothing about the caller's own scope; the application proves
// each is in the caller's company and usable before anything is written.
public sealed record CreateDepartmentRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("parentDepartmentId")] Guid? ParentDepartmentId);

// The mutable descriptive fields only. ParentDepartmentId is absent by construction, so an ordinary update
// can never express a hierarchy move — that is what the move routes are for, and they hold the department
// hierarchy's own serialization.
public sealed record UpdateDepartmentRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// The destination of a hierarchy move. There is no source: the source is the department's current parent,
// read from the record, and accepting one would let a request assert where a record used to be.
public sealed record MoveDepartmentRequest(
  [property: JsonPropertyName("parentDepartmentId")] Guid? ParentDepartmentId,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// Move-to-root and the manager operations carry no argument beyond the concurrency token, so they share
// one shape rather than three identical ones.
public sealed record DepartmentRowVersionRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record AssignDepartmentManagerRequest(
  [property: JsonPropertyName("employeeId")] Guid? EmployeeId,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// The employee-group route. DepartmentId is a classification rather than a security partition (ADR-024), so
// this is an ordinary employee update — which is why the reason fields are free-form audit metadata and not
// an enum the way a branch transfer's are.
public sealed record ChangeEmployeeDepartmentRequest(
  [property: JsonPropertyName("departmentId")] Guid? DepartmentId,
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("reasonText")] string? ReasonText,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// ==================================================================================================
// RESPONSES
// ==================================================================================================
//
// CompanyId IS reported, deliberately: a caller reading across companies needs to know which one each row
// came from, and surfacing it makes a mis-scoped read visible in its own output rather than only in a
// query plan. Nothing here exposes normalized values, audit internals or domain-event data.
public sealed record DepartmentResponse(
  Guid DepartmentId,
  Guid CompanyId,
  string Code,
  string Name,
  Guid? ParentDepartmentId,
  string Status,
  DepartmentManagerResponse? Manager,
  // ---- NULL MEANS "NOT COMPUTABLE FOR THIS CALLER", AND NOTHING ELSE (DEC-POS-0034).
  //
  // Specified by FP-007's `api-contracts.md` and shipped 2026-08-22, after the FP-008 as-built audit found
  // it had never been built. The semantics are `DEC-POS-0034`'s, unchanged: the field is present for every
  // caller, and its value is
  //
  //   * a NUMBER — the members of this department the caller's own employee scope can see, so two callers
  //     authorized for different branches legitimately read different numbers for one department;
  //   * ZERO — the department genuinely has no members the caller can see;
  //   * NULL — the caller holds `HR.Departments.View` without `HR.Employees.View`, has no employee scope at
  //     all, and there is no honest number to report.
  //
  // Zero and null are therefore DIFFERENT ANSWERS and a client must not conflate them. The two rejected
  // alternatives are recorded on `PositionResponse.EmployeeCount` and apply here identically: `0` for the
  // unscoped caller would be a lie, and omitting the field would make the JSON shape vary per caller.
  int? EmployeeCount,
  string RowVersion)
{
  public static DepartmentResponse From(
    DepartmentDetail detail, int? employeeCount, string rowVersion) => new(
    detail.DepartmentId,
    detail.CompanyId,
    detail.Code,
    detail.Name,
    detail.ParentDepartmentId,
    detail.Status.ToString(),
    DepartmentManagerResponse.From(detail.Manager),
    employeeCount,
    rowVersion);
}

// ---- THREE STATES, AND THE WIRE KEEPS THEM DISTINCT.
//
//   null           — the department has no manager;
//   isAssigned + identity   — a manager the caller may see;
//   isAssigned + no identity — a manager the caller may NOT see.
//
// The third is the one that matters. A department is company-visible while employees are branch-scoped, so
// a caller authorized for one branch reading a company-wide department learns THAT a manager exists and
// nothing more. Collapsing it into null would tell them the department has no manager, which is false.
public sealed record DepartmentManagerResponse(
  bool IsAssigned,
  Guid? EmployeeId,
  string? EmployeeNumber,
  string? FullName,
  bool IsActive)
{
  public static DepartmentManagerResponse? From(DepartmentManagerSummary? summary) =>
    summary is null
      ? null
      : new(summary.IsAssigned, summary.EmployeeId, summary.EmployeeNumber, summary.FullName,
        summary.IsActive);
}

// The list row. Narrower than the detail on purpose: a search result set is the widest read on this
// surface, and the manager — whose identity is branch-scoped and would need resolving per row — is not
// part of it.
//
// `employeeCount` is absent for the same reason and one more: `api-contracts.md` specifies it on the DETAIL
// representation only, and a per-row count would be one extra scoped aggregate per result — a page of
// twenty rows becoming twenty-one queries. Adding it here would be a new contract, not this fix.
public sealed record DepartmentSummaryResponse(
  Guid DepartmentId,
  Guid CompanyId,
  string Code,
  string Name,
  Guid? ParentDepartmentId,
  string Status,
  string RowVersion)
{
  public static DepartmentSummaryResponse From(DepartmentListItem item, string rowVersion) => new(
    item.DepartmentId,
    item.CompanyId,
    item.Code,
    item.Name,
    item.ParentDepartmentId,
    item.Status.ToString(),
    rowVersion);
}

// One direct child. REQ-HR-0101 specifies the adjacency model and no full-tree contract, so this is the
// shape of "one level down" and there is deliberately no recursive variant.
public sealed record DepartmentChildResponse(
  Guid DepartmentId,
  string Code,
  string Name,
  string Status)
{
  public static DepartmentChildResponse From(DepartmentChild child) =>
    new(child.DepartmentId, child.Code, child.Name, child.Status.ToString());
}

public sealed record DepartmentPageResponse(
  IReadOnlyList<DepartmentSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);
