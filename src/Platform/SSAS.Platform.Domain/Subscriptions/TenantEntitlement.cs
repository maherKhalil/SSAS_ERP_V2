using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// ENTITLEMENT RESOLUTION — THE CENTRE OF THE MODEL (FP-014, `OD-SUB-0011`, `OD-SUB-0017`).
//
// ---- WHY THIS IS A PURE FUNCTION OVER RECORDS RATHER THAN A STORED PROJECTION.
//
// `OD-SUB-0004` ruled the per-tenant assignment resolved **per request behind a cache invalidated on
// subscription change, never TTL-refreshed** — and `DEC-SUB-0004` requires the subscription surface to keep
// working while the tenant's ERP database is unavailable. A stored projection would be a second source of
// truth that could disagree with the history, and reconstructing a past entitlement (`REQ-SUB-0028`) would
// stop being a query.
//
// So: given the records, compute. Nothing here reads a database, and the same function answers "what is
// entitled now" and "what was entitled last March" by varying one argument.
//
// ---- THE `max` IS NOT AN IMPLEMENTATION DETAIL. IT IS THE INVARIANT.
//
// `OD-SUB-0011` says a grant may raise and never lower. `TenantEntitlementGrant.RaiseLimit` refuses a
// lowering value at write time, which is the loud half. This is the structural half: because a cap resolves
// as `max(plan, grants)`, **a grant that somehow named a lower value cannot lower anything** — whatever
// path wrote it, whatever a future author forgets. Modules are the same shape one dimension down: a union
// can only add members.
public static class TenantEntitlement
{
  // ---- WHICH SUBSCRIPTION RECORD IS IN FORCE: THE GREATEST `EffectiveFromUtc <= T`.
  //
  // Derived, never stored — there is no closing column and no in-force flag. This is the query that "exactly
  // one in force" reduces to, and it is why the schema indexes `(TenantId, EffectiveFromUtc DESC)`.
  //
  // Returns null when the tenant has no record that has taken effect. **That is an ordinary answer, not an
  // error**: with no backfill and no default plan (`CON-0001`), a tenant with no subscription is entitled to
  // nothing, and the caller must handle it rather than treat it as a fault.
  public static TenantSubscription? InForceAt(
    IEnumerable<TenantSubscription> subscriptions, DateTimeOffset instant)
  {
    ArgumentNullException.ThrowIfNull(subscriptions);

    return subscriptions
      .Where(subscription => subscription.HasTakenEffectAt(instant))
      .OrderByDescending(subscription => subscription.EffectiveFromUtc)
      .FirstOrDefault();
  }

  // ---- THE ENTITLED MODULE SET: PLAN ∪ GRANTS.
  //
  // A union, so a grant can only add. An expired grant simply is not in the set at that instant — expiry is
  // a value read here, not a state written anywhere.
  //
  // **An expired TERM entitles nothing.** `OD-SUB-0009` made expiry the one commercial event that refuses
  // login for the whole tenant, so a lapsed subscription resolves to an empty module set rather than to its
  // plan's modules. Module grants do not survive it either: a grant raises entitlement above a plan, and
  // there is no plan in force to raise above.
  public static IReadOnlySet<string> ModulesAt(
    TenantSubscription? inForce,
    SubscriptionPlan? plan,
    IEnumerable<TenantEntitlementGrant> grants,
    DateTimeOffset instant)
  {
    ArgumentNullException.ThrowIfNull(grants);

    if (inForce is null || inForce.HasExpiredAt(instant))
    {
      return new HashSet<string>(StringComparer.Ordinal);
    }

    var modules = new HashSet<string>(StringComparer.Ordinal);

    if (plan is not null && plan.SubscriptionPlanId == inForce.SubscriptionPlanId)
    {
      foreach (var granted in plan.ModuleGrants)
      {
        modules.Add(granted.ModuleKey.Value);
      }
    }

    foreach (var grant in grants)
    {
      if (grant.GrantKind == EntitlementGrantKind.ModuleGrant &&
        grant.TenantId == inForce.TenantId &&
        grant.IsInForceAt(instant) &&
        grant.ModuleKey is { } key)
      {
        modules.Add(key.Value);
      }
    }

    return modules;
  }

  // ---- A CAP: `max(plan, grants)`.
  //
  // Null means no cap is defined at all, which is not the same as a cap of zero and must not collapse into
  // one. A grant naming a limit the plan does not carry establishes it — from "undefined" to a number is
  // additive in the only sense that matters, because there was nothing to exceed.
  //
  // **A cap in force is a property of the subscription record live at that moment, not of the tenant**
  // (`OD-SUB-0017` × `OD-SUB-0008`). A metered overage must therefore be judged by calling this with the
  // instant the usage occurred, not with `UtcNow` — which is exactly why `instant` is a parameter and there
  // is no argument-free overload.
  public static long? LimitAt(
    TenantSubscription? inForce,
    SubscriptionPlan? plan,
    IEnumerable<TenantEntitlementGrant> grants,
    string limitKey,
    DateTimeOffset instant)
  {
    ArgumentNullException.ThrowIfNull(grants);
    ArgumentException.ThrowIfNullOrWhiteSpace(limitKey);

    if (inForce is null || inForce.HasExpiredAt(instant))
    {
      return null;
    }

    long? resolved = null;

    if (plan is not null && plan.SubscriptionPlanId == inForce.SubscriptionPlanId)
    {
      var planLimit = plan.Limits
        .FirstOrDefault(limit => string.Equals(limit.LimitKey, limitKey, StringComparison.Ordinal));

      if (planLimit is not null)
      {
        resolved = planLimit.LimitValue;
      }
    }

    foreach (var grant in grants)
    {
      if (grant.GrantKind != EntitlementGrantKind.LimitRaise ||
        grant.TenantId != inForce.TenantId ||
        !grant.IsInForceAt(instant) ||
        !string.Equals(grant.LimitKey, limitKey, StringComparison.Ordinal) ||
        grant.LimitValue is not { } granted)
      {
        continue;
      }

      // The structural half of `OD-SUB-0011`. `Math.Max` is why a lowering grant is inert rather than
      // merely refused — remove the write-time check and this still cannot lower a cap.
      resolved = resolved is { } current ? Math.Max(current, granted) : granted;
    }

    return resolved;
  }

  // Convenience for the one question the enablement seam actually asks. Kept as a named method rather than
  // left to each caller's `Contains`, so the ordinal comparison is decided once.
  public static bool IsModuleEntitled(IReadOnlySet<string> modules, ModuleKey moduleKey)
  {
    ArgumentNullException.ThrowIfNull(modules);
    ArgumentNullException.ThrowIfNull(moduleKey);

    return modules.Contains(moduleKey.Value);
  }
}
