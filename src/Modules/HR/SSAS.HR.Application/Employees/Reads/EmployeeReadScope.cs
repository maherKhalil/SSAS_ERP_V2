using System.Collections.ObjectModel;

namespace SSAS.HR.Application.Employees.Reads;

// THE THREE AUTHORIZATION DIMENSIONS, PROVEN, IN ONE VALUE (FP-006C4, ADR-023 d22, ADR-025 d10).
//
// ================================================================================================
// HOLDING ONE OF THESE IS PROOF THAT ALL THREE DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
// ================================================================================================
//
// It cannot be constructed: the constructor is private and the only factory is internal to this assembly,
// called from exactly one place — EmployeeScopeResolver, which checks the functional permission, resolves
// the authorized company set and resolves the authorized branch set against live state, and refuses if any
// one of them fails.
//
// EVERY EMPLOYEE READ REQUIRES ONE. That is the whole design: a read that omitted a scope predicate is not
// something a reviewer has to notice, because it is not something a caller can express. There is no
// overload without it, no default, and no way to fabricate a scope meaning "everything".
//
// ---- WHY A MATERIALIZED SET RATHER THAN A MODE.
//
// The modes (`CurrentBranch`, `AllAuthorizedBranches`, …) describe what the CALLER ASKED FOR. By the time a
// scope exists they have been collapsed into concrete identifier lists, so the query composes a predicate
// over values rather than branching on intent. "All branches" is therefore a list, never the absence of a
// condition (`BR-PLT-0016`, `ADR-023` decision 22).
//
// ---- WHY THE SETS CANNOT BE EMPTY.
//
// An empty authorized set refuses the read (`ADR-025` decision 10, FP-006 authorization-model). Enforcing
// that at CONSTRUCTION means the "empty means everything" bug is unrepresentable rather than guarded
// against: `WHERE BranchId IN ()` never reaches SQL because no scope with an empty set exists.
public sealed class EmployeeReadScope
{
  private EmployeeReadScope(Guid tenantId, AuthorizedCompanyScope companies, AuthorizedBranchScope branches)
  {
    TenantId = tenantId;
    Companies = companies;
    Branches = branches;
  }

  // The trusted tenant the scope was resolved within. Carried so a query states the invariant it depends on
  // rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  public AuthorizedCompanyScope Companies { get; }

  public AuthorizedBranchScope Branches { get; }

  // INTERNAL, and called from one place. Making it public would turn every guarantee above into a comment.
  internal static EmployeeReadScope Create(
    Guid tenantId, AuthorizedCompanyScope companies, AuthorizedBranchScope branches) =>
    new(tenantId, companies, branches);
}

// The companies a read may see, already proven and already materialized.
public sealed class AuthorizedCompanyScope
{
  private AuthorizedCompanyScope(IReadOnlyList<Guid> companyIds)
  {
    CompanyIds = companyIds;
  }

  // NEVER EMPTY. A scope with no companies would compose a predicate matching nothing at best, and would be
  // mistaken for "unrestricted" at worst; the resolver refuses before one can be built.
  //
  // AND NEVER WRITEABLE. An IReadOnlyList<Guid> handed a Guid[] is castable straight back to Guid[], so the
  // "read-only" would be a suggestion: any code holding a scope could add a company to it after the
  // authorization that produced it had already passed. The wrapper below is what makes it a fact.
  public IReadOnlyList<Guid> CompanyIds { get; }

  internal static AuthorizedCompanyScope Create(IReadOnlyList<Guid> companyIds) =>
    new(new ReadOnlyCollection<Guid>([.. companyIds]));
}

// The branches a read may see, already proven and already materialized.
public sealed class AuthorizedBranchScope
{
  private AuthorizedBranchScope(IReadOnlyList<Guid> branchIds)
  {
    BranchIds = branchIds;
  }

  // NEVER EMPTY, and never writeable, for the same reasons.
  public IReadOnlyList<Guid> BranchIds { get; }

  internal static AuthorizedBranchScope Create(IReadOnlyList<Guid> branchIds) =>
    new(new ReadOnlyCollection<Guid>([.. branchIds]));
}
