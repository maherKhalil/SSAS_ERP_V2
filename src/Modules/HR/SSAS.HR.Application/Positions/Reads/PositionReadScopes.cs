using System.Collections.ObjectModel;

namespace SSAS.HR.Application.Positions.Reads;

// THE TWO AUTHORIZATION DIMENSIONS A POSITION READ NEEDS, PROVEN, IN ONE VALUE (FP-008 Phase 2).
//
// ================================================================================================
// HOLDING ONE OF THESE IS PROOF THAT BOTH DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
// ================================================================================================
//
// It cannot be constructed: the constructor is private and the only factory is internal to this assembly,
// called from exactly one place — `PositionScopeResolver`, which checks the functional permission and
// resolves the authorized company set against live state, and refuses if either fails.
//
// EVERY POSITION READ REQUIRES ONE. That is the whole design, carried unchanged from `DepartmentReadScope`:
// a read that omitted a scope predicate is not something a reviewer has to notice, because it is not
// something a caller can express. There is no overload without it, no default, and no way to fabricate a
// scope meaning "everything".
//
// ---- TWO DIMENSIONS, NOT THREE, AND THAT IS THE CLASSIFICATION (DEC-POS-0020).
//
// `EmployeeReadScope` carries a branch set. This carries none, because a Position is not branch-owned
// (`DEC-POS-0001`, `BRULE-POS-0003`) and its VISIBILITY is therefore not a branch question. A caller
// authorized for one branch of a company sees every position in that company, including positions whose
// current holders all work elsewhere — because a position's existence is not a function of who is asking.
//
// An architecture guard asserts the absence of a branch dimension, so the next reader knows it was decided
// rather than forgotten.
public sealed class PositionReadScope
{
  private PositionReadScope(Guid tenantId, AuthorizedPositionCompanyScope companies)
  {
    TenantId = tenantId;
    Companies = companies;
  }

  // The trusted tenant the scope was resolved within. Carried so a query states the invariant it depends on
  // rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  public AuthorizedPositionCompanyScope Companies { get; }

  // INTERNAL, and called from one place. Making it public would turn every guarantee above into a comment.
  internal static PositionReadScope Create(Guid tenantId, AuthorizedPositionCompanyScope companies) =>
    new(tenantId, companies);
}

// ================================================================================================
// THREE SCOPE TYPES, BECAUSE THERE ARE THREE VIEW PERMISSIONS (DEC-POS-0018)
// ================================================================================================
//
// A single shared scope type would be the same object whether it was obtained with `HR.Positions.View` or
// `HR.SalaryGrades.View`, and a salary-grade read handed a position scope would compile and run. The whole
// point of the `HR.SalaryGrades.View` separation — pay bands are more sensitive than job titles, on the
// `DEC-EMP-0030` precedent — would then rest on nobody making that mistake.
//
// So the type IS the proof of which permission was held. `SalaryGradeReadService` accepts only a
// `SalaryGradeReadScope`, and the only thing that produces one is the resolver method that checks
// `HR.SalaryGrades.View`.
public sealed class JobGradeReadScope
{
  private JobGradeReadScope(Guid tenantId, AuthorizedPositionCompanyScope companies)
  {
    TenantId = tenantId;
    Companies = companies;
  }

  public Guid TenantId { get; }

  public AuthorizedPositionCompanyScope Companies { get; }

  internal static JobGradeReadScope Create(Guid tenantId, AuthorizedPositionCompanyScope companies) =>
    new(tenantId, companies);
}

// THE SENSITIVE ONE. Obtainable only with `HR.SalaryGrades.View`, which exists as a separate permission
// precisely so that reading the organization chart does not also disclose the pay structure.
public sealed class SalaryGradeReadScope
{
  private SalaryGradeReadScope(Guid tenantId, AuthorizedPositionCompanyScope companies)
  {
    TenantId = tenantId;
    Companies = companies;
  }

  public Guid TenantId { get; }

  public AuthorizedPositionCompanyScope Companies { get; }

  internal static SalaryGradeReadScope Create(Guid tenantId, AuthorizedPositionCompanyScope companies) =>
    new(tenantId, companies);
}

// The companies a position-family read may see, already proven and already materialized.
//
// ---- ONE COMPANY-SCOPE TYPE FOR ALL THREE FAMILIES, AND THAT IS NOT AN INCONSISTENCY.
//
// The company dimension asks the same question whatever is being read, and it is answered by the same
// authority. What differs between the three families is the FUNCTIONAL permission, and that difference is
// already carried by the three scope types above. Duplicating this wrapper three times would state a
// distinction that does not exist.
public sealed class AuthorizedPositionCompanyScope
{
  private AuthorizedPositionCompanyScope(IReadOnlyList<Guid> companyIds)
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

  internal static AuthorizedPositionCompanyScope Create(IReadOnlyList<Guid> companyIds) =>
    new(new ReadOnlyCollection<Guid>([.. companyIds]));
}
