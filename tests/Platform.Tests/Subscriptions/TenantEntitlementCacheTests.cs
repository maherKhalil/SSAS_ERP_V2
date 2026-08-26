using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.Platform.Tests.Subscriptions;

// INVALIDATION ON CHANGE, ON TWO AXES (FP-014, `OD-SUB-0004`, T-040).
//
// The tenant axis is obvious. **The plan axis is the one flagged in T-006 before there was a cache to
// fail**: a plan is shared, so amending its modules changes the entitlement of every tenant whose
// in-force record names it, and an invalidation keyed only on `TenantId` leaves all of them stale —
// the one case where `BR-SUB-0012` fails for tenants nobody touched.
public sealed class TenantEntitlementCacheTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

  private static TenantEntitlementSnapshot For(Guid tenantId, Guid planId, params string[] modules) =>
    new(tenantId, planId, SubscriptionTerm.Perpetual(Noon),
      new HashSet<string>(modules, StringComparer.Ordinal),
      new Dictionary<string, long>(StringComparer.Ordinal), []);

  [Fact]
  public void A_stored_snapshot_is_returned()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var tenant = Guid.NewGuid();
    cache.Store(For(tenant, Guid.NewGuid(), "HR"));

    Assert.True(cache.TryGet(tenant, out var found));
    Assert.Contains("HR", found.PlanModules);
  }

  [Fact]
  public void An_unknown_tenant_is_a_miss() =>
    Assert.False(new InMemoryTenantEntitlementCache().TryGet(Guid.NewGuid(), out _));

  [Fact]
  public void Invalidating_a_tenant_evicts_only_that_tenant()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var plan = Guid.NewGuid();
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();
    cache.Store(For(first, plan, "HR"));
    cache.Store(For(second, plan, "HR"));

    cache.InvalidateTenant(first);

    Assert.False(cache.TryGet(first, out _));
    Assert.True(cache.TryGet(second, out _));
  }

  // ==================================================================================================
  // THE FAN-OUT. THIS IS CRITERION 5.
  // ==================================================================================================
  //
  // Three tenants on one shared plan, a fourth on another. Editing the shared plan must evict exactly
  // the three — and an implementation keyed only on `TenantId` would evict none of them.
  [Fact]
  public void Invalidating_a_plan_evicts_every_tenant_on_it_and_no_others()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var shared = Guid.NewGuid();
    var other = Guid.NewGuid();
    var onShared = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    var onOther = Guid.NewGuid();

    foreach (var tenant in onShared)
    {
      cache.Store(For(tenant, shared, "HR"));
    }

    cache.Store(For(onOther, other, "HR"));

    cache.InvalidatePlan(shared);

    Assert.All(onShared, tenant => Assert.False(cache.TryGet(tenant, out _)));
    Assert.True(cache.TryGet(onOther, out _));
  }

  // ---- A TENANT THAT MOVED PLANS IS NOT EVICTED BY ITS OLD PLAN.
  //
  // If the index kept the stale association, editing a plan the tenant no longer holds would evict it —
  // harmless in effect, but the index would be lying, and an index that lies is worse than none because
  // the next person reasons from it.
  [Fact]
  public void A_tenant_that_changed_plans_is_no_longer_evicted_by_the_old_one()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var oldPlan = Guid.NewGuid();
    var newPlan = Guid.NewGuid();
    var tenant = Guid.NewGuid();

    cache.Store(For(tenant, oldPlan, "HR"));
    cache.Store(For(tenant, newPlan, "HR", "Payroll"));

    cache.InvalidatePlan(oldPlan);

    Assert.True(cache.TryGet(tenant, out var still));
    Assert.Contains("Payroll", still.PlanModules);
  }

  // A tenant with no subscription has no plan to index against, and must not break the index.
  [Fact]
  public void A_tenant_with_no_subscription_caches_and_evicts_cleanly()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var tenant = Guid.NewGuid();

    cache.Store(TenantEntitlementSnapshot.None(tenant));

    Assert.True(cache.TryGet(tenant, out _));
    cache.InvalidateTenant(tenant);
    Assert.False(cache.TryGet(tenant, out _));
  }

  // ---- THERE IS NO TTL, AND ITS ABSENCE IS ASSERTED RATHER THAN ASSUMED.
  //
  // `OD-SUB-0004` ruled invalidation-on-change and never a TTL. An entry survives indefinitely, and it
  // is still not wrong about expiry — because the snapshot evaluates the term on read. A future author
  // adding a TTL "to be safe" would break the ruling and fail here.
  [Fact]
  public void An_entry_survives_indefinitely_because_nothing_expires_it()
  {
    var cache = new InMemoryTenantEntitlementCache();
    var tenant = Guid.NewGuid();
    cache.Store(For(tenant, Guid.NewGuid(), "HR"));

    Assert.True(cache.TryGet(tenant, out _));
    Assert.True(cache.TryGet(tenant, out _));
    Assert.True(cache.TryGet(tenant, out _));
  }
}
