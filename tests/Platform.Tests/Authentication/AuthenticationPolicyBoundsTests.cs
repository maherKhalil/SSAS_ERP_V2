using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Tests.Authentication;

// ==================================================================================================
// THE ELEVEN CLAUSES OF THE AUTHENTICATION POLICY'S BOUNDS CHECK, EACH CONTRADICTED ONCE (T-131).
// ==================================================================================================
//
// ---- ⚠⚠⚠ THE BEFORE-FIGURE, BECAUSE IT IS THE WHOLE JUSTIFICATION FOR THIS FILE.
//
// `AuthenticationPolicy`'s constructor validates eleven conditions in one `if` and throws. **The entire block
// was deleted and the suites were run: Platform 1145/1145, Architecture 725/725, API 1004/1004 — 2,874 tests,
// all green.** *Every bound on password length, lockout, session lifetime and session count could be removed
// from the product without a single test noticing.*
//
// ⚠⚠ AND THE CAUSE IS NOT NEGLECT, IT IS THE SHAPE. **All five constructions of this type in the tree are
// `new AuthenticationPolicy()` — no arguments, every one — and every default sits INSIDE its own bound:
// `minimumPasswordLength = 12` against a `< 12` check, `maximumActiveSessions = DefaultMaximumActiveSessions`
// against a `> DefaultMaximumActiveSessions` check.** ***A PARAMETER LIST WHOSE DEFAULTS SATISFY EVERY CLAUSE
// IS A FIXTURE THAT CANNOT CONTRADICT ITS OWN CRITERION*** — the same defect as an `RSA.Create(2048)` helper
// testing a `KeySize < 2048` floor, one level up.
//
// ⚠ A MEASURED CONTRAST WORTH KEEPING: options validated through `IValidateOptions<T>` — which RETURN a
// result rather than throwing — are richly covered here, a test per clause, including a 257-character subject
// against a 256 limit. **Before this file the whole tree contained ONE `Assert.Throws<ArgumentOutOfRangeException>`
// and it was about naming.** *A validator that returns invites a contradicting caller; a constructor that
// throws hides behind its own defaults.*
//
// ---- ⚠⚠⚠ HOW EACH TEST DISCRIMINATES, BECAUSE THE ASSERTION CANNOT.
//
// **All eleven clauses throw the SAME exception with the SAME message and the SAME parameter name** —
// `nameof(minimumPasswordLength)` is hard-coded, whichever clause fired. *So asserting the message proves the
// throw came from this block and NOT which clause produced it.* The message is asserted anyway, because it
// separates this refusal from an unrelated `ArgumentOutOfRangeException` raised elsewhere in construction.
//
// ***THE DISCRIMINATION IS CARRIED BY THE ARRANGEMENT, NOT THE ASSERTION.*** Every test below overrides
// exactly the fields its clause needs and leaves every other argument at the constructor's OWN default — by
// named argument, so **no default is copied into this file and none can rot against the product's.** An
// arrangement legal in all other respects can only have thrown for its own clause.
//
// ⚠ THE BOUNDARY IS PINNED, NOT MERELY BRACKETED, wherever it is cheap: `minimumPasswordLength: 11` throws
// and `12` succeeds. *A refusal test alone would be satisfied by a build demanding 50.*
public sealed class AuthenticationPolicyBoundsTests
{
  // ---- ⚠⚠⚠ THE ANTI-VACUITY CONTROL, AND IT IS LOAD-BEARING FOR ALL THIRTEEN TESTS BELOW.
  //
  // Every refusal below is an `Assert.Throws` on a constructor. **If the constructor threw for some reason
  // unrelated to the bounds — a missing dependency, an unconstructable default — all thirteen would pass
  // while proving nothing.** This is the arrangement they are all one field away from.
  [Fact]
  public void The_default_policy_is_inside_every_bound_and_constructs()
  {
    var policy = new AuthenticationPolicy();

    Assert.Equal(AuthenticationPolicy.DefaultMinimumPasswordLength, policy.MinimumPasswordLength);
    Assert.Equal(AuthenticationPolicy.DefaultSessionIdleLifetime, policy.SessionIdleLifetime);
    Assert.Equal(AuthenticationPolicy.DefaultSessionAbsoluteLifetime, policy.SessionAbsoluteLifetime);
  }

  [Fact]
  public void A_minimum_password_length_below_twelve_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(minimumPasswordLength: 11));

    // Pins the threshold rather than bracketing it: 11 is refused and 12 is not.
    Assert.Equal(12, new AuthenticationPolicy(minimumPasswordLength: 12).MinimumPasswordLength);
  }

  [Fact]
  public void A_maximum_password_length_below_sixty_four_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(maximumPasswordLength: 63));

    Assert.Equal(64, new AuthenticationPolicy(maximumPasswordLength: 64).MaximumPasswordLength);
  }

  // ⚠ A RELATION, NOT A BOUND — so it needs two overrides, and both are individually legal. The maximum is
  // 64 (passing its own floor) and the minimum is 100 (passing its own floor); only their ORDER is wrong.
  // **A single-field arrangement cannot reach this clause at all.**
  [Fact]
  public void A_maximum_password_length_below_the_minimum_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(minimumPasswordLength: 100, maximumPasswordLength: 64));
  }

  [Fact]
  public void A_failed_attempt_threshold_below_one_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(failedAttemptThreshold: 0));

    Assert.Equal(1, new AuthenticationPolicy(failedAttemptThreshold: 1).FailedAttemptThreshold);
  }

  [Fact]
  public void A_non_positive_lockout_duration_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(lockoutDuration: TimeSpan.Zero));
    AssertRefused(() => new AuthenticationPolicy(lockoutDuration: TimeSpan.FromSeconds(-1)));
  }

  // Both sides of a two-sided bound. Testing only the upper side would leave a build accepting -1 green.
  [Fact]
  public void A_failed_attempt_concurrency_retry_count_outside_zero_to_three_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(failedAttemptConcurrencyRetries: -1));
    AssertRefused(() => new AuthenticationPolicy(failedAttemptConcurrencyRetries: 4));

    Assert.Equal(0, new AuthenticationPolicy(failedAttemptConcurrencyRetries: 0).FailedAttemptConcurrencyRetries);
    Assert.Equal(3, new AuthenticationPolicy(failedAttemptConcurrencyRetries: 3).FailedAttemptConcurrencyRetries);
  }

  [Fact]
  public void A_non_positive_invitation_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(invitationLifetime: TimeSpan.Zero));
  }

  [Fact]
  public void A_non_positive_password_reset_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(passwordResetLifetime: TimeSpan.Zero));
  }

  // Isolated: a zero idle lifetime is not GREATER than the 90-day absolute default, so the relation clause
  // below cannot be what fired here.
  [Fact]
  public void A_non_positive_session_idle_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(sessionIdleLifetime: TimeSpan.Zero));
  }

  // ---- ⚠⚠⚠ THIS CLAUSE CANNOT BE ISOLATED, AND THAT IS A FACT ABOUT THE PRODUCT RATHER THAN THIS TEST.
  //
  // `SessionAbsoluteLifetime <= 0` cannot be the ONLY violated clause. Setting it to zero leaves the 30-day
  // idle default GREATER than it, so the relation clause fires too; and dropping the idle lifetime to match
  // trips `SessionIdleLifetime <= 0` instead. **There is no arrangement in which this clause fires alone.**
  //
  // *Stated rather than papered over: this test proves a non-positive absolute lifetime is refused, and it
  // CANNOT prove the `SessionAbsoluteLifetime <= TimeSpan.Zero` term is the one refusing it.*
  //
  // ⚠ **AND THAT IS MEASURED, NOT REASONED.** That single term was deleted from `AuthenticationPolicy` and
  // the suite ran **1159 / 1159 GREEN** — while deleting the relation term beside it reddens exactly one test,
  // this file's. ***SO THIS IS THE ONE CLAUSE OF THE ELEVEN THIS FILE DOES NOT CLOSE, AND A READER SHOULD KNOW
  // WHICH RATHER THAN INFER THAT ALL ELEVEN ARE COVERED FROM THE FILE'S NAME.***
  [Fact]
  public void A_non_positive_session_absolute_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(sessionAbsoluteLifetime: TimeSpan.Zero));
  }

  // ---- ⚠⚠ THE RELATION BETWEEN THE TWO SESSION LIFETIMES, WHICH IS THE CLAUSE A DEFAULTS-ONLY FIXTURE IS
  // ---- LEAST ABLE TO STUMBLE INTO.
  //
  // Both values are positive and each is individually legal — 91 days and the 90-day default — so nothing but
  // their ORDER is wrong. **An idle lifetime longer than the absolute one means a session that idles out
  // after its own hard expiry, which is to say never**, and no other clause would catch it.
  [Fact]
  public void A_session_idle_lifetime_longer_than_the_absolute_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(sessionIdleLifetime: TimeSpan.FromDays(91)));

    // Equal is permitted — the clause is strictly greater-than, and pinning that stops a build tightening it
    // to `>=` unnoticed.
    var equal = new AuthenticationPolicy(
      sessionIdleLifetime: TimeSpan.FromDays(90),
      sessionAbsoluteLifetime: TimeSpan.FromDays(90));

    Assert.Equal(equal.SessionAbsoluteLifetime, equal.SessionIdleLifetime);
  }

  [Fact]
  public void A_non_positive_tenant_selection_lifetime_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(tenantSelectionLifetime: TimeSpan.Zero));
  }

  [Fact]
  public void A_maximum_active_session_count_outside_one_to_ten_is_refused()
  {
    AssertRefused(() => new AuthenticationPolicy(maximumActiveSessions: 0));
    AssertRefused(() => new AuthenticationPolicy(maximumActiveSessions: 11));

    Assert.Equal(1, new AuthenticationPolicy(maximumActiveSessions: 1).MaximumActiveSessions);
    Assert.Equal(
      AuthenticationPolicy.DefaultMaximumActiveSessions,
      new AuthenticationPolicy(maximumActiveSessions: AuthenticationPolicy.DefaultMaximumActiveSessions)
        .MaximumActiveSessions);
  }

  // ⚠ THE MESSAGE, NOT ONLY THE TYPE. A bare `Assert.Throws<ArgumentOutOfRangeException>` would also pass for
  // an unrelated range failure raised anywhere in construction. **It cannot say WHICH clause fired — the
  // parameter name is hard-coded to `minimumPasswordLength` for all eleven — so this pins the block, and the
  // arrangement at each call site pins the clause.**
  private static void AssertRefused(Func<AuthenticationPolicy> construct)
  {
    var failure = Assert.Throws<ArgumentOutOfRangeException>(() => construct());

    Assert.Contains("Authentication policy values are outside approved bounds", failure.Message, StringComparison.Ordinal);
  }
}
