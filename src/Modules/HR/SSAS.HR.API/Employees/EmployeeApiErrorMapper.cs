using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.API.Employees;

// ==================================================================================================
// EVERY WAY AN EMPLOYEE REQUEST CAN FAIL, AND THE ONE ANSWER EACH GETS (FP-006 api-contracts).
// ==================================================================================================
//
// ---- THE TWO SCOPE DIMENSIONS COLLAPSE TO ONE ANSWER EACH.
//
// Unauthorized, inactive, wrong-tenant and nonexistent all produce the SAME company code, and likewise for
// branch. A caller must not be able to tell which by comparing responses, because the difference is exactly
// the information they are not allowed to have: whether a company or branch exists at all.
//
// The two dimensions stay distinguishable FROM EACH OTHER, because a caller has to know whether to select a
// different company or a different branch, and neither answer reveals anything about the other.
//
// ---- AUTHORIZATION IS 403, NOT 500.
//
// The write boundaries refuse company- and branch-denied saves by throwing, and the unit of work returns the
// authorizer's own code. Those codes land here and become 403. They must never reach the default below —
// a refusal reported as a server failure tells the caller to retry something that will never succeed, and
// pages an operator for a working system.
//
// ---- AND THE DEFAULT IS DELIBERATELY A SERVER ERROR.
//
// An unmapped code means this table is out of date. Answering 400 would blame the caller for the gap and
// hide it; a 500 is visible and gets fixed. Nothing is guessed from the code's shape.
public static class EmployeeApiErrorMapper
{
  public static readonly ApiError NotFound = new(404, "employee.not_found");
  public static readonly ApiError NumberConflict = new(409, "employee.number_conflict");
  public static readonly ApiError NationalIdConflict = new(409, "employee.national_id_conflict");
  public static readonly ApiError TransitionInvalid = new(409, "employee.transition_invalid");
  public static readonly ApiError CompanyScopeDenied = new(403, "company.scope_denied");
  public static readonly ApiError BranchScopeDenied = new(403, "branch.scope_denied");
  public static readonly ApiError BranchSelectionRequired = new(409, "branch.selection_required");

  // ---- A SERVER FAILURE, AND A SPECIFIC ONE (T-091).
  //
  // 500 because nothing the caller did caused it and no change to their request avoids it — the same
  // reasoning the default arm gives. **But not the generic `WriteFailure`:** this one leaves state that
  // needs repairing, and an operator reading `hr.request_failed` in a log has no way to learn that. The
  // distinct code is what makes the half-state findable.
  public static readonly ApiError TerminationIncomplete = new(500, "employee.termination_incomplete");

  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);

    return error.Code switch
    {
      // ---- CALLER INPUT. Value objects and lifecycle arguments the caller got wrong.
      "Employee.InvalidEmployeeNumber" => ApiErrors.RequestInvalid,
      "Employee.InvalidNationalId" => ApiErrors.RequestInvalid,
      "Employee.InvalidFullName" => ApiErrors.RequestInvalid,
      "Employee.InvalidEmploymentDate" => ApiErrors.RequestInvalid,
      "Employee.TerminationBeforeEmployment" => ApiErrors.RequestInvalid,
      "Employee.TerminationIncomplete" => TerminationIncomplete,
      "Employee.InvalidTransitionReason" => ApiErrors.RequestInvalid,
      "Employee.InvalidTransferReason" => ApiErrors.RequestInvalid,
      "Employee.TransferDestinationUnchanged" => ApiErrors.RequestInvalid,
      "Employee.InvalidReadScope" => ApiErrors.RequestInvalid,
      "Employee.InvalidPagination" => ApiErrors.RequestInvalid,

      // ---- SCOPE. Generic within each dimension; never says which condition applied.
      // ---- THE COMPANY HEADER: SYNTAX IS THE CALLER'S PROBLEM, SCOPE IS NOT THEIR BUSINESS.
      //
      // A missing or malformed X-Company-Id is a MALFORMED REQUEST. The caller can already see their own
      // header, so saying so discloses nothing — and a generic denial would leave them guessing at a typo.
      "Company.SelectionRequired" => ApiErrors.RequestInvalid,
      "Company.InvalidSelectionFormat" => ApiErrors.RequestInvalid,

      // Every VALIDATION outcome collapses to one answer: unauthorized, inactive, wrong tenant and
      // nonexistent are indistinguishable, because the difference is precisely what must not be disclosed.
      "Employee.CompanyScopeDenied" => CompanyScopeDenied,
      "Company.InvalidSelection" => CompanyScopeDenied,

      // No trusted tenant or no usable session. Not a company answer at all — the caller is not in a state
      // to act, so it is the functional refusal rather than a scope one.
      "Company.ContextRequired" => ApiErrors.Forbidden,

      "Employee.BranchScopeDenied" => BranchScopeDenied,
      // Likewise the branch resolver: InvalidSelection is its single generic denial. NotFound and Inactive
      // are mapped alongside it so a future resolver change cannot turn one of them into a 500 that
      // discloses, by its very difference, that the branch exists.
      "Branch.InvalidSelection" => BranchScopeDenied,
      "Branch.NotFound" => BranchScopeDenied,
      "Branch.Inactive" => BranchScopeDenied,
      "Branch.ContextRequired" => BranchScopeDenied,
      "Branch.TenantAdministratorRequired" => BranchScopeDenied,
      // The sanctioned transfer channel's refusals. TransferInvalid covers a declaration that no longer
      // matches the entity; TransferNotPermitted covers a destination or recovery the caller may not use.
      "Branch.TransferInvalid" => BranchScopeDenied,
      "Branch.TransferNotPermitted" => BranchScopeDenied,
      "Branch.TransferAlreadyInProgress" => BranchScopeDenied,

      // No branch selected in the durable session for a branch-owned operation. Distinguishable because it
      // describes the caller's own SESSION, not any branch's existence or state — the fix is to select a
      // branch, which they cannot know from a generic denial.
      "Branch.SelectionRequired" => BranchSelectionRequired,
      "Branch.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,

      // ---- FUNCTIONAL PERMISSION. Discloses nothing about companies or branches, so it is safe to name.
      "Employee.ReadPermissionDenied" => ApiErrors.Forbidden,
      "Employee.WritePermissionDenied" => ApiErrors.Forbidden,

      // ---- DEPARTMENT (FP-007 Phase 3). ALL FOUR DESCRIBE THE REQUEST, so all four are 400.
      //
      // They are safe to distinguish from one another because the domain has ALREADY collapsed the
      // dangerous distinction: nonexistent, another tenant's and another company's department are one code
      // before they reach here, so none of these answers can be used to probe for a department outside the
      // caller's company. What remains — required, unusable, inactive, unchanged — is about the caller's
      // own request, and each one has a different fix.
      //
      // Mapped here even though FP-007 Phase 3 adds no department route: the CREATE route already returns
      // them, and a code with no arm falls through to a 500 that would disclose by its own strangeness.
      "Employee.DepartmentRequired" => ApiErrors.RequestInvalid,
      "Employee.DepartmentNotFound" => ApiErrors.RequestInvalid,
      "Employee.DepartmentInactive" => ApiErrors.RequestInvalid,
      "Employee.DepartmentUnchanged" => ApiErrors.RequestInvalid,
      "Employee.DepartmentHistoryImmutable" => ApiErrors.WriteFailure,

      // ---- POSITION (T-080). THE SAME FIVE-AND-ONE SHAPE AS DEPARTMENT ABOVE, AND FOR THE SAME REASONS.
      //
      // These were declared and unmapped, so every one answered `500 request.failed` — on
      // `POST /api/hr/employees` and on `POST /{employeeId}/change-position`, both of which reach this
      // mapper. The comment at `PositionEndpointRouteBuilderExtensions.cs:806` already said *"its
      // `Employee.Position*` arms are the ones that describe an unusable destination here"*. There were
      // none. **The route was right about what should exist and wrong about what did.**
      //
      // ---- THREE OF THE FIVE 400s ARE DISCLOSURE-SENSITIVE; TWO ARE NOT, AND THE DISTINCTION IS REAL.
      //
      // `NotFound`, `Inactive` and `InDifferentCompany` are the three a caller could otherwise use to probe
      // for a position outside their company (`BR-PLT-0002`), so they must be indistinguishable on the
      // wire. `Unchanged` names a position the caller can already read, and `Required` names none at all —
      // neither discloses anything. **They are 400 because they describe the request, which is the same
      // reason the department four are**, and the collapse the other three need falls out of that rather
      // than being imposed on them.
      "Employee.PositionRequired" => ApiErrors.RequestInvalid,
      "Employee.PositionNotFound" => ApiErrors.RequestInvalid,
      "Employee.PositionInactive" => ApiErrors.RequestInvalid,
      "Employee.PositionUnchanged" => ApiErrors.RequestInvalid,
      "Employee.PositionInDifferentCompany" => ApiErrors.RequestInvalid,

      // ---- EXPLICIT DESPITE MATCHING THE DEFAULT, AND THIS ARM IS NOT REDUNDANT.
      //
      // History immutability is a violated invariant, not a caller error: nothing the caller sends can
      // cause it and nothing they send differently would avoid it. A 500 is the honest answer, exactly as
      // `Employee.DepartmentHistoryImmutable` above answers it.
      //
      // **It is written out because the fallthrough producing the same status is a coincidence, not a
      // decision.** Delete this line and the wire behaviour is identical — which is precisely why it must
      // stay: `DepartmentHistoryImmutable` has no such comment, and its 500 had to be read as an inference
      // from the arm existing rather than as a recorded reason. That ambiguity is the thing being avoided
      // here, and the guard in `ModuleErrorMappingArchitectureTests` sees this arm only because it reads
      // the source text rather than calling `Map`.
      "Employee.PositionHistoryImmutable" => ApiErrors.WriteFailure,

      "Employee.InvalidActor" => ApiErrors.Forbidden,
      "Authorization.Unauthorized" => ApiErrors.Forbidden,

      // ---- SCOPED ABSENCE. Unknown, cross-tenant, cross-company and unauthorized-branch employees are all
      // NotFound, so the read cannot be used to prove an identifier exists somewhere unreachable.
      "Employee.NotFound" => NotFound,

      // ---- CONFLICT.
      "Employee.NumberConflict" => NumberConflict,
      "Employee.NationalIdConflict" => NationalIdConflict,
      "Employee.InvalidTransition" => TransitionInvalid,
      "Employee.TransferAfterTermination" => TransitionInvalid,
      "Employee.BranchHistoryImmutable" => TransitionInvalid,
      "Employee.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,
      "Persistence.ConcurrencyConflict" => ApiErrors.ConcurrencyConflict,

      // The database had the last word on uniqueness. Same answer as the pre-check, so a race and a
      // sequential duplicate are indistinguishable to the caller.
      "Persistence.UniqueConstraint" => NumberConflict,

      // ================================================================================================
      // FP-009. EVERY IMPORT AND EXPORT CODE HAS AN ARM, SO NOTHING FALLS TO THE 500 BELOW.
      // ================================================================================================
      //
      // The default is deliberately a server error, and that is exactly why this block must be complete: an
      // unmapped code means this table is out of date, and a 500 makes the gap visible. Before these arms
      // existed EVERY FP-009 failure — a bad header, an exceeded cap, an invalid import key — answered 500,
      // which would have told a caller to retry something that will never succeed.
      //
      // ---- THE FILE CONTRACT'S FAILURES ARE 400s. They describe the caller's own submission.
      "EmployeeImport.HeaderMissing" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnUnknown" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnMissing" => ApiErrors.RequestInvalid,
      "EmployeeImport.HeaderColumnDuplicated" => ApiErrors.RequestInvalid,
      "EmployeeImport.RowShapeInvalid" => ApiErrors.RequestInvalid,
      "EmployeeImport.EmploymentDateInvalid" => ApiErrors.RequestInvalid,
      "EmployeeImport.DuplicateWithinFile" => ApiErrors.RequestInvalid,
      "EmployeeImport.StatusNotCreatable" => ApiErrors.RequestInvalid,

      // The caps name a limit the caller exceeded, so they too describe the request (`DEC-DOC-0005`).
      "EmployeeImport.RowLimitExceeded" => ApiErrors.RequestInvalid,
      "EmployeeImport.ByteLimitExceeded" => ApiErrors.RequestInvalid,

      // ---- THE RUN RECORDS' OWN DOMAIN REFUSALS.
      //
      // `InvalidImportKey` and `InvalidFileName` are caller input. `InvalidCounts` and `InvalidColumnSet`
      // are NOT — no caller can express them, because the counts and the column set are computed by the
      // pipeline. If one is ever returned, the pipeline's arithmetic is wrong, which is a server fault and
      // is answered as one.
      "EmployeeImportRun.InvalidImportKey" => ApiErrors.RequestInvalid,
      "EmployeeImportRun.InvalidFileName" => ApiErrors.RequestInvalid,
      // ---- 500, CORRECTED IN T-096, AND IT BRINGS THIS SITE INTO LINE WITH T-080's RULING.
      //
      // T-080 ruled 500 at the import-contracts site and gave the reason: `ImportEmployeesCommandHandler`
      // already refuses a missing actor with `Employee.InvalidActor` (403), so **reaching the aggregate's
      // own actor guard means the handler's precondition passed and the aggregate refused anyway** — an
      // internal inconsistency, not a caller fault. `AuthenticationSubject.Create` caps the subject at the
      // same length the aggregate checks, so the gap is unreachable.
      //
      // **Answering 403 told a caller they lacked authority when the system had reached an impossible
      // state.** One site was right by that ruling and this one was never brought into line.
      "EmployeeImportRun.InvalidActor" => ApiErrors.WriteFailure,
      "EmployeeImportRun.InvalidCounts" => ApiErrors.WriteFailure,
      "EmployeeExportRun.InvalidColumnSet" => ApiErrors.WriteFailure,

      // ---- EVERYTHING ELSE, including genuine storage and routing failure, keeps server semantics.
      _ => ApiErrors.WriteFailure
    };
  }
}
