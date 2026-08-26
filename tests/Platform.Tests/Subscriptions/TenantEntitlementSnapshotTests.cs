using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Subscriptions;

// THE CACHED SNAPSHOT, AND THE EXPIRY PROBLEM IT EXISTS TO SOLVE (FP-014, T-040).
//
// `OD-SUB-0004` ruled invalidation-on-change and never a TTL. **Expiry writes nothing** — the clock
// passes `TermEndUtc` and the answer is different from then on, with no row changed and therefore no
// invalidation event to hang it on. `DEC-L-033` made that load-bearing by moving expiry evaluation from
// the login path to the enablement gate, which is exactly where the cache sits.
//
// The resolution: **the snapshot caches facts, not the answer.** These tests are that claim.
public sealed class TenantEntitlementSnapshotTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.NewGuid();
  private static readonly Guid PlanId = Guid.NewGuid();

  private static TenantEntitlementSnapshot Snapshot(
    SubscriptionTerm? term = null,
    IEnumerable<string>? modules = null,
    IDictionary<string, long>? limits = null,
    IEnumerable<EntitlementGrantFact>? grants = null) =>
    new(TenantId, PlanId, term ?? SubscriptionTerm.Perpetual(Noon),
      new HashSet<string>(modules ?? ["HR"], StringComparer.Ordinal),
      new Dictionary<string, long>(limits ?? new Dictionary<string, long>(), StringComparer.Ordinal),
      [.. grants ?? []]);

  // ==================================================================================================
  // THE ONE THAT MATTERS: A CACHED ENTRY DOES NOT OUTLIVE `TermEndUtc`.
  // ==================================================================================================
  //
  // The snapshot is built ONCE, before the term ends, and never rebuilt — exactly as a cached entry
  // behaves when nothing writes. Advancing only the clock flips the answer.
  //
  // **Nothing is written and nothing is invalidated between the two assertions.** That is the whole
  // point: had the snapshot cached `enabled: true`, it would still say true after expiry and stay wrong
  // until something unrelated evicted it.
  [Fact]
  public void A_snapshot_cached_before_expiry_refuses_after_it_with_no_write_and_no_invalidation()
  {
    var cached = Snapshot(term: SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value);

    Assert.True(cached.IsModuleEnabledAt("HR", Noon.AddDays(29)));
    Assert.True(cached.IsModuleEnabledAt("HR", Noon.AddDays(30)));

    // One tick past the term. Same object, same facts, different clock.
    Assert.False(cached.IsModuleEnabledAt("HR", Noon.AddDays(30).AddTicks(1)));
    Assert.False(cached.IsModuleEnabledAt("HR", Noon.AddYears(1)));
  }

  [Fact]
  public void A_perpetual_term_never_expires_however_far_the_clock_advances()
  {
    var cached = Snapshot(term: SubscriptionTerm.Perpetual(Noon));

    Assert.True(cached.IsModuleEnabledAt("HR", Noon.AddYears(50)));
  }

  // An expired term denies everything, including modules a grant would otherwise have added: a grant
  // raises entitlement above a plan, and there is no plan in force to raise above.
  [Fact]
  public void An_expired_term_denies_a_module_a_live_grant_would_have_added()
  {
    var cached = Snapshot(
      term: SubscriptionTerm.Fixed(Noon, Noon.AddDays(1)).Value,
      grants: [new EntitlementGrantFact(
        EntitlementGrantKind.ModuleGrant, "Attendance", null, null, Noon, null)]);

    Assert.True(cached.IsModuleEnabledAt("Attendance", Noon));
    Assert.False(cached.IsModuleEnabledAt("Attendance", Noon.AddDays(2)));
  }

  // ==================================================================================================
  // PLAN ∪ GRANTS, AND `max(plan, grants)`.
  // ==================================================================================================

  [Fact]
  public void The_plans_modules_are_entitled() =>
    Assert.True(Snapshot(modules: ["HR", "Payroll"]).IsModuleEnabledAt("Payroll", Noon));

  [Fact]
  public void A_module_the_plan_does_not_carry_is_refused() =>
    Assert.False(Snapshot(modules: ["HR"]).IsModuleEnabledAt("Payroll", Noon));

  [Fact]
  public void A_grant_adds_a_module_the_plan_does_not_carry() =>
    Assert.True(Snapshot(
      modules: ["HR"],
      grants: [new EntitlementGrantFact(
        EntitlementGrantKind.ModuleGrant, "Payroll", null, null, Noon, null)])
      .IsModuleEnabledAt("Payroll", Noon));

  // A grant with its own end date is absent after it, without expiring the subscription.
  [Fact]
  public void An_expired_grant_stops_adding_its_module()
  {
    var cached = Snapshot(
      modules: ["HR"],
      grants: [new EntitlementGrantFact(
        EntitlementGrantKind.ModuleGrant, "Payroll", null, null, Noon, Noon.AddDays(7))]);

    Assert.True(cached.IsModuleEnabledAt("Payroll", Noon.AddDays(3)));
    Assert.False(cached.IsModuleEnabledAt("Payroll", Noon.AddDays(8)));
    Assert.True(cached.IsModuleEnabledAt("HR", Noon.AddDays(8)));
  }

  [Fact]
  public void A_tenant_with_no_subscription_is_entitled_to_nothing()
  {
    var none = TenantEntitlementSnapshot.None(TenantId);

    Assert.False(none.IsModuleEnabledAt("HR", Noon));
    Assert.Null(none.LimitAt("Seats", Noon));
    Assert.Null(none.SubscriptionPlanId);
  }

  // ---- THE CAP FLOOR, WHICH IS STRUCTURAL RATHER THAN CHECKED.
  //
  // A grant naming a value below the plan's cap cannot lower it, because resolution takes the maximum.
  // The write-time refusal lives in the domain; this is the half that holds whatever wrote the row.
  [Fact]
  public void A_grant_below_the_plan_cap_cannot_lower_it()
  {
    var cached = Snapshot(
      limits: new Dictionary<string, long> { ["Seats"] = 100 },
      grants: [new EntitlementGrantFact(
        EntitlementGrantKind.LimitRaise, null, "Seats", 5, Noon, null)]);

    Assert.Equal(100, cached.LimitAt("Seats", Noon));
  }

  [Fact]
  public void A_grant_above_the_plan_cap_raises_it() =>
    Assert.Equal(250, Snapshot(
      limits: new Dictionary<string, long> { ["Seats"] = 100 },
      grants: [new EntitlementGrantFact(
        EntitlementGrantKind.LimitRaise, null, "Seats", 250, Noon, null)])
      .LimitAt("Seats", Noon));

  // Undefined is not zero, and collapsing the two would silently cap a tenant at nothing.
  [Fact]
  public void An_undefined_cap_is_null_rather_than_zero() =>
    Assert.Null(Snapshot().LimitAt("Seats", Noon));

  // A cap is a property of the record live at that moment: an expired term has no cap, not a cap of nil.
  [Fact]
  public void An_expired_term_resolves_no_cap() =>
    Assert.Null(Snapshot(
      term: SubscriptionTerm.Fixed(Noon, Noon.AddDays(1)).Value,
      limits: new Dictionary<string, long> { ["Seats"] = 100 })
      .LimitAt("Seats", Noon.AddDays(2)));
}
