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

  // ⚠ CITES `AC-SUB-0002` — *"`EntitlementAt(tenant, T)` for a `T` EARLIER THAN THE SECOND RECORD'S
  // `EffectiveFromUtc` resolves the FIRST record. History is queryable by instant, not only as an audit
  // list."* Day 5 falls before the second record's day 10 and resolves `first`; the day-10 row is the
  // boundary (at, not after) and the day-99 row shows the answer does not decay.
  //
  // ⚠⚠ SCOPE THE CITATION DOES NOT REACH, AND IT IS VISIBLE IN THE SIGNATURE: the criterion names
  // `EntitlementAt(TENANT, T)`. This calls `InForceAt(RECORDS, T)`. **The criterion's function looks the
  // tenant's records up; this one is HANDED them** — so the ordering rule is covered and the lookup that
  // feeds it is not. Same "told, not discovering" shape as `AC-SUB-0004`'s `currentMaximum` next door: the
  // domain honours its input and says nothing about who assembles it.
  // ---- ⚠ ALSO CITES `AC-SUB-0001`, WHICH THIS FIXTURE ALREADY WITNESSES AND WHICH NOTHING CITED.
  //
  // *"Appending a second subscription record makes it the one in force, and the first remains readable and
  // unchanged — byte for byte, including its `EffectiveFromUtc`."* **This test constructs exactly that: a
  // first record and a second appended ten days later.** `Assert.Equal(second, …day 10)` is the first
  // clause; `Assert.Equal(first, …day 5)` shows the first record is still resolvable at its own instant,
  // which is its `EffectiveFromUtc` unchanged.
  //
  // ⚠⚠ THE BYTE-FOR-BYTE HALF IS NOT ASSERTED HERE AND IS NOT UNASSERTED: `PlatformAppendOnlyGuardTests`
  // refuses `Modified` and `Deleted` for `TenantSubscription` and carries `AC-SUB-0003`. **This citation is
  // for the in-force ordering; that one is for the immutability.** Neither test alone carries the whole
  // criterion, and saying which half each holds is the point of writing it down.
  //
  // ⚠ AND THE SCOPE BOUND ABOVE APPLIES UNCHANGED — `InForceAt(RECORDS, T)` is HANDED the records, so the
  // ordering rule is covered and the lookup that assembles them is not. Adding a second criterion to this
  // test does not widen what it reaches.
  [Fact]
  [Trait("Criterion", "AC-SUB-0002")]
  [Trait("Criterion", "AC-SUB-0001")]
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
  //
  // ⚠ CITES `AC-SUB-0012` — *"A tenant with NO subscription record resolves to NO MODULES — not an error,
  // and emphatically NOT ALL MODULES. A missing record is a state, not a failure to configure."* All three
  // of the criterion's alternatives are separated here: `Assert.Empty` is *no modules* rather than *all*,
  // and the absence of a throw is *not an error*. **The `Assert.Empty` is the load-bearing one** — a
  // resolver that treated "no plan" as "unrestricted" would return every module and fail only that line.
  //
  // ⚠⚠ THE THIRD ALTERNATIVE THE CRITERION DOES NOT NAME BUT `TS-SUB-0007` DOES: *not a null a caller might
  // treat as "unrestricted"*. `ModulesAt` returns an empty collection and `LimitAt` returns null — and
  // those are different answers for a reason. An absent CAP is genuinely unknown; an absent MODULE SET is
  // known to be empty. Asserted separately on the two lines below, which is why this test reads as three
  // assertions of one fact and is not.
  [Fact]
  [Trait("Criterion", "AC-SUB-0012")]
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
  //
  // ⚠ CITES `AC-SUB-0017`, BOTH CLAUSES — *"The resolved cap is `max(plan, grants)`. A grant row carrying a
  // LOWER value than the plan — HOWEVER IT CAME TO EXIST — does not lower the resolved cap."* The
  // `planLimitValue: null` construction is *however it came to exist* made executable: it is how a corrupt
  // row, a direct insert, or a future write path would arrive, and it is the only way to reach this state
  // at all, since the factory refuses it when told the truth. `Assert.Equal(5, lowering.LimitValue)` is not
  // decoration — **it proves the grant really carries the lower value**, so the following assertion is
  // about `max` and not about a value that was silently clamped on the way in.
  //
  // ANTI-VACUITY IS ALREADY HERE AND IS THE REASON THIS PAIR WORKS: `A_raising_grant_wins` and
  // `The_highest_of_several_grants_wins` are the positives. A `LimitAt` that ignored grants entirely and
  // always returned the plan's cap would pass THIS test and fail BOTH of those. Neither test alone
  // distinguishes `max(plan, grants)` from a constant.
  //
  // ⚠⚠⚠ AND THE HEADER ABOVE — *"the write-time refusal is tested next door; this suite tests that removing
  // it would still leave the invariant standing"* — UNDERSTATES WHAT THIS SUITE NOW CARRIES. The refusal
  // is not merely removable in principle: **it is already unreachable in production.** `RaiseLimit` and
  // `GrantModule` are `TenantEntitlementGrant`'s only two factories and neither is called anywhere in
  // `src/`; the trial seed writes five tables and not this one; and the migration that creates the table
  // ASSERTS IT IS EMPTY afterwards. **So `max(plan, grants)` is not the second of two guards — it is the
  // only one in effect**, and `TS-SUB-0005`'s note that the two are tested separately *"because either
  // alone would let the other rot"* has already come true in one direction. Detail in
  // `SubscriptionInvariantTests`.
  [Fact]
  [Trait("Criterion", "AC-SUB-0017")]
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
