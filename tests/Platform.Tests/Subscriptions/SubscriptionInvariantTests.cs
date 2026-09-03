using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Subscriptions;

// THE TWO INVARIANTS THAT MAKE THE COMMERCIAL MODEL SAFE (FP-014, T-035).
//
// Monotonic append and additive-only grants. Neither is expressible as a database constraint — the first
// spans rows, the second spans two aggregates and varies with time — so both are domain rules, and a domain
// rule with no test is a comment.
public sealed class SubscriptionInvariantTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid Tenant = Guid.NewGuid();
  private static readonly Guid Plan = Guid.NewGuid();

  private static SubscriptionTerm Perpetual => SubscriptionTerm.Perpetual(Noon);

  private static Result<TenantSubscription> Append(
    DateTimeOffset effectiveFrom, DateTimeOffset? currentMaximum) =>
    TenantSubscription.Append(
      Tenant, Plan, effectiveFrom, currentMaximum, Perpetual, "USD", "operator", null, null, Noon);

  // ==================================================================================================
  // MONOTONIC APPEND.
  // ==================================================================================================

  [Fact]
  public void The_first_record_for_a_tenant_appends_with_no_current_maximum()
  {
    var result = Append(Noon, currentMaximum: null);

    Assert.True(result.IsSuccess);
    Assert.Equal(Noon, result.Value.EffectiveFromUtc);
  }

  [Fact]
  public void An_append_strictly_after_the_current_maximum_is_accepted()
  {
    var result = Append(Noon.AddTicks(1), currentMaximum: Noon);

    Assert.True(result.IsSuccess);
  }

  // ---- THE TWO REFUSALS, AND WHY *EQUAL* IS AS WRONG AS *BEHIND*.
  //
  // Two records at the same instant make "the greatest `EffectiveFromUtc <= T`" ambiguous — the derived
  // invariant "exactly one in force" stops being derivable, and which plan a tenant holds depends on row
  // order. That is why the rule is strictly-greater rather than not-less-than.
  // ⚠ CITES `AC-SUB-0004`'s FIRST CLAUSE — *"Appending a record whose `EffectiveFromUtc` is EQUAL TO OR
  // EARLIER THAN the tenant's current maximum is refused."* The three rows are that clause exactly: `0` is
  // *equal to*, `-1` and `-864000000000` are *earlier than* at two magnitudes.
  //
  // ⚠⚠ THE `0` ROW IS THE ONE THE CRITERION EXISTS FOR AND THE ONE AN IMPLEMENTER WOULD OMIT. A
  // not-less-than rule passes the two negative rows and fails only this one — and the comment above already
  // says why: two records at the same instant make *the greatest `EffectiveFromUtc <= T`* ambiguous, so
  // *exactly one in force* stops being derivable and the answer depends on row order. **Delete the `0` row
  // and the test still reads as a monotonicity test while permitting the tie it exists to forbid.**
  //
  // ⚠⚠⚠ AND CLAUSE TWO IS NOT HERE: *"Two CONCURRENT appends produce one record and one refusal, never two
  // records."* That is a race over a shared maximum and needs real SQL; this passes `currentMaximum` as an
  // argument, so **the value it is compared against is supplied by the caller and never read under
  // contention.** Same shape as `Role.Retire` being TOLD whether a role is assigned — the domain honours
  // the input and says nothing about who computes it. Cited for clause 1 only.
  [Theory]
  [InlineData(0)]      // the same instant
  [InlineData(-1)]     // one tick behind
  [InlineData(-864000000000L)] // a day behind
  [Trait("Criterion", "AC-SUB-0004")]
  public void An_append_at_or_behind_the_current_maximum_is_refused(long offsetTicks)
  {
    var result = Append(Noon.AddTicks(offsetTicks), currentMaximum: Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.NonMonotonicAppend, result.Error);
  }

  // The reason the rule exists, asserted as behaviour rather than left in a comment: an append behind the
  // present would change what was in force at an instant already used to judge a metered overage.
  [Fact]
  public void A_backdated_append_cannot_rewrite_what_was_in_force_at_a_past_instant()
  {
    var first = Append(Noon, currentMaximum: null).Value;
    var backdated = Append(Noon.AddDays(-30), currentMaximum: Noon);

    Assert.True(backdated.IsFailure);

    // Fifteen days ago nothing was in force, and it still is nothing — because the record that would have
    // retroactively covered that instant was never created. Had the append succeeded, this same query would
    // now answer with a plan the tenant did not hold at the time, and any overage judged then would change
    // verdict.
    Assert.Null(TenantEntitlement.InForceAt([first], Noon.AddDays(-15)));
    Assert.Equal(first, TenantEntitlement.InForceAt([first], Noon));
  }

  // ==================================================================================================
  // ADDITIVE GRANTS — THE WRITE-TIME REFUSAL.
  // ==================================================================================================

  // ⚠ CITES `AC-SUB-0016` — *"A grant whose `LimitValue` is AT OR BELOW the plan's current cap for that key
  // is REFUSED AT WRITE, with an error naming the plan's value."* The two rows are *below* and *at*, and
  // `A_limit_grant_above_the_plan_cap_is_accepted` below is the anti-vacuity control: without it, a
  // `RaiseLimit` that refused everything satisfies both rows.
  //
  // ⚠⚠⚠ THE CLAUSE THIS TEST DOES **NOT** CARRY IS NOT A TEST GAP. IT IS A PRODUCT GAP, AND IT IS
  // STRUCTURAL. *"…refused at write, WITH AN ERROR NAMING THE PLAN'S VALUE."*
  //
  // `TenantEntitlementGrant.RaiseLimit` returns `SubscriptionErrors.GrantWouldNotRaise`, which is a
  // `static readonly Error` built once at type load:
  //
  //   "An entitlement grant may only raise a limit above the plan's value; it may never lower one."
  //
  // **It names the plan's value the way a sentence names a variable — it does not CARRY it.** The instance
  // is shared by every refusal and takes no parameters, so **it cannot vary with `planLimitValue` at all**.
  // An operator submitting 100 against a cap of 100 is told the rule and not the number.
  //
  // ⚠ SO THE CLAUSE IS UNSATISFIABLE WITHOUT A `src/` CHANGE — a parameterised error, or detail added at
  // the transport boundary — **and that is a decision for the owner, not a test to write.** Recorded here
  // rather than asserted, because a test demanding `100` in the message would fail on correct-as-built code
  // and would be a proposal wearing a test's clothes.
  //
  // ⚠⚠⚠ AND *"REFUSED AT WRITE"* HAS A SECOND PROBLEM THAT SUBSUMES THE FIRST: **THERE IS NO WRITE.**
  // `TenantEntitlementGrant` has exactly two factories — `GrantModule` and `RaiseLimit` — and searching
  // `src/` for both names plus `new TenantEntitlementGrant` finds **only their own declarations and three
  // comments about them.** No command handler, no endpoint, nothing in Application or API. **The only
  // callers in the repository are four test files**, this one among them.
  //
  // The type IS persisted and IS read — `TenantEntitlementGrantConfiguration`, the migrations, and
  // `TenantEntitlementReader` are all real, so a grant row that existed would be resolved correctly.
  // **Nothing that ships can create one**; see the SQL and migration checks below for how far that goes.
  //
  // ⚠ MECHANISM, NOT NAMES: two factories and a private constructor are the complete set of ways this
  // aggregate comes into being in C#, so the search is exhaustive over source. **The name search is only
  // corroboration, and it OVER-INCLUDES as well as under-includes** — `SubscriptionPlan.GrantModule` is a
  // different method sharing the name, in `src/`, in the same namespace. The mechanism argument is what
  // carries this, not the grep.
  //
  // ⚠⚠ AND SQL WAS CHECKED SEPARATELY, BECAUSE A RAW INSERT NAMES NO C# SYMBOL. Searching `src/` for the
  // TABLE name finds create, constrain, index, map and read — **and no writer of any kind.** The trial seed
  // (`TrialSubscriptionSeed.Sql`) inserts into `SubscriptionPlans`, `SubscriptionPlanModules`,
  // `SubscriptionPlanPrices`, `ModuleDefinitions` and `TenantSubscriptions`: five tables, not this one.
  //
  // ⚠⚠⚠ AND THE MIGRATION THAT CREATES THE TABLE ASSERTS IT IS **EMPTY**.
  // `20260826031515_AddSubscriptionCommercialPlane.cs:289-304` counts plans, subscriptions and grants after
  // creating them and FAILS THE MIGRATION if any is non-zero, quoting `CON-0001` and `OD-SUB-0004` —
  // *"entitlement is recorded, never assumed."* **So the table is guaranteed empty at creation, has no
  // writer, and its only production consumer is a reader.** `AC-SUB-0017`'s *"however it came to exist"* is
  // not merely the only remaining path; the schema refuses at migration time the very row that would
  // exercise it.
  //
  // ---- WHAT THIS IS AND IS NOT, WITH THE BENIGN READING FIRST BECAUSE IT IS PROBABLY THE TRUE ONE.
  //
  // **`FP-014` is a young package under construction, and a domain and schema built ahead of the
  // application layer is ordinary rather than a defect.** Nothing here says anyone did anything wrong.
  // **The finding is about what the DOCUMENTATION CLAIMS, not about the missing handler.**
  //
  // ⚠⚠⚠ AND THE RISK IS NOT CURRENT — IT IS **ARMED**. Today the exposure is nil: no writer, and the table
  // asserted empty at creation, so the resolution guard protects a set that cannot be non-empty. **The
  // exposure arrives the day someone wires up grant creation** — and on that day the guard that catches a
  // lowering grant is documented as *the redundant half of a pair* whose loud half has never run. **A
  // reader tidying away belt-and-braces removes the only enforcement there is, and every test stays green,
  // because no grant row can exist to fail one.**
  //
  // So the shape is not *something is broken*. It is ***a correct-looking redundancy claim that is false in
  // the direction which makes removal look safe, and that becomes load-bearing later.***
  //
  // ⚠ THIS IS THE THIRD INSTANCE OF ONE CLASS AND THE CLASS IS WRITTEN UP ONCE, IN
  // `API.Tests/IdentityAccess/TenantUserRouteInventoryTests.cs` — *a criterion's subject exists, is
  // correct, and is on no executed path*. The other two are `AC-IAM-0001`'s user listing
  // (defended-but-unwitnessed) and three unrouted tenant-user handlers (unrouted-and-untested). **This one
  // is the worst of the three precisely because it is the least broken**: the other two announce their
  // gaps, and this one is described in its own source as safe by redundancy.
  //
  // ⚠⚠ THAT INVERTS THE BELT-AND-BRACES READING. `SubscriptionErrors.cs` calls the write refusal *"the loud
  // half"* of a deliberate pair. **The loud half is unreachable in production, so the resolution-side
  // `max(plan, grants)` is not redundancy — it is the whole enforcement**, and `AC-SUB-0017`'s tests are
  // carrying a load their own criterion describes as secondary.
  //
  // ⚠⚠ AND THE ERROR'S OWN DECLARATION CONFIRMS `AC-SUB-0017`'s READING: *"Resolution ALSO takes
  // `max(plan, grants)`, so a grant that somehow named a lower value could not lower anything — the two are
  // deliberate belt and braces, and this error is the loud half."* **The redundant enforcement is stated in
  // the source as well as split across two criteria**, which is the opposite of the undocumented double
  // guard found earlier tonight in the localization batch validator.
  //
  // ⚠⚠⚠ AND `AC-SUB-0017` IS A SEPARATE CRITERION, DELIBERATELY, WHICH THIS FILE'S OWN HEADING ANTICIPATES
  // — *ADDITIVE GRANTS: THE WRITE-TIME REFUSAL*. `0017` says the resolved cap is `max(plan, grants)` and
  // that a lower grant row **"however it came to exist"** does not lower it. **That phrase is the criterion
  // authors saying the write guard may be bypassed** — by a migration, a seed, a direct write — so the
  // resolution side must hold independently. **It is not cited here and this test cannot carry it**:
  // nothing below resolves a cap. `TenantEntitlementResolutionTests` is where that half must live, and a
  // reader who takes this citation as covering *additive grants* would have the write half and none of the
  // defence behind it.
  [Theory]
  [InlineData(50)]   // below the plan's cap
  [InlineData(100)]  // equal to it — a no-op the caller would believe did something
  [Trait("Criterion", "AC-SUB-0016")]
  public void A_limit_grant_at_or_below_the_plan_cap_is_refused(long grantValue)
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, grantValue, planLimitValue: 100, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.GrantWouldNotRaise, result.Error);
  }

  [Fact]
  public void A_limit_grant_above_the_plan_cap_is_accepted()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 250, planLimitValue: 100, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsSuccess);
    Assert.Equal(EntitlementGrantKind.LimitRaise, result.Value.GrantKind);
    Assert.Equal(250, result.Value.LimitValue);
  }

  // A plan that carries no such limit has nothing to exceed, so establishing one is additive. Distinguished
  // from a plan cap of zero, which is a real cap a grant must exceed.
  [Fact]
  public void A_limit_grant_is_accepted_when_the_plan_carries_no_such_limit()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 1, planLimitValue: null, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public void A_limit_grant_of_zero_against_a_plan_cap_of_zero_is_refused()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 0, planLimitValue: 0, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.GrantWouldNotRaise, result.Error);
  }

  // ==================================================================================================
  // THE TERM, AND THE EXPLICIT PERPETUAL MARKER.
  // ==================================================================================================

  [Fact]
  public void A_fixed_term_ending_before_it_starts_is_refused() =>
    Assert.True(SubscriptionTerm.Fixed(Noon, Noon.AddDays(-1)).IsFailure);

  [Fact]
  public void A_fixed_term_ending_at_its_start_is_refused() =>
    Assert.True(SubscriptionTerm.Fixed(Noon, Noon).IsFailure);

  [Fact]
  public void A_perpetual_term_never_expires()
  {
    var term = SubscriptionTerm.Perpetual(Noon);

    Assert.Equal(SubscriptionTermKind.Perpetual, term.Kind);
    Assert.Null(term.EndUtc);
    Assert.False(term.HasExpiredAt(Noon.AddYears(50)));
  }

  [Fact]
  public void A_fixed_term_expires_after_its_end()
  {
    var term = SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value;

    Assert.False(term.HasExpiredAt(Noon.AddDays(30)));
    Assert.True(term.HasExpiredAt(Noon.AddDays(30).AddTicks(1)));
  }

  // ---- REHYDRATION REFUSES THE COMBINATIONS THE FACTORIES REFUSE.
  //
  // EF materialises through `Rehydrate`, so a row written before the `CHECK` existed — or by any path that
  // bypassed the domain — must not become an object the rest of the model believes is valid.
  [Fact]
  public void Rehydrating_a_perpetual_term_that_carries_an_end_is_refused() =>
    Assert.True(SubscriptionTerm
      .Rehydrate(SubscriptionTermKind.Perpetual, Noon, Noon.AddDays(1)).IsFailure);

  [Fact]
  public void Rehydrating_a_fixed_term_with_no_end_is_refused() =>
    Assert.True(SubscriptionTerm.Rehydrate(SubscriptionTermKind.Fixed, Noon, null).IsFailure);
}
