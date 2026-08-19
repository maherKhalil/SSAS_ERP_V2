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

      // ---- EVERYTHING ELSE, including genuine storage and routing failure, keeps server semantics.
      _ => ApiErrors.WriteFailure
    };
  }
}
