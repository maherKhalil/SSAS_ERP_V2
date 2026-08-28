using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Payroll.API;

// PAYROLL'S DOMAIN ERRORS ON THE WIRE (api-contracts.md).
//
// Every refusal a caller can provoke has a stable `payroll.*` code they can branch on, and a status that
// says what kind of problem it is. The mapping is EXHAUSTIVE BY CONSTRUCTION: the default arm is
// `ApiErrors.WriteFailure` (500), so an error added to the domain without a line here surfaces loudly and is
// caught by the mapper-arm tests rather than shipped as a plausible 400.
//
// ---- AN OUT-OF-SCOPE RECORD IS A 404, AND ON THIS SURFACE THAT MATTERS MOST.
//
// `Payroll.CompensationNotFound` covers both "no such record" and "a record you may not reach". Reporting
// the second as 403 would let a caller enumerate who is paid what, one probe at a time — ask for an employee
// identifier, read the status, learn whether compensation exists. GL made this argument about a chart of
// accounts; here the directory being denied is people's pay.
public static class PayrollApiErrorMapper
{
  public static readonly ApiError NotFound = new(404, "payroll.not_found");

  // FP-015 (T-088). 404 because the route exists, the caller is authenticated and permitted, and what is
  // absent is the SUBJECT of the read. Not 403 — nothing is forbidden. Not a 4xx-invalid — the request was
  // well formed. A distinct code from `payroll.not_found`, which answers about a thing the caller named.
  public static readonly ApiError NoLinkedEmployee = new(404, "payroll.no_linked_employee");
  public static readonly ApiError Conflict = new(409, "payroll.conflict");
  public static readonly ApiError PeriodClosed = new(409, "payroll.period_closed");
  public static readonly ApiError ElementUnmapped = new(409, "payroll.element_unmapped");
  public static readonly ApiError RunStateInvalid = new(409, "payroll.run_state_invalid");
  public static readonly ApiError LedgerRefused = new(409, "payroll.ledger_refused");
  public static readonly ApiError NothingToCalculate = new(422, "payroll.nothing_to_calculate");
  public static readonly ApiError AttendancePeriodOpen = new(409, "payroll.attendance_period_open");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");

  // ---- T-095. A GL CODE REACHING A PAYROLL MAPPER, AND IT KEEPS GL'S STRING.
  //
  // `PostPayrollRunCommandHandler` posts through `IJournalPoster`, which returns `Gl.AccountNotFound` when a
  // mapped account is gone. `DEC-L-079` fixes the STATUS at GL's 404; the string is `gl.not_found` rather
  // than `payroll.not_found` because **what was not found is the GL account, and answering `payroll.not_found`
  // would name the wrong missing thing.**
  //
  // The literal is repeated rather than referenced: `SSAS.Payroll.API` does not reference `SSAS.GL.API` and
  // must not. `Cross_site_agreement` is what keeps the two from drifting.
  public static readonly ApiError LedgerAccountNotFound = new(404, "gl.not_found");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- MALFORMED INPUT. The caller can fix these by sending something else.
      "Payroll.PayElementCodeInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PayElementNameInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PayElementAmountNegative" => ApiErrors.RequestInvalid,
      "Payroll.PayElementCalculationOrderInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PayElementCompanyRequired" => ApiErrors.RequestInvalid,
      "Payroll.PayElementAccountRequired" => ApiErrors.RequestInvalid,

      // ---- OVERTIME TIER (T-080). BOTH 400, AND THE SECOND IS NOT A CONFLICT.
      //
      // `OvertimeTierInvalid` is a length-and-control-character check on a caller-sent string, and
      // `Attendance.OvertimeTierInvalid` — the identical concept in another module — is already 400
      // (`AttendanceApiErrorMapper.cs:67`).
      //
      // `OvertimeTierNotApplicable` refuses a tier on an element whose behaviour is not `OvertimeHourly`.
      // That is NOT `PayElementInactive`'s shape, which is 409 because inactivity is a state that changes
      // over time and the caller could not have known. Behaviour is intrinsic and visible: the domain calls
      // this *"a caller who has misunderstood the model, not a harmless extra"* at `PayElement.cs:346`,
      // the same phrase `PayElementErrors` uses for the negative-amount case, which is 400.
      "Payroll.PayElementOvertimeTierInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PayElementOvertimeTierNotApplicable" => ApiErrors.RequestInvalid,
      "Payroll.CompensationCompanyRequired" => ApiErrors.RequestInvalid,
      "Payroll.CompensationEmployeeRequired" => ApiErrors.RequestInvalid,
      "Payroll.CompensationBaseAmountNegative" => ApiErrors.RequestInvalid,
      "Payroll.CompensationAssignmentElementRequired" => ApiErrors.RequestInvalid,
      "Payroll.CompensationAssignmentAmountNegative" => ApiErrors.RequestInvalid,
      "Payroll.PeriodCompanyRequired" => ApiErrors.RequestInvalid,
      "Payroll.PeriodFiscalPeriodRequired" => ApiErrors.RequestInvalid,
      "Payroll.PeriodNameInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PeriodBoundsInvalid" => ApiErrors.RequestInvalid,
      "Payroll.PayDateBeforePeriod" => ApiErrors.RequestInvalid,
      "Payroll.RunCompanyRequired" => ApiErrors.RequestInvalid,
      "Payroll.RunPeriodRequired" => ApiErrors.RequestInvalid,
      "Payroll.RunJournalRequired" => ApiErrors.RequestInvalid,

      // ---- ABSENT, OR NOT REACHABLE. Deliberately indistinguishable.
      "Payroll.PayElementNotFound" => NotFound,
      "Payroll.CompensationNotFound" => NotFound,
      "Payroll.CompensationNoneInForce" => NotFound,
      "Payroll.PeriodNotFound" => NotFound,
      "Payroll.RunNotFound" => NotFound,
      "Payroll.FiscalPeriodNotFound" => NotFound,

      // ---- STATE CONFLICTS. The caller is not wrong; the world is not ready.
      "Payroll.PayElementCodeConflict" => Conflict,
      "Payroll.PayElementCodeImmutable" => Conflict,
      "Payroll.CompensationAssignmentDuplicate" => Conflict,
      "Payroll.PeriodConflict" => Conflict,
      "Payroll.RunConflict" => Conflict,
      "Payroll.PayElementInactive" => Conflict,

      // The mapping-at-approval refusal (`OD-PAY-0012`). Its own code because the remedy is specific: map
      // the element the message names, then approve again.
      "Payroll.PayElementUnmapped" => ElementUnmapped,

      // `OD-PAY-0014`. Its own code because a client can act on it — the message names the period.
      "Payroll.FiscalPeriodClosed" => PeriodClosed,

      // Lifecycle refusals. 409 rather than 400: the request was well-formed and the run was in the wrong
      // state, which is a fact about the world rather than about the message.
      "Payroll.RunNotRecalculable" => RunStateInvalid,
      "Payroll.RunNotApprovable" => RunStateInvalid,
      "Payroll.RunNotPostable" => RunStateInvalid,
      "Payroll.RunNotReversible" => RunStateInvalid,
      "Payroll.RunHasNoLines" => RunStateInvalid,

      "Payroll.LedgerRefusedPosting" => LedgerRefused,
      "Payroll.LedgerRefusedReversal" => LedgerRefused,

      // 422 rather than 404: the request named a real company and period, and the answer is that there is
      // nothing to compute. That is a semantic refusal, not a missing resource.
      "Payroll.NoIncludedEmployees" => NothingToCalculate,
      "Payroll.UnbalancedPosting" => NothingToCalculate,

      // ---- A DAILY SALARY WITH NO WORKING DAYS TO PRICE (T-115). 409, not 422.
      //
      // **The request is well-formed and the world is not ready** — the company has no working calendar, or
      // the employee's attendance summary did not arrive — which is `AttendancePeriodOpen`'s shape rather
      // than `NothingToCalculate`'s. There IS something to compute; the input it needs is absent.
      //
      // **Unmapped, this fell through to a 500** for what is a business refusal, and the error-mapping
      // register caught it. It had been unmapped since T-107 declared the constant.
      "Payroll.DailySalaryHasNoWorkingDays" => RunStateInvalid,

      // FP-013, OD-ATT-0010. A 409 rather than a 422: the request is well-formed and the world is not ready
      // — somebody has to close the attendance period, which is the same shape as `PeriodClosed` above.
      "Payroll.AttendancePeriodOpen" => AttendancePeriodOpen,

      // ---- AUTHORIZATION. Naming no company, no tenant and no topology.
      "Payroll.NoLinkedEmployee" => NoLinkedEmployee,
      "Payroll.InvalidActor" => ApiErrors.Forbidden,
      "Payroll.ReadPermissionDenied" => ApiErrors.Forbidden,
      "Payroll.WritePermissionDenied" => ApiErrors.Forbidden,
      "Payroll.CompanyScopeDenied" => CompanyScopeDenied,

      // Company-context establishment shares the platform's codes, so they are mapped by the same names GL
      // uses — wire-equivalence across modules is the contract where errors must match (`ADR-012`).
      // ---- 403, CORRECTED IN T-096. IT ANSWERED 400 HERE AND 403 AT FOUR OTHER SITES.
      //
      // Found by `The_same_code_answers_the_same_status_at_every_site_that_maps_it` on its first run, and
      // ruled on the distinction the product already draws rather than on a head-count:
      // `Company.SelectionRequired` is 400 because THE CALLER MUST SELECT ONE, while this one is *"a
      // trusted company context is required"* — **an authorization context that could not be established,
      // which no change to the request can fix.**
      "Company.ContextRequired" => ApiErrors.Forbidden,
      "Company.ScopeDenied" => CompanyScopeDenied,

      // ---- EXHAUSTIVE BY CONSTRUCTION. A new domain error with no line here becomes a 500 and fails the
      // mapper-arm test, rather than being quietly served as a 400 that tells a caller to fix something
      // they did not get wrong.
      "Gl.AccountNotFound" => LedgerAccountNotFound,

      _ => ApiErrors.WriteFailure
    };
  }
}
