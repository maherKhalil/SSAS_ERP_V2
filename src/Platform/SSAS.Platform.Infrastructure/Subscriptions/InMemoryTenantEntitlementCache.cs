using System.Collections.Concurrent;
using SSAS.Platform.Application.Subscriptions;

namespace SSAS.Platform.Infrastructure.Subscriptions;

// INVALIDATION ON CHANGE, NEVER A TTL (FP-014, `OD-SUB-0004`).
//
// ---- WHY THERE IS NO EXPIRY POLICY HERE, WHICH LOOKS LIKE AN OMISSION AND IS NOT.
//
// Every cache in this product that holds a time-sensitive answer needs one. This one holds **facts**,
// not an answer: the subscription record in force, the plan's modules and limits, and the tenant's
// grants. Those change only when something is written, and a write is exactly what invalidates them.
//
// **Subscription expiry is not a write.** The clock passes `TermEndUtc` and nothing changes in the
// database — so it is evaluated on every read by `TenantEntitlementSnapshot.IsModuleEnabledAt`, against
// the term this entry carries. An entry may therefore live indefinitely and still never be wrong about
// expiry, which is the property a TTL would only approximate and `OD-SUB-0004` ruled out.
//
// ---- THE PLAN INDEX EXISTS FOR ONE FAILURE NOBODY WOULD SEE.
//
// A plan is shared. Amending its modules changes the entitlement of every tenant whose in-force record
// names it, and none of those tenants was touched. Keeping a plan → tenants index means
// `InvalidatePlan` can evict all of them; without it, `BR-SUB-0012` fails for tenants nobody edited and
// nothing reports it.
//
// ---- WHAT THIS IS NOT.
//
// **Process-local.** Two hosts hold two caches, and an invalidation on one does not reach the other.
// That is a real limit and it is stated rather than discovered: the product runs multi-instance
// (`ADR-018` names the racing case), so a distributed invalidation is owed before this is deployed
// behind more than one instance. It is out of scope here — nothing writes a subscription yet, so
// nothing can invalidate — and it is recorded so the next author does not assume coverage.
public sealed class InMemoryTenantEntitlementCache : ITenantEntitlementCache
{
  private readonly ConcurrentDictionary<Guid, TenantEntitlementSnapshot> byTenant = new();

  // Plan → the tenants currently cached against it. A set per plan, so eviction is O(tenants on plan)
  // rather than a scan of every entry.
  private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> tenantsByPlan = new();

  public bool TryGet(Guid tenantId, out TenantEntitlementSnapshot snapshot) =>
    byTenant.TryGetValue(tenantId, out snapshot!);

  public void Store(TenantEntitlementSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);

    // Remove the previous plan association first, or a tenant that moved plans would stay indexed under
    // the old one and be evicted by an edit to a plan it no longer holds. Harmless, but it would make
    // the index lie, and an index that lies is worse than no index.
    if (byTenant.TryGetValue(snapshot.TenantId, out var previous) &&
      previous.SubscriptionPlanId is { } previousPlan &&
      previousPlan != snapshot.SubscriptionPlanId &&
      tenantsByPlan.TryGetValue(previousPlan, out var previousSet))
    {
      previousSet.TryRemove(snapshot.TenantId, out _);
    }

    byTenant[snapshot.TenantId] = snapshot;

    if (snapshot.SubscriptionPlanId is { } planId)
    {
      tenantsByPlan.GetOrAdd(planId, _ => new ConcurrentDictionary<Guid, byte>())[snapshot.TenantId] = 0;
    }
  }

  public void InvalidateTenant(Guid tenantId)
  {
    if (byTenant.TryRemove(tenantId, out var removed) &&
      removed.SubscriptionPlanId is { } planId &&
      tenantsByPlan.TryGetValue(planId, out var set))
    {
      set.TryRemove(tenantId, out _);
    }
  }

  public void InvalidatePlan(Guid subscriptionPlanId)
  {
    if (!tenantsByPlan.TryRemove(subscriptionPlanId, out var tenants))
    {
      return;
    }

    foreach (var tenantId in tenants.Keys)
    {
      byTenant.TryRemove(tenantId, out _);
    }
  }
}
