using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.API.Employees;

namespace SSAS.HR.API.Positions;

// ==================================================================================================
// EVERY WAY A POSITION-FAMILY REQUEST CAN FAIL, AND THE ONE ANSWER EACH GETS (FP-008 api-contracts).
// ==================================================================================================
//
// ---- ITS OWN TABLE, AND THREE NAMESPACES INSIDE IT.
//
// `api-contracts.md` gives the three aggregates their own problem-code namespaces — `position.*`,
// `job_grade.*`, `salary_grade.*` — and routes none of them through `EmployeeApiErrorMapper` or
// `DepartmentApiErrorMapper`. `DEC-DEP-0026` records why that separation is not fussiness: a shared table
// once answered a department manager conflict with `employee.number_conflict`, because its only
// unique-constraint arm had been written for the employee-number pre-check. A shared table cannot stay
// honest once two resources disagree about what a shared persistence code means.
//
// The same logic applies WITHIN this file, which is why the domain carries per-family error constants
// rather than one shared `CodeConflict`: a switch keyed on the error code cannot know which family the
// caller addressed, so the family has to be in the code.
//
// ---- SCOPE COLLAPSES TO ABSENCE.
//
// Unknown, another tenant's, another company's and out-of-scope records are all `*.not_found`. The
// application already collapsed them; this does not undo it. A caller must not be able to discover that a
// position exists somewhere they cannot reach by comparing two responses (`BR-PLT-0002`).
//
// ---- THE GRADE-REFERENCE TRIO IS ONE WIRE ANSWER, DELIBERATELY.
//
// `GradeReferenceNotFound`, `GradeInDifferentCompany` and `GradeInactive` all map to `*.grade_invalid`.
// `api-contracts.md` names the second and third; the first exists because a reference to a nonexistent
// grade must not answer `job_grade.not_found` — a 404 about the grade, when the operation that failed was a
// write to a position. **The wire equivalence is the contract, not the error identity**: what must hold is
// that a caller cannot tell whether the grade is missing or merely invisible to them, and that is satisfied
// by the three arms sharing a code. If any is renamed, all three must move together.
//
// ---- AND THE DEFAULT IS DELIBERATELY A SERVER ERROR.
//
// An unmapped code means this table is out of date. Answering 400 would blame the caller for the gap and
// hide it; a 500 is visible and gets fixed. Nothing is guessed from the code's shape.
public static class PositionApiErrorMapper
{
  // ---- POSITION.
  public static readonly ApiError PositionNotFound = new(404, "position.not_found");
  public static readonly ApiError PositionCodeConflict = new(409, "position.code_conflict");
  public static readonly ApiError PositionGradeInvalid = new(422, "position.grade_invalid");
  public static readonly ApiError PositionTransitionInvalid = new(409, "position.transition_invalid");

  // ---- JOB GRADE.
  public static readonly ApiError JobGradeNotFound = new(404, "job_grade.not_found");
  public static readonly ApiError JobGradeCodeConflict = new(409, "job_grade.code_conflict");
  public static readonly ApiError JobGradeRankConflict = new(409, "job_grade.rank_conflict");
  public static readonly ApiError JobGradeHasDependents = new(422, "job_grade.has_dependents");
  public static readonly ApiError JobGradeGradeInvalid = new(422, "job_grade.grade_invalid");
  public static readonly ApiError JobGradeTransitionInvalid = new(409, "job_grade.transition_invalid");

  // ---- SALARY GRADE.
  public static readonly ApiError SalaryGradeNotFound = new(404, "salary_grade.not_found");
  public static readonly ApiError SalaryGradeCodeConflict = new(409, "salary_grade.code_conflict");
  public static readonly ApiError SalaryGradeRankConflict = new(409, "salary_grade.rank_conflict");
  public static readonly ApiError SalaryGradeHasDependents = new(422, "salary_grade.has_dependents");
  public static readonly ApiError SalaryGradeAmountsInvalid = new(422, "salary_grade.amounts_invalid");
  public static readonly ApiError SalaryGradeTransitionInvalid = new(409, "salary_grade.transition_invalid");

  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");

  // ================================================================================================
  // THREE ENTRY POINTS, NOT ONE, BECAUSE THE FAMILY DECIDES THE NAMESPACE
  // ================================================================================================
  //
  // The shared codes — pagination, actor, permission, scope, concurrency and the persistence ones — mean
  // the same thing everywhere and are resolved by `MapShared`. What differs is the per-family half, and a
  // route knows which family it is: that is exactly the "resolved by the caller who knows the operation"
  // rule `DEC-DEP-0027` established for `Persistence.UniqueConstraint`.
  //
  // ---- `Persistence.UniqueConstraint` NEEDS NO CONTEXT-DEPENDENT ARM HERE.
  //
  // `api-contracts.md` flagged the two-unique-index case as new: a grade has both a code index and a rank
  // index, and they are not interchangeable. It is resolved WITHOUT an operation-aware switch, because the
  // handlers pre-check both and answer with the specific conflict; only a genuine race reaches this table,
  // and a race on either index means the same thing to the caller — somebody got there first. The code
  // conflict is the honest default for that, and no analog of the department's `TranslateManagerConflict`
  // is needed because no route here has a second unique constraint with a different meaning.
  public static ApiError MapPosition(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      "Position.InvalidCode" => ApiErrors.RequestInvalid,
      "Position.InvalidTitle" => ApiErrors.RequestInvalid,
      "Position.PositionNotFound" => PositionNotFound,
      "Position.PositionCodeConflict" => PositionCodeConflict,
      "Position.InvalidTransition" => PositionTransitionInvalid,

      // The reference trio, one answer. See the header for why the first arm exists at all.
      "Position.GradeReferenceNotFound" => PositionGradeInvalid,
      "Position.GradeInDifferentCompany" => PositionGradeInvalid,
      "Position.GradeInactive" => PositionGradeInvalid,

      // A race on the only unique index this family has.
      "Persistence.UniqueConstraint" => PositionCodeConflict,

      _ => MapShared(error)
    };
  }

  public static ApiError MapJobGrade(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      "Position.InvalidJobGradeCode" => ApiErrors.RequestInvalid,
      "Position.InvalidJobGradeName" => ApiErrors.RequestInvalid,
      "Position.InvalidRankOrder" => ApiErrors.RequestInvalid,
      "Position.JobGradeNotFound" => JobGradeNotFound,
      "Position.JobGradeCodeConflict" => JobGradeCodeConflict,
      "Position.JobGradeRankConflict" => JobGradeRankConflict,
      "Position.InvalidTransition" => JobGradeTransitionInvalid,

      // `DEC-POS-0013`: deactivation refused while Active positions reference it.
      "Position.GradeHasActiveDependents" => JobGradeHasDependents,

      // The salary grade this job grade points at, on the same three terms as above.
      "Position.GradeReferenceNotFound" => JobGradeGradeInvalid,
      "Position.GradeInDifferentCompany" => JobGradeGradeInvalid,
      "Position.GradeInactive" => JobGradeGradeInvalid,

      "Persistence.UniqueConstraint" => JobGradeCodeConflict,

      _ => MapShared(error)
    };
  }

  public static ApiError MapSalaryGrade(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      "Position.InvalidSalaryGradeCode" => ApiErrors.RequestInvalid,
      "Position.InvalidSalaryGradeName" => ApiErrors.RequestInvalid,
      "Position.InvalidRankOrder" => ApiErrors.RequestInvalid,
      "Position.SalaryGradeNotFound" => SalaryGradeNotFound,
      "Position.SalaryGradeCodeConflict" => SalaryGradeCodeConflict,
      "Position.SalaryGradeRankConflict" => SalaryGradeRankConflict,
      "Position.InvalidTransition" => SalaryGradeTransitionInvalid,
      "Position.GradeHasActiveDependents" => SalaryGradeHasDependents,

      // ---- THE BAND, AND WHY THREE REFUSALS SHARE ONE CODE.
      //
      // `DEC-POS-0027` made the band ATOMIC and the domain refuses three distinct mistakes — half-filled,
      // negative, out of order. All three are the caller's amounts being unusable, which is what
      // `salary_grade.amounts_invalid` says; the DETAIL that distinguishes them travels in the problem
      // document's message rather than in the code, so a client branches on one code and a human reads
      // which of the three it was.
      "Position.SalaryBandIncomplete" => SalaryGradeAmountsInvalid,
      "Position.SalaryBandNegative" => SalaryGradeAmountsInvalid,
      "Position.SalaryBandOutOfOrder" => SalaryGradeAmountsInvalid,

      "Persistence.UniqueConstraint" => SalaryGradeCodeConflict,

      _ => MapShared(error)
    };
  }

  // ---- WHAT EVERY FAMILY ANSWERS IDENTICALLY.
  //
  // Permission, scope, pagination, actor and concurrency say the same thing whichever aggregate was
  // addressed, so they are written once. Splitting them per family would triple the table and invite the
  // three copies to drift — which is the failure mode this file's header describes at a larger scale.
  private static ApiError MapShared(Error error) =>
    error.Code switch
    {
      "Position.InvalidPagination" => ApiErrors.RequestInvalid,
      "Position.InvalidActor" => ApiErrors.RequestInvalid,
      "Position.InvalidGradeReference" => ApiErrors.RequestInvalid,

      // ---- AUTHORITY AND SCOPE ARE BOTH 403, AND THAT IS NOT AN ACCIDENT.
      //
      // A caller lacking the permission and a caller lacking the company must be indistinguishable: telling
      // them apart would confirm that the company exists and is merely out of reach.
      "Position.PermissionDenied" => ApiErrors.Forbidden,
      "Position.CompanyScopeDenied" => CompanyScopeDenied,

      // ---- CONCURRENCY.
      //
      // HR's own code and the shared persistence one answer identically: the caller reloads and retries
      // either way, and which layer noticed is not their business.
      //
      // `Persistence.ConcurrencyConflict` lives in `SSAS.Platform.Domain`, which `HR.API` does not
      // reference and must not (`ADR-012`) — the compiler refuses it, which is the boundary doing its job.
      // The arm below matches on the STRING, and the wire equivalence with HR's own error is the contract:
      // if either is renamed the two arms must move together, or this silently starts disclosing the
      // difference it exists to hide.
      "Position.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,
      "Persistence.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,

      "Position.InvalidPositionAssignment" => ApiErrors.RequestInvalid,

      // ================================================================================================
      // TEN `Employee.*` CODES THIS SITE CAN RECEIVE (T-095, `DEC-L-079`).
      // ================================================================================================
      //
      // T-094's derived register found them: this site's routes invoke `ChangeEmployeePositionCommandHandler`
      // and the position-history read, both of which return `Employee.*` refusals directly. **Until now every
      // one of them answered `500 request.failed`.**
      //
      // ---- THE STATUSES ARE COPIED FROM `EmployeeApiErrorMapper`, NOT CHOSEN HERE.
      //
      // `DEC-L-079`: a status is a property of the CODE, not of the SITE. **`Employee.NotFound` answering 404
      // on an employee route and 500 here is a disclosure and an inconsistency at once** — a caller could
      // learn which surface refused them from the status alone.
      //
      // The CODE STRINGS are reused too: `employee.not_found` on a position route is accurate, because what
      // was not found is the employee. `Cross_site_agreement` asserts the statuses.
      "Employee.NotFound" => EmployeeApiErrorMapper.NotFound,
      "Employee.InvalidTransition" => EmployeeApiErrorMapper.TransitionInvalid,
      "Employee.CompanyScopeDenied" => EmployeeApiErrorMapper.CompanyScopeDenied,
      "Employee.BranchScopeDenied" => EmployeeApiErrorMapper.BranchScopeDenied,
      "Employee.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,
      "Employee.InvalidActor" => ApiErrors.Forbidden,
      "Employee.ReadPermissionDenied" => ApiErrors.Forbidden,
      "Employee.WritePermissionDenied" => ApiErrors.Forbidden,
      "Employee.InvalidReadScope" => ApiErrors.RequestInvalid,
      "Employee.PositionUnchanged" => ApiErrors.RequestInvalid,

      // Everything else, including genuine storage and routing failure, keeps server semantics.
      _ => ApiErrors.WriteFailure
    };
}
