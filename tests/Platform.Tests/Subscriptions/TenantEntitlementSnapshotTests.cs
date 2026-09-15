using System.Reflection;
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

  // ================================================================================================
  // ⚠⚠⚠ TRIPWIRE FOR AC-SUB-0022, WHICH IS VACUOUS TODAY AND WILL NOT STAY THAT WAY.
  // ================================================================================================
  //
  // *"The enabled-module response contains module keys and **nothing else** — no price, plan name, term,
  // cap, invoice or payment state. The criterion is failed by any additional field, including one that
  // seems harmless."*
  //
  // **THERE IS NO ENABLED-MODULE RESPONSE.** `ITenantModuleEntitlement` has exactly one member — one key
  // in, one boolean out — so a set-valued response cannot be assembled without ADDING A MEMBER TO THE
  // CONTRACT. *That is a fact about the interface's shape, not about how hard anyone searched.*
  //
  // ---- ⚠⚠⚠ SO WHY GUARD IT HERE, ON THIS RECORD, AND NOT ON THE MISSING ENDPOINT.
  //
  // ***THE HAZARD IS NAMED AND IT IS SITTING IN THIS FILE'S SUBJECT.*** Whoever builds "list my modules"
  // reaches for `PlanModules`, and `PlanModules` arrives bolted to `Term`, `PlanLimits` and
  // `SubscriptionPlanId` — **which are the term, the cap and the plan name that AC-SUB-0022 forbids by
  // name.** The obvious implementation returns this record or a projection of it, and the obvious
  // projection leaks three of the six fields the criterion lists.
  //
  // *They will be building a listing. They will not be building a disclosure rule.* **"And nothing else"
  // is the side effect nobody is looking at**, which is the whole test for whether a tripwire earns its
  // place — the same test `AC-SUB-0036` FAILED and is recorded as failing in `SubscriptionInvariantTests`.
  //
  // ---- ⚠⚠⚠ AND HERE IS WHAT THIS GUARD DOES **NOT** CATCH. READ THIS BEFORE TRUSTING ITS SILENCE.
  //
  //     WHAT IT CATCHES        a MEMBER being added to `TenantEntitlementSnapshot`
  //     WHAT IT DOES NOT       a ROUTE beginning to SERIALISE this record to a tenant caller
  //
  // ***SOMEBODY CAN SHIP THE LEAKING ENDPOINT TOMORROW WITHOUT TOUCHING ONE FIELD OF THIS RECORD, AND THIS
  // TEST STAYS GREEN THROUGHOUT.*** **Its silence is not a statement that nothing is disclosed.** A guard
  // whose comment implies it watches the other event is worse than no guard, because it teaches the next
  // reader that green means safe.
  //
  // ⚠⚠ A CONSUMPTION GUARD — *"nothing tenant-facing serialises this type"* — WOULD catch the real
  // hazard, and was REFUSED ON ITS PRECONDITION rather than on cost. **Measured 2026-09-05: 34 distinct
  // response types across 10 files, and 10 of those 10 files changed in the last 60 days — a 100% change
  // rate.** *And that 34 is a FLOOR, not the population*: the matcher sees `Results.Ok(new Foo(...))` and
  // is blind to every handler returning a variable. **An absence over a population I cannot close, moving
  // at 100% per sixty days, is a tripwire that fires on unrelated work and gets deleted — and deleting it
  // would take the disposition with it.**
  //
  // ⚠ SO WHAT THIS ACTUALLY BUYS IS THE MESSAGE, AND THAT IS STATED RATHER THAN IMPLIED. The next person
  // to add a field here reads this comment. That is the entire mechanism. *It is the route-ban lesson
  // again: redundant for detection, load-bearing for telling a reader what they have just acquired.*
  [Fact]
  [Trait("Tripwire", "AC-SUB-0022")]
  // ---- ⚠⚠⚠ PLANT-BACKED 2026-09-05. **PLANT:** `string? PlantedBillingReference = null` added to the
  // `TenantEntitlementSnapshot` record. **RED:** this test, `Assert.Equal() Failure: Collections differ`.
  // **REVERT → GREEN**, confirmed alongside the two subscription tripwires: `Passed: 3, Failed: 0`.
  //
  // ⚠⚠ **AND THIS IS THE STRONGEST OF THE NINE TRIPWIRES BECAUSE ITS SUBJECT EXISTS.** *The other eight
  // assert an ABSENCE and cannot fail while the absence holds — which is forever, until the one day it
  // matters — so their detection can only ever be shown by a plant.* ***THIS ONE IS A BIND OVER A LIVE TYPE:
  // it can fail for its stated reason on any ordinary day, and the plant only confirms what its shape
  // already promised.***
  public void The_snapshot_carries_exactly_these_members_and_three_of_them_are_forbidden_in_a_response()
  {
    Assert.Equal(
      ["Grants", "PlanLimits", "PlanModules", "SubscriptionPlanId", "TenantId", "Term"],
      typeof(TenantEntitlementSnapshot)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name)
        .OrderBy(name => name, StringComparer.Ordinal));

    // The three AC-SUB-0022 names, pinned against the record rather than restated in prose — a comment
    // saying "Term is forbidden" rots the day someone renames it; this fails.
    var members = typeof(TenantEntitlementSnapshot)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
    Assert.Contains("Term", members);                 // the TERM the criterion forbids
    Assert.Contains("PlanLimits", members);           // the CAP
    Assert.Contains("SubscriptionPlanId", members);   // the PLAN identity
    Assert.Contains("PlanModules", members);          // and the one field a response IS allowed to carry
  }
}
