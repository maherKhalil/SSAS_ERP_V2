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
  [Theory]
  [InlineData(0)]      // the same instant
  [InlineData(-1)]     // one tick behind
  [InlineData(-864000000000L)] // a day behind
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

  [Theory]
  [InlineData(50)]   // below the plan's cap
  [InlineData(100)]  // equal to it — a no-op the caller would believe did something
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
