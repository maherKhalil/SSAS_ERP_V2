using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Subscriptions;

// ENTITLEMENT RESOLUTION (FP-014, T-035).
//
// The half of `OD-SUB-0011` that is structural rather than enforced: because a cap resolves as
// `max(plan, grants)`, a grant that somehow named a LOWER value cannot lower anything — whatever path wrote
// it. The write-time refusal is tested next door; this suite tests that removing it would still leave the
// invariant standing.
public sealed class TenantEntitlementResolutionTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid TenantId = Guid.NewGuid();

  private static SubscriptionPlan PlanWith(params (string Key, long Value)[] limits)
  {
    var plan = SubscriptionPlan.Create(
      PlanCode.Create("STD").Value, PlanName.Create("Standard").Value, "operator", Noon).Value;

    plan.GrantModule(ModuleKey.Create("HR").Value, "operator", Noon);
    plan.GrantModule(ModuleKey.Create("Payroll").Value, "operator", Noon);

    foreach (var (key, value) in limits)
    {
      plan.SetLimit(key, value, "operator", Noon);
    }

    return plan;
  }

  private static TenantSubscription SubscriptionFor(SubscriptionPlan plan, SubscriptionTerm term) =>
    TenantSubscription.Append(
      TenantId, plan.SubscriptionPlanId, Noon, null, term, "USD", "operator", null, null, Noon).Value;

  // ==================================================================================================
  // WHICH RECORD IS IN FORCE — DERIVED BY ORDERING, NEVER STORED.
  // ==================================================================================================

  [Fact]
  public void The_record_in_force_is_the_greatest_effective_from_at_or_before_the_instant()
  {
    var plan = PlanWith();
    var first = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var second = TenantSubscription.Append(
      TenantId, plan.SubscriptionPlanId, Noon.AddDays(10), Noon,
      SubscriptionTerm.Perpetual(Noon.AddDays(10)), "USD", "operator", null, null, Noon).Value;

    Assert.Equal(first, TenantEntitlement.InForceAt([first, second], Noon.AddDays(5)));
    Assert.Equal(second, TenantEntitlement.InForceAt([first, second], Noon.AddDays(10)));
    Assert.Equal(second, TenantEntitlement.InForceAt([first, second], Noon.AddDays(99)));
  }

  // ---- NO RECORD IS AN ORDINARY ANSWER, NOT AN ERROR.
  //
  // With no backfill and no default plan (`CON-0001`), a tenant with no subscription is entitled to nothing.
  // The caller must handle it; treating it as a fault is how a missing row becomes a 500 instead of a 403.
  [Fact]
  public void A_tenant_with_no_record_resolves_to_nothing_rather_than_failing()
  {
    Assert.Null(TenantEntitlement.InForceAt([], Noon));
    Assert.Empty(TenantEntitlement.ModulesAt(null, null, [], Noon));
    Assert.Null(TenantEntitlement.LimitAt(null, null, [], PlanLimit.Seats, Noon));
  }

  // ==================================================================================================
  // MODULES — PLAN ∪ GRANTS.
  // ==================================================================================================

  [Fact]
  public void The_module_set_is_the_plans_modules()
  {
    var plan = PlanWith();
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));

    var modules = TenantEntitlement.ModulesAt(inForce, plan, [], Noon);

    Assert.Equal(["HR", "Payroll"], modules.OrderBy(module => module, StringComparer.Ordinal));
  }

  [Fact]
  public void A_module_grant_adds_to_the_plans_modules()
  {
    var plan = PlanWith();
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var grant = TenantEntitlementGrant.GrantModule(
      TenantId, ModuleKey.Create("Attendance").Value, Noon, null, "operator", null, null, Noon).Value;

    var modules = TenantEntitlement.ModulesAt(inForce, plan, [grant], Noon);

    Assert.Contains("Attendance", modules);
    Assert.Equal(3, modules.Count);
  }

  // An expired grant is simply absent at that instant. Expiry is a value read here, never a state written.
  [Fact]
  public void An_expired_module_grant_is_not_in_the_set()
  {
    var plan = PlanWith();
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var grant = TenantEntitlementGrant.GrantModule(
      TenantId, ModuleKey.Create("Attendance").Value, Noon, Noon.AddDays(7), "operator", null, null,
      Noon).Value;

    Assert.Contains("Attendance", TenantEntitlement.ModulesAt(inForce, plan, [grant], Noon.AddDays(3)));
    Assert.DoesNotContain("Attendance", TenantEntitlement.ModulesAt(inForce, plan, [grant], Noon.AddDays(8)));
  }

  // ---- AN EXPIRED TERM ENTITLES NOTHING, AND GRANTS DO NOT SURVIVE IT.
  //
  // `OD-SUB-0009` made expiry the one commercial event that refuses login for the whole tenant. A grant
  // raises entitlement above a plan; with no plan in force there is nothing to raise above.
  [Fact]
  public void An_expired_term_resolves_to_no_modules_even_with_a_live_grant()
  {
    var plan = PlanWith();
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value);
    var grant = TenantEntitlementGrant.GrantModule(
      TenantId, ModuleKey.Create("Attendance").Value, Noon, null, "operator", null, null, Noon).Value;

    Assert.Empty(TenantEntitlement.ModulesAt(inForce, plan, [grant], Noon.AddDays(31)));
  }

  // ==================================================================================================
  // LIMITS — `max(plan, grants)`, WHICH IS WHY LOWERING IS IMPOSSIBLE RATHER THAN MERELY REFUSED.
  // ==================================================================================================

  [Fact]
  public void A_cap_with_no_grant_is_the_plans_cap()
  {
    var plan = PlanWith((PlanLimit.Seats, 100));
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));

    Assert.Equal(100, TenantEntitlement.LimitAt(inForce, plan, [], PlanLimit.Seats, Noon));
  }

  [Fact]
  public void A_raising_grant_wins()
  {
    var plan = PlanWith((PlanLimit.Seats, 100));
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var grant = TenantEntitlementGrant.RaiseLimit(
      TenantId, PlanLimit.Seats, 250, 100, Noon, null, "operator", null, null, Noon).Value;

    Assert.Equal(250, TenantEntitlement.LimitAt(inForce, plan, [grant], PlanLimit.Seats, Noon));
  }

  // ---- THE ONE THAT MATTERS: A LOWERING GRANT IS INERT.
  //
  // The write-time refusal cannot be reached here, because this grant is constructed against a plan cap the
  // factory was told was lower — the shape a corrupted row, a future write path, or a direct SQL insert
  // would produce. Resolution still answers with the plan's cap.
  //
  // **Remove `RaiseLimit`'s refusal entirely and this test still passes.** That is the point: the invariant
  // is a property of the resolution function rather than a rule a future author must remember.
  [Fact]
  public void A_grant_naming_a_lower_value_cannot_lower_the_cap()
  {
    var plan = PlanWith((PlanLimit.Seats, 100));
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));

    // Constructed as though the plan carried no limit, so the write-time guard does not fire — then resolved
    // against a plan that does carry one.
    var lowering = TenantEntitlementGrant.RaiseLimit(
      TenantId, PlanLimit.Seats, 5, planLimitValue: null, Noon, null, "operator", null, null, Noon).Value;

    Assert.Equal(5, lowering.LimitValue);
    Assert.Equal(100, TenantEntitlement.LimitAt(inForce, plan, [lowering], PlanLimit.Seats, Noon));
  }

  [Fact]
  public void The_highest_of_several_grants_wins()
  {
    var plan = PlanWith((PlanLimit.Seats, 100));
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var lower = TenantEntitlementGrant.RaiseLimit(
      TenantId, PlanLimit.Seats, 150, 100, Noon, null, "operator", null, null, Noon).Value;
    var higher = TenantEntitlementGrant.RaiseLimit(
      TenantId, PlanLimit.Seats, 400, 100, Noon, null, "operator", null, null, Noon).Value;

    Assert.Equal(400, TenantEntitlement.LimitAt(inForce, plan, [lower, higher], PlanLimit.Seats, Noon));
  }

  // ---- A CAP IS A PROPERTY OF THE RECORD LIVE AT THAT MOMENT, NOT OF THE TENANT.
  //
  // `OD-SUB-0017` × `OD-SUB-0008`. A metered overage must be judged against the instant the usage occurred,
  // which is why `instant` is a parameter and there is no argument-free overload to reach for.
  [Fact]
  public void A_cap_is_judged_against_the_instant_the_usage_occurred()
  {
    var small = PlanWith((PlanLimit.Seats, 10));
    var large = SubscriptionPlan.Create(
      PlanCode.Create("BIG").Value, PlanName.Create("Large").Value, "operator", Noon).Value;
    large.SetLimit(PlanLimit.Seats, 1000, "operator", Noon);

    var early = SubscriptionFor(small, SubscriptionTerm.Perpetual(Noon));
    var later = TenantSubscription.Append(
      TenantId, large.SubscriptionPlanId, Noon.AddDays(10), Noon,
      SubscriptionTerm.Perpetual(Noon.AddDays(10)), "USD", "operator", null, null, Noon).Value;

    var records = new[] { early, later };

    // Usage five days in is judged against the small plan, even though the tenant is on the large one now.
    var atUsage = TenantEntitlement.InForceAt(records, Noon.AddDays(5));
    Assert.Equal(10, TenantEntitlement.LimitAt(atUsage, small, [], PlanLimit.Seats, Noon.AddDays(5)));

    var now = TenantEntitlement.InForceAt(records, Noon.AddDays(20));
    Assert.Equal(1000, TenantEntitlement.LimitAt(now, large, [], PlanLimit.Seats, Noon.AddDays(20)));
  }

  // A limit the plan does not carry, established by a grant, resolves to the granted value — "undefined" is
  // not "zero", and collapsing the two would silently cap a tenant at nothing.
  [Fact]
  public void A_grant_can_establish_a_limit_the_plan_does_not_carry()
  {
    var plan = PlanWith();
    var inForce = SubscriptionFor(plan, SubscriptionTerm.Perpetual(Noon));
    var grant = TenantEntitlementGrant.RaiseLimit(
      TenantId, PlanLimit.Seats, 25, null, Noon, null, "operator", null, null, Noon).Value;

    Assert.Null(TenantEntitlement.LimitAt(inForce, plan, [], PlanLimit.Seats, Noon));
    Assert.Equal(25, TenantEntitlement.LimitAt(inForce, plan, [grant], PlanLimit.Seats, Noon));
  }
}
