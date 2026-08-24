using System.Text.Json.Serialization;
using SSAS.Payroll.Domain.Elements;

namespace SSAS.Payroll.API;

// ================================================================================================
// PAYROLL'S WIRE SHAPES — AND EVERY REQUEST PROPERTY CARRIES [property: JsonPropertyName].
// ================================================================================================
//
// **THIS IS NOT A STYLE NOTE. IT IS THE FP-011 DEFECT, WRITTEN DOWN SO IT IS NOT RESHIPPED.**
//
// `StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
// **case-sensitive**. GL shipped its request records without these attributes and the consequence was total:
// `{"code":"4100"}` never bound to `Code`, the reader returned null, and **every GL write route answered
// `400 request.invalid`** — while the routes, handlers, domain and error mapper were all correct.
//
// The fault was an ABSENCE, which is precisely what reading the code does not reveal. Two API tests caught
// it on their first run. A Step-2 scenario asserts a correctly-cased body binds on every payroll write
// route, so the absence cannot return silently.
//
// ---- WHAT IS DELIBERATELY ABSENT FROM EVERY REQUEST.
//
// **No currency.** The company's base currency is projected on read and never accepted (`DEC-PAY-0003`,
// `ADR-027` decision 2). A request supplying one is refused by the strict reader as an unknown property —
// the reader doing its ordinary job, not a special case.
//
// **No fiscal period, and no payroll-period bounds.** `OD-PAY-0002` ruled 1:1 alignment, and
// `GeneratePayrollPeriodCommand` takes a DATE from which the ledger resolves the period. Bounds a caller
// could name are bounds a caller could misalign, and the closed-period check would then guard a straddle.
//
// **No status field anywhere.** Every transition is a named-action POST with its own permission. A
// `PUT {status: "approved"}` would let the most sensitive act in the module arrive through the same door as
// an ordinary edit.

// ---- PAY ELEMENTS.
//
// No `code` on the update request: the code is immutable from creation, following `Account`'s precedent, so
// the wire shape has no field for it and a caller who sends one gets a 400 rather than a silently ignored
// property. `kind` and `behaviour` are absent for the same reason — changing either would redefine what past
// runs computed while leaving their stored lines untouched.
public sealed record CreatePayElementRequest(
  [property: JsonPropertyName("code")] string Code,
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("kind")] PayElementKind Kind,
  [property: JsonPropertyName("behaviour")] PayElementBehaviour Behaviour,
  [property: JsonPropertyName("defaultRateOrAmount")] decimal DefaultRateOrAmount,
  [property: JsonPropertyName("calculationOrder")] int CalculationOrder,
  [property: JsonPropertyName("glAccountId")] Guid? GlAccountId);

public sealed record UpdatePayElementRequest(
  [property: JsonPropertyName("name")] string Name,
  [property: JsonPropertyName("defaultRateOrAmount")] decimal DefaultRateOrAmount,
  [property: JsonPropertyName("calculationOrder")] int CalculationOrder,
  [property: JsonPropertyName("glAccountId")] Guid? GlAccountId,
  [property: JsonPropertyName("rowVersion")] string? RowVersion);

public sealed record PayElementActivationRequest(
  [property: JsonPropertyName("rowVersion")] string? RowVersion);

public sealed record PayElementResponse(
  Guid PayElementId, Guid CompanyId, string Code, string Name, string Kind, string Behaviour,
  decimal DefaultRateOrAmount, int CalculationOrder, Guid? GlAccountId, bool IsActive);

// ---- COMPENSATION.
//
// There is a POST and NO PUT. `OD-PAY-0003` ruled dated history, so a change is a NEW record — the absent
// verb is the ruling made visible in the surface rather than a rule someone has to remember.
//
// The grade-band observation is SUPPLIED rather than fetched: `OD-PAY-0004` made the band informational, and
// a handler that fetched it would have made it a prerequisite, which is the "validated" reading the ruling
// refused. Its absence simply means no observation was recorded.
public sealed record CompensationAssignmentRequest(
  [property: JsonPropertyName("payElementId")] Guid PayElementId,
  [property: JsonPropertyName("rateOrAmount")] decimal? RateOrAmount);

public sealed record RecordCompensationRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("effectiveFromUtc")] DateTimeOffset EffectiveFromUtc,
  [property: JsonPropertyName("baseAmount")] decimal BaseAmount,
  [property: JsonPropertyName("assignments")] IReadOnlyList<CompensationAssignmentRequest>? Assignments,
  [property: JsonPropertyName("wasOutsideGradeBand")] bool WasOutsideGradeBand,
  [property: JsonPropertyName("gradeBandObservation")] string? GradeBandObservation);

public sealed record CompensationResponse(
  Guid EmployeeCompensationId, Guid CompanyId, Guid EmployeeId, DateTimeOffset EffectiveFromUtc,
  decimal BaseAmount, bool WasOutsideGradeBand, string? GradeBandObservation, bool IsInForceNow,
  string CurrencyCode, IReadOnlyList<CompensationAssignmentResponse> Assignments);

public sealed record CompensationAssignmentResponse(Guid PayElementId, string PayElementCode, decimal? RateOrAmount);

// ---- PERIODS AND RUNS.
//
// `anyDateInPeriodUtc` rather than bounds: the ledger resolves which fiscal period covers it, and
// `PayrollPeriod.CreateAlignedTo` is not permitted to disagree with what comes back.
public sealed record GeneratePayrollPeriodRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("anyDateInPeriodUtc")] DateTimeOffset AnyDateInPeriodUtc,
  [property: JsonPropertyName("payDateUtc")] DateTimeOffset PayDateUtc);

public sealed record CreatePayrollRunRequest(
  [property: JsonPropertyName("companyId")] Guid CompanyId,
  [property: JsonPropertyName("payrollPeriodId")] Guid PayrollPeriodId);

// Calculation, approval and posting take NO BODY. Everything each needs is on the run it names, and a body
// would let a caller change what is being approved at the moment of approval — the same reasoning that
// gives GL's posting route no body.
public sealed record ReversePayrollRunRequest(
  [property: JsonPropertyName("reversalDateUtc")] DateTimeOffset ReversalDateUtc,
  [property: JsonPropertyName("description")] string Description);

public sealed record PayrollPeriodResponse(
  Guid PayrollPeriodId, Guid CompanyId, Guid FiscalPeriodId, string Name,
  DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset PayDateUtc);

public sealed record PayrollRunResponse(
  Guid PayrollRunId, Guid CompanyId, Guid PayrollPeriodId, string PeriodName, string Status,
  string? CalculatedBy, DateTimeOffset? CalculatedUtc,
  string? ApprovedBy, DateTimeOffset? ApprovedUtc,
  string? PostedBy, DateTimeOffset? PostedUtc,
  Guid? JournalEntryId, string CurrencyCode,
  decimal TotalEarnings, decimal TotalDeductions, decimal NetPay, int EmployeeCount);

// ---- PAYSLIPS (OD-PAY-0015 — a projection, not a document).
//
// The lines SUM to the totals, by construction under `OD-PAY-0008`'s per-line rounding, so a client can
// verify the payslip's own arithmetic without re-deriving anything. `AC-PAY-0026` asserts exactly that.
public sealed record PayslipResponse(
  Guid PayrollRunId, Guid EmployeeId, string PeriodName, DateTimeOffset PayDateUtc, string CurrencyCode,
  decimal TotalEarnings, decimal TotalDeductions, decimal NetPay,
  IReadOnlyList<PayslipLineResponse> Lines);

public sealed record PayslipLineResponse(
  int Sequence, Guid PayElementId, string PayElementCode, string PayElementName, string Kind, decimal Amount);
