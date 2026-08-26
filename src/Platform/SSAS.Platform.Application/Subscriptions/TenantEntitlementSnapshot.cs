using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Subscriptions;

// ==================================================================================================
// THE CACHED THING IS THE FACTS, NOT THE ANSWER. THAT IS THE WHOLE DESIGN (FP-014, T-040).
// ==================================================================================================
//
// ---- THE TENSION THIS RESOLVES, WHICH `business-rules.md` STATED BEFORE IT MATTERED.
//
// `OD-SUB-0004` ruled entitlement is resolved per request **behind a cache invalidated on subscription
// change, never a TTL refresh**. `BR-SUB-0012` requires a change to take effect immediately, and
// invalidation-on-change delivers that.
//
// **But expiry writes nothing.** No row changes when a term lapses: the clock passes `TermEndUtc` and
// the answer is simply different from then on. So there is **no invalidation event to hang expiry on**,
// and a cached value holding *enabled: true* is wrong from the instant of expiry and stays wrong until
// something unrelated evicts it. `DEC-L-033` made that load-bearing by moving expiry evaluation off the
// login path and onto the enablement gate — which is exactly where this cache sits.
//
// ---- SO THE CACHE HOLDS INPUTS AND THE DECISION IS TAKEN ON READ.
//
// A boolean has an expiry date. **The subscription record, the plan's modules and limits, and the
// tenant's grants do not** — they change only when something is written, which is precisely the event
// invalidation-on-change already covers. Cache those, evaluate against the clock on every read, and
// expiry needs no invalidation event because it was never a cached value in the first place.
//
// The alternative the passage offered — keying the entry so it cannot outlive `TermEndUtc` — was
// rejected: constructing that key requires knowing `TermEndUtc`, which requires the read the cache
// exists to avoid, and any approximation of it is a TTL wearing a different name. `OD-SUB-0004` ruled
// out a TTL explicitly.
//
// ---- IT CARRIES ITS PLAN'S IDENTITY BECAUSE A PLAN EDIT FANS OUT.
//
// A plan is shared. Amending its modules or limits changes the entitlement of **every tenant whose
// in-force record names it**, and an invalidation keyed only on `TenantId` leaves all of them stale.
// `SubscriptionPlanId` is carried here so the cache can evict by plan as well as by tenant.
public sealed record TenantEntitlementSnapshot(
  Guid TenantId,
  Guid? SubscriptionPlanId,
  SubscriptionTerm? Term,
  IReadOnlySet<string> PlanModules,
  IReadOnlyDictionary<string, long> PlanLimits,
  IReadOnlyList<EntitlementGrantFact> Grants)
{
  // A tenant with no subscription record in force. **An ordinary answer, not an error** — with no
  // backfill and no default plan (`CON-0001`), a tenant that has never been assigned one is entitled to
  // nothing, and the caller must handle it rather than treat it as a fault.
  public static TenantEntitlementSnapshot None(Guid tenantId) =>
    new(tenantId, null, null,
      new HashSet<string>(StringComparer.Ordinal),
      new Dictionary<string, long>(StringComparer.Ordinal),
      []);

  // ---- THE READ-TIME EVALUATION. EXPIRY LIVES HERE AND NOWHERE ELSE.
  //
  // `DEC-L-033`: an expired term denies every gated module. It does not deny authentication, and this
  // type could not make it: it answers one question — *is this module enabled* — and the authentication
  // path never asks it.
  public bool IsModuleEnabledAt(string moduleKey, DateTimeOffset instant)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

    if (Term is null || Term.HasExpiredAt(instant))
    {
      return false;
    }

    if (PlanModules.Contains(moduleKey))
    {
      return true;
    }

    // Plan ∪ additive grants. A union, so a grant can only add (`OD-SUB-0011`).
    return Grants.Any(grant =>
      grant.Kind == EntitlementGrantKind.ModuleGrant &&
      string.Equals(grant.ModuleKey, moduleKey, StringComparison.Ordinal) &&
      grant.IsInForceAt(instant));
  }

  // ---- `max(plan, grants)`, SO A GRANT CANNOT LOWER A CAP WHATEVER WROTE IT.
  //
  // Null means no cap is defined, which is not a cap of zero and must not collapse into one.
  public long? LimitAt(string limitKey, DateTimeOffset instant)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(limitKey);

    if (Term is null || Term.HasExpiredAt(instant))
    {
      return null;
    }

    long? resolved = PlanLimits.TryGetValue(limitKey, out var planValue) ? planValue : null;

    foreach (var grant in Grants)
    {
      if (grant.Kind != EntitlementGrantKind.LimitRaise ||
        !string.Equals(grant.LimitKey, limitKey, StringComparison.Ordinal) ||
        !grant.IsInForceAt(instant) ||
        grant.LimitValue is not { } granted)
      {
        continue;
      }

      resolved = resolved is { } current ? Math.Max(current, granted) : granted;
    }

    return resolved;
  }
}

// One grant, flattened for the cache. Kept as data rather than the domain entity because a cached
// aggregate is an aggregate nobody owns — and because the entity carries an EF identity this must not.
public sealed record EntitlementGrantFact(
  EntitlementGrantKind Kind,
  string? ModuleKey,
  string? LimitKey,
  long? LimitValue,
  DateTimeOffset EffectiveFromUtc,
  DateTimeOffset? ExpiresUtc)
{
  public bool IsInForceAt(DateTimeOffset instant) =>
    EffectiveFromUtc <= instant && (ExpiresUtc is null || instant <= ExpiresUtc);
}
