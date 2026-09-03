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
  // ⚠⚠ THE CLAUSE THIS TEST DOES **NOT** CARRY: *"with an error NAMING THE PLAN'S VALUE."* It asserts the
  // error IS `GrantWouldNotRaise` and never inspects its message for `100`. **An operator who submits 100
  // against a cap of 100 gets a refusal that does not tell them what the cap is** — and the criterion asked
  // for that specifically, so it is a dropped clause rather than an unstated nicety. Recorded, not fixed:
  // asserting message content is a different decision about error contracts.
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
