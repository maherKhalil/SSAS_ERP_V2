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

  // ============================================================================================
  // ⚠ THE UNCLASSIFIED UNIQUE VIOLATION -- `DEC-DEP-0027` AS AMENDED (T-245).
  // ============================================================================================
  //
  // T-165 ruled this code must stay UNMAPPED here, and **most of that ruling stands**. GL has SIX unique
  // indexes -- account code, fiscal-year code, draft line number, journal number, one-reversal-per-
  // original, entry line number -- and one arm cannot tell which lost. Its principle is the binding one:
  // **a handler that cannot tell which index it hit must not name one.**
  //
  // ---- WHAT CHANGED, AND IT IS ONE SENTENCE OF THAT DECISION.
  //
  // T-165 also said *"the 500 default is not the bug here; it is the house rule working"*. **That is
  // overturned.** A 500 is not silence, it is a WRONG ASSERTION: it tells a caller the server broke when
  // nothing broke, sends them to file a bug instead of examining their input, and inflates the one metric
  // an operator pages on.
  //
  // **The forcing-function reading is refuted by this repository's own history.** T-171, T-173 and T-176
  // each rediscovered this class and repaired a single path. A wrong status used as a reminder reminded
  // nobody; it shipped 500s until someone tripped over it a fourth time.
  //
  // ---- WHY THIS ARM DOES NOT CONTRADICT THE PART THAT STANDS.
  //
  // **`gl.unique_conflict` names no index**, so it makes exactly the claim the evidence supports: a
  // uniqueness rule was violated. T-165's objection was to the MESSAGE being false for five of six
  // indexes -- a duplicate account code told a journal number already exists -- and it never considered a
  // deliberately unnamed code, because the option before it was reusing a specific one.
  //
  // Context is still resolved **by the caller who knows the operation**: `PostJournalDraftCommandHandler`
  // translates to `JournalErrors.NumberConflict` because it can reach exactly one index, and
  // `ReverseJournalCommandHandler` still translates nothing because it can reach two. **This arm fires
  // only where no handler resolved it** -- a floor under the unclassified, not a switch pretending to know.
  public static readonly ApiError UniqueConflict = new(409, "gl.unique_conflict");
  public static readonly ApiError Unbalanced = new(422, "gl.journal_unbalanced");
  public static readonly ApiError PeriodClosed = new(409, "gl.period_closed");
  public static readonly ApiError AccountInactive = new(409, "gl.account_inactive");
  public static readonly ApiError Immutable = new(409, "gl.journal_immutable");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");

  // ⚠ A PRECONDITION, NOT A CORRECTION (T-268).
  //
  // 129 domain codes collapse into `request.invalid` and 128 of them say **fix your input**. This one
  // says *an active company must be selected before company-scoped operations* -- **you are not in a
  // state where this input means anything.** The remedy is a different call followed by the same request
  // unchanged, and a client that cannot tell it from a bad field name cannot offer the company picker.
  //
  // The status stays 400: it IS a client error. **The status is the category; the code is the
  // instruction**, and only the instruction differs.
  //
  // Declared here rather than in the shared `ApiErrors`, for the same reason `CompanyScopeDenied` above
  // is: `The_shared_api_project_names_no_business_concept` refuses a business noun in BuildingBlocks.
  // **The repetition across mappers is that rule being obeyed, not duplication** -- the gate refused the
  // shared version of this very constant.
  public static readonly ApiError CompanySelectionRequired = new(400, "company.selection_required");

  // ⚠ THE DOMAIN MESSAGE IS ATTACHED HERE BECAUSE THIS IS THE LAST PLACE IT EXISTS (T-261).
  //
  // Ninety-six call sites hand an already-mapped `ApiError` straight to `ApiProblems.Problem` and never
  // see the original `Error`. Attaching the message to the result is one edit per mapper; passing it
  // alongside would have been ninety-six.
  //
  // `ApiError.ShowsDetail` decides whether it reaches the caller: an authorization refusal (401/403)
  // drops it unless that code opted in, because `branch.scope_denied` has nine different messages behind
  // it and showing them would separate a branch that does not exist from one that is forbidden.
  public static ApiError Map(Error error) =>
    MapCore(error).Explaining(error.Message, error.Field);

  private static ApiError MapCore(Error error)
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
      "Gl.FiscalCalendarAmbiguous" => Conflict,
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
      "Company.SelectionRequired" => CompanySelectionRequired,
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
      // `DEC-DEP-0027` as amended -- see `UniqueConflict`. Fires only where no handler resolved the
      // context; names no index, because this switch cannot know which one lost.
      "Persistence.UniqueConstraint" => UniqueConflict,
      _ => ApiErrors.WriteFailure
    };
  }
}
