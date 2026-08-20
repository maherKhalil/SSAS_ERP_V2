using System.Collections.ObjectModel;

namespace SSAS.HR.Application.Departments.Reads;

// THE TWO AUTHORIZATION DIMENSIONS A DEPARTMENT READ NEEDS, PROVEN, IN ONE VALUE (FP-007 Phase 2).
//
// ================================================================================================
// HOLDING ONE OF THESE IS PROOF THAT BOTH DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
// ================================================================================================
//
// It cannot be constructed: the constructor is private and the only factory is internal to this assembly,
// called from exactly one place — `DepartmentScopeResolver`, which checks the functional permission and
// resolves the authorized company set against live state, and refuses if either fails.
//
// EVERY DEPARTMENT READ REQUIRES ONE. That is the whole design: a read that omitted a scope predicate is not
// something a reviewer has to notice, because it is not something a caller can express. There is no overload
// without it, no default, and no way to fabricate a scope meaning "everything".
//
// ---- TWO DIMENSIONS, NOT THREE, AND THAT IS THE CLASSIFICATION.
//
// `EmployeeReadScope` carries a branch set. This carries none, because a Department is not branch-owned
// (ADR-026 decision 1) and its VISIBILITY is therefore not a branch question. A caller authorized for one
// branch of a company sees every department in that company, including departments whose current members
// all work elsewhere — because a department's existence is not a function of who is asking.
//
// An architecture guard asserts the absence of a branch dimension, so the next reader knows it was decided
// rather than forgotten.
//
// ---- WHY A MATERIALIZED SET RATHER THAN A MODE.
//
// By the time a scope exists, "the current company" or "every authorized company" has been collapsed into a
// concrete identifier list, so the query composes a predicate over values rather than branching on intent.
// "All companies" is therefore a list, never the absence of a condition.
public sealed class DepartmentReadScope
{
  private DepartmentReadScope(Guid tenantId, AuthorizedDepartmentCompanyScope companies)
  {
    TenantId = tenantId;
    Companies = companies;
  }

  // The trusted tenant the scope was resolved within. Carried so a query states the invariant it depends on
  // rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  public AuthorizedDepartmentCompanyScope Companies { get; }

  // INTERNAL, and called from one place. Making it public would turn every guarantee above into a comment.
  internal static DepartmentReadScope Create(Guid tenantId, AuthorizedDepartmentCompanyScope companies) =>
    new(tenantId, companies);
}

// The companies a department read may see, already proven and already materialized.
public sealed class AuthorizedDepartmentCompanyScope
{
  private AuthorizedDepartmentCompanyScope(IReadOnlyList<Guid> companyIds)
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

  internal static AuthorizedDepartmentCompanyScope Create(IReadOnlyList<Guid> companyIds) =>
    new(new ReadOnlyCollection<Guid>([.. companyIds]));
}
