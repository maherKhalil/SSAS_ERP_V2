using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.API;

// GL'S DOMAIN ERRORS ON THE WIRE (api-contracts.md).
//
// Every refusal a caller can provoke has a stable `gl.*` code they can branch on, and a status that says
// what kind of problem it is. The mapping is EXHAUSTIVE by construction: the default arm is
// `ApiErrors.WriteFailure` (500), so an error added to the domain without a line here surfaces loudly and is
// caught by the mapper-arm tests rather than shipped as a plausible 400.
//
// ---- THE ONE MAPPING WORTH ARGUING ABOUT: AN OUT-OF-SCOPE ACCOUNT IS A 404.
//
// `Gl.AccountNotFound` covers both "no such account" and "an account you may not reach". Reporting the
// second as 403 would let a caller enumerate the chart one probe at a time — ask for an identifier, read
// the status, learn whether it exists. The two are deliberately indistinguishable, which costs a caller
// nothing they are entitled to and denies an attacker a directory.
public static class GlApiErrorMapper
{
  public static readonly ApiError NotFound = new(404, "gl.not_found");
  public static readonly ApiError Conflict = new(409, "gl.conflict");
  public static readonly ApiError Unbalanced = new(422, "gl.journal_unbalanced");
  public static readonly ApiError PeriodClosed = new(409, "gl.period_closed");
  public static readonly ApiError AccountInactive = new(409, "gl.account_inactive");
  public static readonly ApiError Immutable = new(409, "gl.journal_immutable");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- MALFORMED INPUT. The caller can fix these by sending something else.
      "Gl.AccountCodeInvalid" => ApiErrors.RequestInvalid,
      "Gl.AccountNameInvalid" => ApiErrors.RequestInvalid,
      "Gl.FiscalYearCodeInvalid" => ApiErrors.RequestInvalid,
      "Gl.FiscalYearRangeInvalid" => ApiErrors.RequestInvalid,
      "Gl.FiscalYearHasNoPeriods" => ApiErrors.RequestInvalid,
      "Gl.FiscalPeriodsNotContiguous" => ApiErrors.RequestInvalid,
      "Gl.JournalDescriptionInvalid" => ApiErrors.RequestInvalid,
      "Gl.JournalReferenceInvalid" => ApiErrors.RequestInvalid,
      "Gl.JournalLineNotSingleSided" => ApiErrors.RequestInvalid,
      "Gl.JournalLineAmountNegative" => ApiErrors.RequestInvalid,
      "Gl.InvalidActor" => ApiErrors.RequestInvalid,

      // ---- 422 RATHER THAN 400, AND THE DISTINCTION IS REAL.
      //
      // An unbalanced journal is well-formed: every field is present and correctly typed, and no reader
      // could have rejected it. What fails is a RULE ABOUT THE CONTENT, which is exactly what 422 is for.
      // A client distinguishing "I sent malformed JSON" from "my journal does not balance" needs the two to
      // differ, because only the second is something a user can be shown and asked to correct.
      "Gl.JournalUnbalanced" => Unbalanced,
      "Gl.JournalInsufficientLines" => Unbalanced,

      // ---- ABSENT, OR NOT YOURS. Deliberately the same answer — see the note above.
      "Gl.AccountNotFound" => NotFound,
      "Gl.JournalNotFound" => NotFound,
      "Gl.JournalDraftNotFound" => NotFound,
      "Gl.FiscalYearNotFound" => NotFound,
      "Gl.FiscalPeriodNotFound" => NotFound,

      // ---- STATE CONFLICTS. The request was valid; the world was not in the required state.
      "Gl.AccountCodeConflict" => Conflict,
      "Gl.FiscalYearCodeConflict" => Conflict,
      "Gl.FiscalCalendarBusy" => Conflict,
      "Gl.FiscalYearOverlaps" => Conflict,
      "Gl.JournalNumberConflict" => Conflict,
      "Gl.JournalAlreadyReversed" => Conflict,
      "Gl.FiscalPeriodAlreadyClosed" => Conflict,
      "Gl.FiscalPeriodAlreadyOpen" => Conflict,
      "Gl.FiscalPeriodClosed" => PeriodClosed,
      "Gl.AccountInactive" => AccountInactive,
      "Gl.AccountCodeImmutable" => Immutable,

      // ---- AUTHORIZATION. Permission and scope are separate axes and answer separately.
      "Gl.ReadPermissionDenied" => ApiErrors.Forbidden,
      "Gl.WritePermissionDenied" => ApiErrors.Forbidden,
      "Gl.CompanyScopeDenied" => CompanyScopeDenied,
      "Company.InvalidSelection" => CompanyScopeDenied,
      "Company.SelectionRequired" => ApiErrors.RequestInvalid,
      "Company.InvalidSelectionFormat" => ApiErrors.RequestInvalid,
      "Company.ContextRequired" => ApiErrors.Forbidden,

      // ---- CONCURRENCY. The caller held a stale row version and lost.
      "Persistence.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,

      // ---- THE DEFAULT IS A 500, AND THAT IS DELIBERATE.
      //
      // An unmapped domain error means someone added a refusal and did not decide what it means on the
      // wire. Surfacing it as a 500 makes that visible in a test run; defaulting to 400 would ship a
      // confident, wrong answer and nobody would look. `WriteFailure` is the house default for this arm --
      // `DepartmentApiErrorMapper` uses the same one.
      _ => ApiErrors.WriteFailure
    };
  }
}
