using System.Collections.ObjectModel;

namespace SSAS.GL.Application.Reads;

// THE TWO AUTHORIZATION DIMENSIONS, PROVEN, IN ONE VALUE (DEC-GL-0004, OD-GL-0005).
//
// ================================================================================================
// HOLDING ONE OF THESE IS PROOF THAT BOTH DIMENSIONS WERE CHECKED, LIVE, JUST NOW.
// ================================================================================================
//
// It cannot be constructed: the constructor is private and the only factory is internal to this assembly,
// called from exactly one place — `GlScopeResolver`, which checks the functional permission and resolves
// the authorized company set against live state, and refuses if either fails.
//
// EVERY GL READ REQUIRES ONE. That is the whole design: a read that omitted a scope predicate is not
// something a reviewer has to notice, because it is not something a caller can express. There is no
// overload without it, no default, and no way to fabricate a scope meaning "everything".
//
// ---- TWO DIMENSIONS, NOT THREE, AND THAT IS A RULING RATHER THAN A SIMPLIFICATION.
//
// `EmployeeReadScope` carries company AND branch because HR data is branch-aware. `OD-GL-0005` declined the
// branch dimension for V1, so this carries tenant and company only. It is a SMALLER scope object, not a
// weaker one — still unforgeable, still built by one resolver against live state, still refusing an empty
// set. If GL ever gains a branch dimension, this type grows a third set and every call site is forced to
// acknowledge it by the compiler.
//
// ---- WHY A MATERIALIZED SET RATHER THAN A MODE.
//
// The caller's request modes describe what was ASKED FOR. By the time a scope exists they have been
// collapsed into a concrete identifier list, so a query composes a predicate over values rather than
// branching on intent. "All companies" is therefore a LIST, never the absence of a condition
// (`BR-PLT-0016`, `ADR-023` decision 22).
//
// ---- WHY THE SET CANNOT BE EMPTY.
//
// An empty authorized set REFUSES the read (`ADR-025` decision 10, `AC-GL-0014`). Enforcing that at
// CONSTRUCTION means the "empty means everything" bug is unrepresentable rather than guarded against:
// `WHERE CompanyId IN ()` never reaches SQL because no scope with an empty set exists.
//
// ---- WHY THIS IS NOT HR'S `AuthorizedCompanyScope`, AND THE TRIGGER FOR MAKING IT SO.
//
// `ADR-012` puts that type out of reach, and `ADR-027` decision 4 names PROMOTION into
// `SSAS.BuildingBlocks.Domain` as the sanctioned answer when two modules need one type. Decision 4
// SANCTIONS promotion; it does not mandate it per package — and it is explicit that a promotion is "a
// deliberate, reviewed change to shared foundations, not a side effect of a feature package needing a
// type". A list of identifiers behind a guarded constructor is honestly too little type to spend a
// shared-foundations review on.
//
// **THE TRIGGER, WRITTEN WHERE IT WILL BE FOUND: a THIRD consumer.** Two modules each carrying a guarded
// identifier list is duplication nobody trips over. Three is where the shapes start to drift, and drift in
// a scope type is a security defect rather than an inconvenience — so the third module that needs one
// raises the promotion as its own reviewed change, and does not simply write a fourth copy.
public sealed class GlReadScope
{
  private GlReadScope(Guid tenantId, IReadOnlyList<Guid> companyIds)
  {
    TenantId = tenantId;
    CompanyIds = companyIds;
  }

  // The trusted tenant the scope was resolved within. Carried so a query STATES the invariant it depends on
  // rather than inheriting it from the global filter.
  public Guid TenantId { get; }

  public IReadOnlyList<Guid> CompanyIds { get; }

  // `internal` and called from exactly one place. The empty check is here rather than in the resolver so it
  // holds for every future caller of the factory, not merely for the one that exists today.
  internal static GlReadScope? Create(Guid tenantId, IReadOnlyList<Guid> companyIds)
  {
    ArgumentNullException.ThrowIfNull(companyIds);

    if (tenantId == Guid.Empty || companyIds.Count == 0)
    {
      return null;
    }

    return new GlReadScope(tenantId, new ReadOnlyCollection<Guid>([.. companyIds]));
  }
}
