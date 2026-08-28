using System.Text.Json.Serialization;
using SSAS.Attendance.Domain.Leave;

namespace SSAS.Attendance.API;

// ================================================================================================
// ATTENDANCE'S WIRE SHAPES — EVERY REQUEST PROPERTY CARRIES [property: JsonPropertyName], AND EVERY
// ENUM PROPERTY ADDITIONALLY CARRIES [property: JsonConverter(typeof(JsonStringEnumConverter))].
// ================================================================================================
//
// **THIS IS NOT A STYLE NOTE. IT IS TWO SHIPPED DEFECTS, WRITTEN DOWN SO THEY ARE NOT RESHIPPED A THIRD
// TIME.**
//
// `StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
// **case-sensitive** AND deserializes enums **from numbers only**.
//
//   * **FP-011 (GL)** shipped its request records without `JsonPropertyName`. `{"code":"4100"}` never bound
//     to `Code`, the reader returned null, and **every GL write route answered `400 request.invalid`** —
//     while the routes, handlers, domain and error mapper were all correct.
//
//   * **FP-012 (Payroll)** shipped `CreatePayElementRequest` without the enum converter. `"Earning"` could
//     not become a `PayElementKind`, so **`POST /api/payroll/elements` refused every well-formed body**: no
//     pay element could be created, therefore no payroll could ever be calculated.
//
// Both faults were an **ABSENCE**, which is precisely what reading the code does not reveal. Both were
// total and silent. A property-level `[JsonConverter]` is honoured regardless of serializer options, which
// is why it works here where a global option would not.
//
// `TS-ATT-0018` asserts a correctly-cased body binds on EVERY write route, including every enum-valued
// property, and it is written FIRST rather than last.
//
// ---- WHAT IS DELIBERATELY ABSENT FROM EVERY REQUEST.
//
// **No `branchId`, anywhere.** `AttendanceRecord` is branch-owned, and the write boundary stamps `BranchId`
// from the execution context via `ICurrentBranchResolver`. A caller who could name a branch could record
// attendance into one they cannot read — which is exactly the boundary `OD-ATT-0011` drew.
//
// **No `status` field on anything.** Every transition is a named-action POST with its own permission. A
// `PUT {status: "closed"}` would let the act that freezes Payroll's inputs arrive through the same door as
// an ordinary edit.
//
// **No money, no rate, no multiplier, no currency** (`DEC-ATT-0004`). Attendance records quantities.

// ---- WORKING CALENDAR.
//
// `weekendDays` as an array of integer day ordinals (0 = Sunday), not names. The wire shape mirrors
// `DayOfWeek`'s own ordinals so no client has to agree with the server about spelling or locale — which
// matters more than usual here, because `BR-ATT-0001` exists precisely because the weekend is not universal.
public sealed record CreateWorkingCalendarRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("weekendDays")] IReadOnlyList<int>? WeekendDays,
  [property: JsonPropertyName("isDefault")] bool IsDefault);

public sealed record UpdateWorkingCalendarRequest(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("weekendDays")] IReadOnlyList<int>? WeekendDays);

public sealed record AddHolidayRequest(
  [property: JsonPropertyName("holidayDate")] DateOnly HolidayDate,
  [property: JsonPropertyName("name")] string Name);

public sealed record RemoveHolidayRequest(
  [property: JsonPropertyName("holidayDate")] DateOnly HolidayDate);

// ---- PERIODS.
//
// Close and reopen take NO BODY. Everything each needs is on the period it names, and a body would let a
// caller change what is being closed at the moment of closing — the same reasoning that gives GL's posting
// route and Payroll's approval route no body.
public sealed record CreateAttendancePeriodRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("startDate")] DateOnly StartDate,
  [property: JsonPropertyName("endDate")] DateOnly EndDate);

// ---- ATTENDANCE RECORDS.
//
// No `attendancePeriodId`: the period is RESOLVED from the date. A caller who could name both could name a
// date and a period that disagree, and the record would then sit in a period that does not cover it —
// invisible until a payroll run read the wrong period's totals.
public sealed record RecordAttendanceRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("employeeId")] Guid EmployeeId,
  [property: JsonPropertyName("attendanceDate")] DateOnly AttendanceDate,
  [property: JsonPropertyName("workedQuantity")] decimal WorkedQuantity,
  [property: JsonPropertyName("overtimeQuantity")] decimal OvertimeQuantity,
  [property: JsonPropertyName("overtimeTier")] string? OvertimeTier,
  [property: JsonPropertyName("paidAbsenceQuantity")] decimal PaidAbsenceQuantity,
  [property: JsonPropertyName("unpaidAbsenceQuantity")] decimal UnpaidAbsenceQuantity,
  [property: JsonPropertyName("note")] string? Note);

// Deltas, signed. No company, no employee and no date: all three come from the record being corrected, so a
// caller cannot retarget an adjustment at somebody else's record while naming a company they hold.
public sealed record AdjustAttendanceRequest(
  [property: JsonPropertyName("workedDelta")] decimal WorkedDelta,
  [property: JsonPropertyName("overtimeDelta")] decimal OvertimeDelta,
  [property: JsonPropertyName("overtimeTier")] string? OvertimeTier,
  [property: JsonPropertyName("paidAbsenceDelta")] decimal PaidAbsenceDelta,
  [property: JsonPropertyName("unpaidAbsenceDelta")] decimal UnpaidAbsenceDelta,
  [property: JsonPropertyName("note")] string Note);

// ---- LEAVE TYPES.
//
// No `code` on the update request: the code is immutable from creation, following `Account` and
// `PayElement`, so the wire shape has no field for it and a caller who sends one gets a 400 rather than a
// silently ignored property. `behaviour` is absent for the same reason — changing it would redefine what
// past requests consumed while leaving their stored rows untouched.
public sealed record CreateLeaveTypeRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("code")] string Code,
  [property: JsonPropertyName("name")] string Name,

  // ---- THE ENUM THAT NEEDS THE CONVERTER, AND FP-012 FOUND THAT THE HARD WAY.
  //
  // Without this attribute `"Unpaid"` cannot become a `LeaveBehaviour` — `JsonSerializerOptions.Default`
  // reads enums from NUMBERS only — the whole record fails to bind, and this route answers
  // `400 request.invalid` for every well-formed request. Exactly the Payroll defect, and it would be just
  // as total: no leave type could be created, so no leave could ever be requested.
  [property: JsonPropertyName("behaviour")]
  [property: JsonConverter(typeof(JsonStringEnumConverter))] LeaveBehaviour Behaviour,

  [property: JsonPropertyName("isSensitive")] bool IsSensitive);

public sealed record UpdateLeaveTypeRequest(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("isSensitive")] bool IsSensitive);

public sealed record LeaveTypeActivationRequest(
  [property: JsonPropertyName("rowVersion")] string? RowVersion);

// ---- BALANCES AND REQUESTS.
//
// `employeeId` is MANDATORY and never inferred from the caller. The mapping exists (`UserEmployeeLink`,
// `ADR-030`, T-082) but NOTHING ON THIS ROUTE READS IT, so this route is an administrator acting on an
// employee's behalf.
//
// **The anticipation recorded here was that the field would become optional and this route would gain a
// self-service meaning. It did not happen that way.** FP-015 delivered self-service as separate `/me/`
// routes with their own permissions (T-089), because a route that infers its subject when a field is
// absent and accepts one when it is present is two authorization rules wearing one contract. `employeeId`
// here is still mandatory.
public sealed record SetLeaveEntitlementRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("employeeId")] Guid EmployeeId,
  [property: JsonPropertyName("leaveTypeId")] Guid LeaveTypeId,
  [property: JsonPropertyName("periodYear")] int PeriodYear,
  [property: JsonPropertyName("entitlementQuantity")] decimal EntitlementQuantity);

public sealed record SubmitLeaveRequestRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("employeeId")] Guid EmployeeId,
  [property: JsonPropertyName("leaveTypeId")] Guid LeaveTypeId,
  [property: JsonPropertyName("startDate")] DateOnly StartDate,
  [property: JsonPropertyName("endDate")] DateOnly EndDate);

// A decision note and NOTHING that changes what is being decided. No dates, no type, no employee — an
// approver must not be able to alter the request at the moment of approving it, which is the same reasoning
// that gives Payroll's approval route no body at all.
public sealed record DecideLeaveRequestRequest(
  [property: JsonPropertyName("decisionNote")] string? DecisionNote);
