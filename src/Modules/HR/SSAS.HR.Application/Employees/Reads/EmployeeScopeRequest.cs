namespace SSAS.HR.Application.Employees.Reads;

// WHICH BRANCHES THE CALLER IS ASKING FOR (FP-006 authorization-model, ADR-023 decision 22).
//
// A CLOSED CHOICE, so "no branch specified" is unrepresentable in the read contract. There is deliberately
// no `All` that means "everything": AllAuthorizedBranches means every branch CURRENTLY AUTHORIZED to this
// caller, materialized into the predicate.
public enum EmployeeBranchScopeMode
{
  // The default. The trusted execution branch, re-read live — not a value the caller supplies.
  CurrentBranch = 0,

  // An explicit subset of the caller's authorized set. A request naming anything outside it is REFUSED
  // rather than silently intersected: quietly narrowing a request would tell the caller they had seen
  // everything they asked for when they had not.
  SelectedAuthorizedBranches = 1,

  // Every branch currently authorized to this caller, materialized. Never the absence of a predicate.
  AllAuthorizedBranches = 2
}

// WHICH COMPANIES THE CALLER IS ASKING FOR (FP-006 authorization-model, ADR-025 decision 10).
//
// Milestone 1 exposes CurrentCompany for every read and additionally AllAuthorizedCompanies for search.
public enum EmployeeCompanyScopeMode
{
  // The selected company, proven by the five-step live validation before it is used.
  CurrentCompany = 0,

  // Every company currently authorized to this caller, materialized. Search only.
  AllAuthorizedCompanies = 1
}

// The caller's INTENT. It carries no authority: the resolver turns it into an EmployeeReadScope only after
// proving the functional permission and both scope dimensions against live state, and refuses otherwise.
public sealed record EmployeeScopeRequest(
  EmployeeCompanyScopeMode CompanyScope = EmployeeCompanyScopeMode.CurrentCompany,
  EmployeeBranchScopeMode BranchScope = EmployeeBranchScopeMode.CurrentBranch,
  // Required when BranchScope is SelectedAuthorizedBranches, and rejected otherwise — a stray list is a
  // malformed request rather than something to ignore.
  IReadOnlyCollection<Guid>? SelectedBranchIds = null);
