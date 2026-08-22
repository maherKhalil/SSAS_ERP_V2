using System.Text.Json.Serialization;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Positions.Reads;

namespace SSAS.HR.API.Positions;

// ==================================================================================================
// THE POSITION-FAMILY WIRE SHAPES (FP-008 api-contracts).
// ==================================================================================================
//
// ---- WHAT IS ABSENT IS THE POINT.
//
// No request type below carries a TenantId, a CompanyId or a Status. Tenant and company are execution
// context and are never accepted from a caller; status changes through the activate and deactivate ROUTES,
// so an update cannot express one. Strict JSON rejects any undeclared field, so sending one is a 400 rather
// than a silently ignored value and a false belief about what changed.
//
// No request carries a BranchId or a DepartmentId either, and neither will: `DEC-POS-0001` made position
// company-owned rather than branch-owned, and `OD-POS-003` made it independent of Department. Those
// absences are structural, not validated.
//
// `jobGradeId` and `salaryGradeId` ARE present, and that is not an exception. They are business arguments
// authorized server-side against live state — the application proves each is in the caller's company and
// Active before anything is written (`BRULE-POS-0009`, `BRULE-POS-0011`).
public sealed record CreatePositionRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("title")] string? Title,
  [property: JsonPropertyName("jobGradeId")] Guid? JobGradeId);

// Code, title and the grade reference. `DEC-POS-0018` grouped the re-grade under `HR.Positions.Update`
// deliberately — a role able to retitle a position but not re-grade it is a distinction no requirement asks
// for — so it belongs on this shape rather than on a route of its own.
public sealed record UpdatePositionRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("title")] string? Title,
  [property: JsonPropertyName("jobGradeId")] Guid? JobGradeId,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record CreateJobGradeRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("rankOrder")] int? RankOrder,
  [property: JsonPropertyName("salaryGradeId")] Guid? SalaryGradeId);

public sealed record UpdateJobGradeRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("rankOrder")] int? RankOrder,
  [property: JsonPropertyName("salaryGradeId")] Guid? SalaryGradeId,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// ---- THE THREE AMOUNTS ARE NULLABLE ON THE WIRE, AND ALL THREE MOVE TOGETHER (DEC-POS-0027).
//
// The band is ATOMIC: all present or all absent. They are three nullable decimals rather than a nested
// object precisely so a caller CAN send a half-filled band and be refused by name — a shape that could not
// express the mistake would make the refusal untestable because it would be unreachable.
//
// `currencyCode` is NOT accepted here. It is a projection of the owning Company's `BaseCurrencyCode`
// (`DEC-POS-0015`), so sending it is an undeclared field and a 400: accepting it would create a second
// source of truth for a fact the Company owns.
public sealed record CreateSalaryGradeRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("rankOrder")] int? RankOrder,
  [property: JsonPropertyName("minimumAmount")] decimal? MinimumAmount,
  [property: JsonPropertyName("midpointAmount")] decimal? MidpointAmount,
  [property: JsonPropertyName("maximumAmount")] decimal? MaximumAmount);

public sealed record UpdateSalaryGradeRequest(
  [property: JsonPropertyName("code")] string? Code,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("rankOrder")] int? RankOrder,
  [property: JsonPropertyName("minimumAmount")] decimal? MinimumAmount,
  [property: JsonPropertyName("midpointAmount")] decimal? MidpointAmount,
  [property: JsonPropertyName("maximumAmount")] decimal? MaximumAmount,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// The lifecycle routes carry no argument beyond the concurrency token, so one shape serves all six rather
// than six identical ones.
public sealed record PositionRowVersionRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// The employee-group route. `PositionId` is a classification rather than a security partition
// (`DEC-POS-0020`), so this is an ordinary employee update under `HR.Employees.Update` — which is why the
// reason fields are free-form audit metadata rather than an enum the way a branch transfer's are.
public sealed record ChangeEmployeePositionRequest(
  [property: JsonPropertyName("positionId")] Guid? PositionId,
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("reasonText")] string? ReasonText,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// ==================================================================================================
// RESPONSES
// ==================================================================================================
//
// CompanyId IS reported, deliberately: a caller reading across companies needs to know which one each row
// came from, and surfacing it makes a mis-scoped read visible in its own output rather than only in a query
// plan. Nothing here exposes normalized values, audit internals or domain-event data.
public sealed record PositionResponse(
  Guid PositionId,
  Guid CompanyId,
  string Code,
  string Title,
  PositionJobGradeResponse? JobGrade,
  string Status,
  // ---- NULL MEANS "NOT COMPUTABLE FOR THIS CALLER" (DEC-POS-0034).
  //
  // The count is taken within the caller's EMPLOYEE read scope, so two callers legitimately see different
  // numbers for one position. A caller holding `HR.Positions.View` but not `HR.Employees.View` has no
  // employee scope at all, and this is **null** for them.
  //
  // The two rejected alternatives, recorded because the choice is not obvious:
  //
  //   * **0** would be a lie — the position may have holders the caller simply cannot count.
  //   * **omitting the field** would make the JSON shape vary per caller, forcing clients to branch on
  //     field presence and poisoning any cache keyed on the shape. The strict-reader conventions on this
  //     surface favour a stable contract.
  //
  // Null carries the honest meaning at a stable shape, which is why it was ruled.
  int? EmployeeCount,
  string RowVersion)
{
  public static PositionResponse From(PositionDetail detail, int? employeeCount, string rowVersion) => new(
    detail.PositionId,
    detail.CompanyId,
    detail.Code,
    detail.Title,
    PositionJobGradeResponse.From(detail.JobGrade),
    detail.Status.ToString(),
    employeeCount,
    rowVersion);
}

// The grade block on a position. Code, name and rank — never the salary grade it maps to, and never any
// amount: reading pay bands needs `HR.SalaryGrades.View`, and this response is served under
// `HR.Positions.View`.
public sealed record PositionJobGradeResponse(
  Guid JobGradeId,
  string Code,
  string Name,
  int RankOrder)
{
  public static PositionJobGradeResponse? From(PositionJobGradeSummary? summary) =>
    summary is null ? null : new(summary.JobGradeId, summary.Code, summary.Name, summary.RankOrder);
}

// The list row. Narrower than the detail on purpose: a search result set is the widest read on this
// surface, so the grade block — which would need resolving per row — and the holder count are not part of
// it. The grade IDENTIFIER is enough to let a caller fetch what they need.
public sealed record PositionSummaryResponse(
  Guid PositionId,
  Guid CompanyId,
  string Code,
  string Title,
  Guid? JobGradeId,
  string Status,
  string RowVersion)
{
  public static PositionSummaryResponse From(PositionListItem item, string rowVersion) => new(
    item.PositionId,
    item.CompanyId,
    item.Code,
    item.Title,
    item.JobGradeId,
    item.Status.ToString(),
    rowVersion);
}

public sealed record JobGradeResponse(
  Guid JobGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  // The identifier only. Which band a grade maps to is a structural fact about the ladder; the band's code,
  // name and amounts need `HR.SalaryGrades.View` and are served by that resource.
  Guid? SalaryGradeId,
  string Status,
  string RowVersion)
{
  public static JobGradeResponse From(JobGradeDetail detail, string rowVersion) => new(
    detail.JobGradeId,
    detail.CompanyId,
    detail.Code,
    detail.Name,
    detail.RankOrder,
    detail.SalaryGradeId,
    detail.Status.ToString(),
    rowVersion);
}

public sealed record JobGradeSummaryResponse(
  Guid JobGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  Guid? SalaryGradeId,
  string Status,
  string RowVersion)
{
  public static JobGradeSummaryResponse From(JobGradeListItem item, string rowVersion) => new(
    item.JobGradeId,
    item.CompanyId,
    item.Code,
    item.Name,
    item.RankOrder,
    item.SalaryGradeId,
    item.Status.ToString(),
    rowVersion);
}

// ---- THE PAY BAND, WITH ITS CURRENCY ECHOED RATHER THAN STORED (DEC-POS-0015, DEC-POS-0035).
//
// `currencyCode` is the owning Company's `BaseCurrencyCode`, read through the module-facing lookup and
// never persisted on a salary grade. It appears here because an amount without a currency is unreadable;
// it is rejected on write, because accepting it would make this a second source of truth for a fact
// `DEC-CMP-0009` gives the Company and makes immutable.
public sealed record SalaryGradeResponse(
  Guid SalaryGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount,
  string? CurrencyCode,
  string Status,
  string RowVersion)
{
  public static SalaryGradeResponse From(
    SalaryGradeDetail detail, string? currencyCode, string rowVersion) => new(
    detail.SalaryGradeId,
    detail.CompanyId,
    detail.Code,
    detail.Name,
    detail.RankOrder,
    detail.MinimumAmount,
    detail.MidpointAmount,
    detail.MaximumAmount,
    currencyCode,
    detail.Status.ToString(),
    rowVersion);
}

public sealed record SalaryGradeSummaryResponse(
  Guid SalaryGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount,
  string? CurrencyCode,
  string Status,
  string RowVersion)
{
  public static SalaryGradeSummaryResponse From(
    SalaryGradeListItem item, string? currencyCode, string rowVersion) => new(
    item.SalaryGradeId,
    item.CompanyId,
    item.Code,
    item.Name,
    item.RankOrder,
    item.MinimumAmount,
    item.MidpointAmount,
    item.MaximumAmount,
    currencyCode,
    item.Status.ToString(),
    rowVersion);
}

// ---- ONE POSITION-HISTORY ENTRY (FR-POS-0212).
//
// `sourcePositionId` is null on the INITIAL record, which is what distinguishes a hire from a change. The
// reason fields are free-form audit metadata, present here because a reader of someone's promotion history
// is exactly who needs them — unlike the domain event, which deliberately carries neither.
public sealed record EmployeePositionHistoryResponse(
  Guid AssignmentId,
  Guid? SourcePositionId,
  Guid DestinationPositionId,
  DateTimeOffset EffectiveFromUtc,
  string? ReasonCode,
  string? ReasonText,
  string ChangedBy)
{
  public static EmployeePositionHistoryResponse From(EmployeePositionHistoryEntry entry) => new(
    entry.AssignmentId,
    entry.SourcePositionId,
    entry.DestinationPositionId,
    entry.EffectiveFromUtc,
    entry.ReasonCode,
    entry.ReasonText,
    entry.ChangedBy);
}

public sealed record PositionPageResponse(
  IReadOnlyList<PositionSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

public sealed record JobGradePageResponse(
  IReadOnlyList<JobGradeSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);

public sealed record SalaryGradePageResponse(
  IReadOnlyList<SalaryGradeSummaryResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);
