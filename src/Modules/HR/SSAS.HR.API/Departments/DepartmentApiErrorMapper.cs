using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.API.Departments;

// ==================================================================================================
// EVERY WAY A DEPARTMENT REQUEST CAN FAIL, AND THE ONE ANSWER EACH GETS (FP-007 api-contracts).
// ==================================================================================================
//
// ---- ITS OWN TABLE, NOT AN EXTENSION OF THE EMPLOYEE ONE.
//
// The two surfaces share a grammar, not a vocabulary. Routing department Results through
// `EmployeeApiErrorMapper` is what produced the defect this table was written to fix: a department manager
// conflict answered `employee.number_conflict`, because that mapper's only arm for a unique-constraint
// violation was written for the employee-number pre-check. A shared table cannot stay honest once two
// resources disagree about what a shared persistence code means.
//
// ---- SCOPE COLLAPSES TO ABSENCE, AS IT DOES FOR EMPLOYEES.
//
// Unknown, another tenant's, another company's and out-of-scope departments are all
// `department.not_found`. The application already collapsed them; this does not undo it. A caller must not
// be able to discover that a department exists somewhere they cannot reach by comparing two responses.
//
// ---- AND THE DEFAULT IS DELIBERATELY A SERVER ERROR.
//
// An unmapped code means this table is out of date. Answering 400 would blame the caller for the gap and
// hide it; a 500 is visible and gets fixed. Nothing is guessed from the code's shape.
public static class DepartmentApiErrorMapper
{
  public static readonly ApiError NotFound = new(404, "department.not_found");
  public static readonly ApiError CodeConflict = new(409, "department.code_conflict");
  public static readonly ApiError TransitionInvalid = new(409, "department.transition_invalid");
  public static readonly ApiError HierarchyInvalid = new(409, "department.hierarchy_invalid");
  public static readonly ApiError HierarchyBusy = new(409, "department.hierarchy_busy");
  public static readonly ApiError ManagerInvalid = new(409, "department.manager_invalid");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- CALLER INPUT. Value objects and arguments the caller got wrong.
      "Department.InvalidCode" => ApiErrors.RequestInvalid,
      "Department.InvalidName" => ApiErrors.RequestInvalid,
      "Department.InvalidPagination" => ApiErrors.RequestInvalid,
      "Department.InvalidActor" => ApiErrors.RequestInvalid,

      // ---- ABSENCE, AND EVERYTHING THAT COLLAPSES INTO IT.
      //
      // The PARENT and MANAGER absences are deliberately NOT folded in here. They describe a value the
      // caller supplied in the body rather than the resource they addressed, so answering 404 would claim
      // the department they asked for does not exist — which is both wrong and more confusing than the
      // truth. They stay 409s below.
      "Department.NotFound" => NotFound,

      // ---- FUNCTIONAL AUTHORITY AND SCOPE.
      //
      // PermissionDenied is the resolver's refusal when the caller lacks the HR department permission. It
      // is a 403 rather than a 404 because the caller can already see which operation they attempted;
      // concealing it would tell them nothing they do not know.
      "Department.PermissionDenied" => ApiErrors.Forbidden,
      "Department.CompanyScopeDenied" => CompanyScopeDenied,
      "Company.InvalidSelection" => CompanyScopeDenied,
      "Company.SelectionRequired" => ApiErrors.RequestInvalid,
      "Company.InvalidSelectionFormat" => ApiErrors.RequestInvalid,
      "Company.ContextRequired" => ApiErrors.Forbidden,

      // ---- UNIQUENESS. The database had the last word, and it agrees with the pre-check.
      //
      // A race and a sequential duplicate are indistinguishable to the caller, exactly as they are on the
      // employee surface.
      "Department.CodeConflict" => CodeConflict,

      // ---- HIERARCHY. Each names a rule the caller's requested move would break.
      //
      // They stay separable from one another because each has a different fix — choose another parent,
      // reactivate the parent, or retry — and none of them reveals anything about a department the caller
      // could not already address.
      "Department.HierarchyCycle" => HierarchyInvalid,
      "Department.ParentIsSelf" => HierarchyInvalid,
      "Department.InvalidParent" => HierarchyInvalid,
      "Department.ParentNotFound" => HierarchyInvalid,
      "Department.ParentInDifferentCompany" => HierarchyInvalid,
      "Department.ParentInactive" => HierarchyInvalid,

      // Another caller holds the per-company hierarchy lock. The only distinguishable one, because it is
      // the only one where RETRYING IS THE CORRECT ADVICE.
      "Department.HierarchyMutationBusy" => HierarchyBusy,

      // ---- LIFECYCLE.
      "Department.InvalidTransition" => TransitionInvalid,
      "Department.HasActiveChildren" => TransitionInvalid,

      // ---- MANAGER. All describe the EMPLOYEE named in the body, never the department addressed.
      //
      // Collapsed to one code on purpose: nonexistent, another company's and terminated would otherwise let
      // a department caller probe the employee set, which they may hold no permission for at all.
      "Department.ManagerEmployeeNotFound" => ManagerInvalid,
      "Department.ManagerInDifferentCompany" => ManagerInvalid,
      "Department.ManagerTerminated" => ManagerInvalid,
      "Department.InvalidManagerAssignment" => ManagerInvalid,
      "Department.ManagerNotAssigned" => ManagerInvalid,

      // ---- CONCURRENCY.
      //
      // The department's own code and the shared persistence one answer identically: the caller reloads and
      // retries either way, and which layer noticed is not their business.
      "Department.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,
      "Persistence.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,

      // ---- THE SHARED UNIQUE-CONSTRAINT CODE MEANS "CODE ALREADY TAKEN" *HERE*.
      //
      // On create and update it is the unique index on NormalizedCode, so it is the same answer the
      // pre-check gives. It does NOT mean that on the assign-manager route, where the only unique
      // constraint is the association's primary key — that route pre-translates before calling this, so the
      // ambiguity is resolved by the caller who knows the operation rather than by a switch that does not.
      "Persistence.UniqueConstraint" => CodeConflict,

      "Department.DepartmentHistoryImmutable" => ApiErrors.WriteFailure,
      "Department.InvalidDepartmentAssignment" => ApiErrors.RequestInvalid,

      // ---- EVERYTHING ELSE, including genuine storage and routing failure, keeps server semantics.
      _ => ApiErrors.WriteFailure
    };
  }

  // ---- THE ASSIGN-MANAGER PRE-TRANSLATION (the registered miscoding fix).
  //
  // On that route a unique-constraint violation can only be PK_DepartmentManagers — two callers racing to
  // seat a manager, where the primary key on DepartmentId is what makes a second row unrepresentable. The
  // loser of that race and the loser of a rowversion check must be indistinguishable: both mean "somebody
  // got there first, reload and retry", and telling them apart would leak which internal check fired.
  //
  // It lives here rather than inside Map's switch because the switch sees only a code, and this
  // distinction depends on the OPERATION. Making the table operation-aware would mean every future caller
  // has to know which context they are mapping in — the exact coupling that produced the employee-mapper
  // defect this whole surface exists to correct.
  // ---- IT TRANSLATES TO HR'S OWN CONCURRENCY ERROR, NOT PLATFORM'S.
  //
  // `Persistence.ConcurrencyConflict` lives in SSAS.Platform.Domain, which HR.API does not reference and
  // must not (ADR-012) — the compiler refuses it, which is the boundary doing its job. HR's own
  // `Department.ConcurrencyConflict` says the same thing to a caller: both arms above map it to
  // `concurrency.conflict`, so the wire answer is identical and the module stays isolated.
  public static Error TranslateManagerConflict(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code == "Persistence.UniqueConstraint"
      ? DepartmentErrors.ConcurrencyConflict
      : error;
  }
}
