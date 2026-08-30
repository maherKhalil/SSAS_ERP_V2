using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Positions;

// NOTHING HERE NAMES DATABASE TOPOLOGY OR ANOTHER TENANT'S DATA (ADR-023, ADR-025).
//
// Scope refusals — a company the caller may not reach — are answered by the Platform boundaries with their
// own generic errors and are never restated here, so the HR surface cannot be used to probe for the
// existence of identifiers it is not allowed to see. This mirrors `EmployeeErrors` and `DepartmentErrors`
// exactly.
//
// ---- THE FILE IS IN TWO HALVES, AND THE SPLIT IS MEANINGFUL.
//
// Above the Phase 2 banner are the refusals an AGGREGATE can decide alone — an invalid code, a band that is
// half-priced or out of order, a lifecycle transition that does not exist. Below it are the ones that need a
// repository lookup or application orchestration to reach. Phase 1 carries only ONE of those, and says why.
public static class PositionErrors
{
  // ---- POSITION IDENTITY.
  public static readonly Error InvalidCode =
    new("Position.InvalidCode", "The position code is invalid.",
    Field: "code");

  public static readonly Error InvalidTitle =
    new("Position.InvalidTitle", "The position title is invalid.",
    Field: "title");

  public static readonly Error InvalidActor =
    new("Position.InvalidActor", "A trusted lifecycle actor is required.");

  // ---- GRADE IDENTITY.
  //
  // SEPARATE CONSTANTS PER LADDER, not one shared `InvalidGradeCode`. `DEC-POS-0005` made the two grades
  // separate aggregates, and a refusal that cannot say WHICH ladder rejected the value would be answered
  // identically for two different mistakes.
  public static readonly Error InvalidJobGradeCode =
    new("Position.InvalidJobGradeCode", "The job grade code is invalid.",
    Field: "code");

  public static readonly Error InvalidJobGradeName =
    new("Position.InvalidJobGradeName", "The job grade name is invalid.",
    Field: "name");

  public static readonly Error InvalidSalaryGradeCode =
    new("Position.InvalidSalaryGradeCode", "The salary grade code is invalid.",
    Field: "code");

  public static readonly Error InvalidSalaryGradeName =
    new("Position.InvalidSalaryGradeName", "The salary grade name is invalid.",
    Field: "name");

  // ---- RANK ORDER (DEC-POS-0006).
  //
  // Authoritative data, not derived from the code. Rank UNIQUENESS is a database concern — a unique index
  // per company and ladder — so its refusal arrives with the handler that translates index violations, in
  // Phase 2, rather than as a constant here that nothing can raise.
  public static readonly Error InvalidRankOrder =
    new("Position.InvalidRankOrder", "The grade rank order must be a positive number.",
    Field: "rankOrder");

  // ---- THE SALARY BAND (DEC-POS-0016, DEC-POS-0027).
  //
  // THREE DISTINCT REFUSALS, because they are three distinct mistakes and a caller can act on each
  // differently. A half-filled band is a form the user did not finish; a negative amount is a typo; an
  // out-of-order band is a misunderstanding of which field is which. Collapsing them into one
  // `InvalidSalaryBand` would leave the API unable to say which.
  public static readonly Error SalaryBandIncomplete =
    new("Position.SalaryBandIncomplete",
      "A salary band requires all three amounts, or none. A partially specified band is not accepted.");

  public static readonly Error SalaryBandNegative =
    new("Position.SalaryBandNegative", "A salary band amount cannot be negative.");

  public static readonly Error SalaryBandOutOfOrder =
    new("Position.SalaryBandOutOfOrder",
      "A salary band requires minimum, midpoint and maximum in non-decreasing order.");

  // ---- GRADE REFERENCE.
  public static readonly Error InvalidGradeReference =
    new("Position.InvalidGradeReference", "The grade reference is invalid.",
    Field: "salaryGradeId");

  // ---- LIFECYCLE.
  public static readonly Error InvalidTransition =
    new("Position.InvalidTransition", "The lifecycle transition is invalid.");

  // ---- POSITION HISTORY.
  //
  // The append-only assignment log is never edited. A correction is another position change, never a
  // rewrite — exactly as for the branch and department histories.
  public static readonly Error InvalidPositionAssignment =
    new("Position.InvalidPositionAssignment", "The employee position assignment is invalid.");

  // ================================================================================================
  // THE ONE PHASE 1 REFUSAL THAT NEEDS A REPOSITORY TO REACH — AND WHY IT IS HERE ANYWAY
  // ================================================================================================
  //
  // `DEC-POS-0013` refuses deactivating a grade while `Active` dependents reference it. The check is a
  // repository lookup and the OPERATION ships in Phase 2, so nothing in Phase 1 can raise this.
  //
  // It is declared now because the ruling that binds this phase named it, and because the alternative —
  // inventing the constant in Phase 2 alongside the handler — is how a rule ends up worded by whoever
  // happens to implement it rather than by the decision that created it. The asymmetry with
  // `DepartmentErrors`, where Phase 1 deliberately carried NO Phase 2 constants, is the deliberate part:
  // FP-007 had no ruling pointing forward, and this phase does.
  public static readonly Error GradeHasActiveDependents =
    new("Position.GradeHasActiveDependents",
      "The grade cannot be deactivated while active positions or grades reference it.");

  // ================================================================================================
  // PHASE 2 — THE REFUSALS THAT NEED A REPOSITORY, A SCOPE, OR AN ORCHESTRATION TO REACH
  // ================================================================================================
  //
  // Everything below arrives with the operations in FP-008 Phase 2 and is raised by a handler, never by an
  // aggregate. Each has a live raise site; the discipline the top half of this file states applies to the
  // bottom half unchanged.
  //
  // ---- WHY "NOT FOUND" IS THREE CONSTANTS RATHER THAN ONE.
  //
  // `api-contracts.md` gives the three families their own problem-code namespaces — `position.*`,
  // `job_grade.*`, `salary_grade.*` — and a mapper keyed on the error cannot emit `job_grade.not_found` from
  // a shared `NotFound`. The same reasoning already produced separate identity constants per ladder above.
  public static readonly Error PositionNotFound =
    new("Position.PositionNotFound", "The position was not found.");

  public static readonly Error JobGradeNotFound =
    new("Position.JobGradeNotFound", "The job grade was not found.");

  public static readonly Error SalaryGradeNotFound =
    new("Position.SalaryGradeNotFound", "The salary grade was not found.");

  // ---- UNIQUENESS, PER FAMILY, FOR THE SAME REASON.
  //
  // `BRULE-POS-0004` (code) and `BRULE-POS-0007` (rank). Each is TESTED by the handler and ENFORCED by a
  // unique index; the pre-check turns the common case into a named conflict rather than a raw persistence
  // failure, and is an optimisation of the message rather than the rule.
  public static readonly Error PositionCodeConflict =
    new("Position.PositionCodeConflict", "The position code already exists within the company.");

  public static readonly Error JobGradeCodeConflict =
    new("Position.JobGradeCodeConflict", "The job grade code already exists within the company.");

  public static readonly Error SalaryGradeCodeConflict =
    new("Position.SalaryGradeCodeConflict", "The salary grade code already exists within the company.");

  // Rank uniqueness is per company AND per ladder, so the two ladders cannot share one constant without
  // answering a job-grade collision with a salary-grade problem code.
  public static readonly Error JobGradeRankConflict =
    new("Position.JobGradeRankConflict", "The job grade rank order already exists within the company.");

  public static readonly Error SalaryGradeRankConflict =
    new("Position.SalaryGradeRankConflict",
      "The salary grade rank order already exists within the company.");

  // ================================================================================================
  // THE THREE WAYS A GRADE REFERENCE CAN BE INVALID (BRULE-POS-0009, BRULE-POS-0010, BRULE-POS-0011)
  // ================================================================================================
  //
  // The trio mirrors `DepartmentErrors.ParentNotFound` / `ParentInDifferentCompany` / `ParentInactive`
  // exactly, and it is shared by both referencing directions — Position -> JobGrade and JobGrade ->
  // SalaryGrade — because the three failures are the same three failures.
  //
  // ---- NAMING THE CROSS-COMPANY CASE IS NOT A DISCLOSURE, BECAUSE THE WIRE CANNOT TELL.
  //
  // All three map to one problem code, `<owner>.grade_invalid` — so a caller cannot distinguish "no such
  // grade" from "a grade you may not see". `api-contracts.md` names the second and third; the first is the
  // arm that table does not list, and it must exist or a reference to a nonexistent grade would answer
  // `job_grade.not_found` — a 404 about the grade, when the operation that failed was a write to a position.
  public static readonly Error GradeReferenceNotFound =
    new("Position.GradeReferenceNotFound", "The referenced grade was not found.");

  public static readonly Error GradeInDifferentCompany =
    new("Position.GradeInDifferentCompany", "The referenced grade belongs to a different company.");

  public static readonly Error GradeInactive =
    new("Position.GradeInactive", "The referenced grade is not active.");

  // ---- SCOPE, CONCURRENCY AND PAGINATION.
  //
  // Company scope and functional permission are separate questions with separate refusals (`ADR-025`
  // decision 8), and neither names the company or the identifier it refused.
  public static readonly Error CompanyScopeDenied =
    new("Position.CompanyScopeDenied", "The company is outside the caller's authorized scope.");

  public static readonly Error PermissionDenied =
    new("Position.PermissionDenied", "The caller lacks the required position permission.");

  // ⚠ TWO CODES, BECAUSE ONE CANNOT SAY WHICH PARAMETER TO FIX (T-260).
  //
  // The code these replaced covered three conditions -- page below one, page size below one,
  // page size above the maximum -- and all three answered the same 400 `request.invalid`. **A paging
  // client that fixes the wrong parameter retries and fails identically**, which is the same argument
  // that made a malformed identifier a 400 rather than a 404: a caller who cannot tell two conditions
  // apart cannot act on either.
  //
  // TWO rather than three: whether a page size was below one or above the maximum is visible to the
  // client from its own request. **And there is nowhere to say which bound** -- the problem document
  // carries `code`, `correlationId` and `resourceKey`, and no message field, so the code is the whole
  // channel.
  public static readonly Error InvalidPageNumber =
    new("Position.InvalidPageNumber", "The requested page number is out of range.");

  public static readonly Error InvalidPageSize =
    new("Position.InvalidPageSize", "The requested page size is out of range.");

  // The friendly refusal for a stale token, compared by the handler before the aggregate is mutated. The
  // database's own rowversion check is the rule; this is the message (`DEC-POS-0021`, `NFR-POS-0302`).
  public static readonly Error ConcurrencyConflict =
    new("Position.ConcurrencyConflict", "The record was modified by another operation.");
}
