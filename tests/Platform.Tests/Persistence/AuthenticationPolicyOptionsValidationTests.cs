using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Persistence;

// ==================================================================================================
// THE POLICY OPTIONS VALIDATOR REFUSES EACH OF ITS NINE CLAUSES (T-165).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY THIS EXISTS: `AuthenticationPolicyBoundsTests` TESTED THE LAYER PRODUCTION NEVER REACHES.
//
// T-131 planted `AuthenticationPolicy`'s ELEVEN constructor bounds and proved every one of them dead —
// deleting the whole block left 2,874 tests green. **That work is sound and it is DEFENCE IN DEPTH, not the
// production path.**
//
// ***`AddPlatformInfrastructure` VALIDATES `AuthenticationPolicyOptions` WITH NINE `.Validate(…)` CLAUSES AND
// `ValidateOnStart()`, AND ONLY THEN BUILDS `new AuthenticationPolicy(...)` FROM THE VALIDATED VALUES.*** So a
// misconfigured deployment is refused by the OPTIONS validator and the constructor never sees the bad value.
// **The constructor is the inner layer; the options validator is the one a real misconfiguration meets.**
//
// ⚠ Before this file, ***only TWO of the nine clauses had ever been contradicted*** —
// `Invalid_password_length_configuration_fails_options_validation` covers the minimum, the 64-character floor
// and the maximum-below-minimum relation in three rows. **The other seven refuse in production and were
// contradicted by nothing.**
//
// ---- ⚠⚠ THREE OF THE SEVEN ARE UNCONTRADICTABLE FROM THE SHIPPED CONFIGURATION.
//
// `appsettings.json` sets values that sit exactly ON their bound:
//
//     FailedAttemptConcurrencyRetries   3   against `is >= 0 and <= 3`
//     MaximumActiveSessions            10   against `is >= 1 and <= DefaultMaximumActiveSessions` (10)
//
// **A deployment cannot drift past those by editing one number upward without also being refused** — which is
// the validator working, and which also means *nothing in the repository had ever seen them refuse.*
//
// ---- ⚠⚠⚠ EACH ROW PROVES IT EXERCISED THE **OPTIONS** LAYER, AND THE PROOF IS STRUCTURAL.
//
// The arrangement resolves `IOptions<AuthenticationPolicyOptions>.Value` and nothing else. ***`AuthenticationPolicy`
// IS NEVER CONSTRUCTED, SO THE CONSTRUCTOR CANNOT BE WHAT THREW.*** *That is what makes these tests about the
// outer layer rather than a second copy of T-131 — the layers enforce identical thresholds, and only the
// entry point distinguishes them.*
//
// ⚠ AND THE MESSAGE IS ASSERTED, NOT THE TYPE. **All nine clauses raise `OptionsValidationException`**, so the
// type is satisfied by any of them — including the two already covered. *Rule 2 of `AssertionMessageChoice`:
// ask what else produces this outcome.*
public sealed class AuthenticationPolicyOptionsValidationTests
{
  // ---- ONE CLAUSE PER ROW, EVERY OTHER FIELD LEFT AT ITS LEGAL DEFAULT.
  //
  // `AuthenticationPolicyOptions` declares defaults that satisfy all nine clauses, so overriding a single key
  // violates exactly one and the message identifies which. ⚠ The session row overrides TWO keys because the
  // clause is a RELATION — idle exceeding absolute — and both values are individually legal: 40 days and 30
  // days each pass every other clause, so only their ORDER is wrong.
  [Theory]
  [InlineData("FailedAttemptThreshold", "0", "Failed-attempt threshold must be positive.")]
  [InlineData("LockoutDuration", "00:00:00", "Lockout duration must be positive.")]
  [InlineData("FailedAttemptConcurrencyRetries", "4", "Failed-attempt concurrency retries must be between zero and three.")]
  [InlineData("FailedAttemptConcurrencyRetries", "-1", "Failed-attempt concurrency retries must be between zero and three.")]
  [InlineData("InvitationLifetime", "00:00:00", "Action-token lifetimes must be positive.")]
  [InlineData("PasswordResetLifetime", "00:00:00", "Action-token lifetimes must be positive.")]
  [InlineData("SessionIdleLifetime", "00:00:00", "Session lifetimes must be positive and idle lifetime cannot exceed absolute lifetime.")]
  [InlineData("TenantSelectionLifetime", "00:00:00", "Tenant-selection lifetime must be positive.")]
  [InlineData("MaximumActiveSessions", "0", "Maximum active sessions must be between one and the approved maximum.")]
  [InlineData("MaximumActiveSessions", "11", "Maximum active sessions must be between one and the approved maximum.")]
  public void A_configuration_violating_one_policy_clause_is_refused_by_that_clause(
    string key, string value, string expectedMessage)
  {
    var failure = Refusal(new Dictionary<string, string?>
    {
      [$"{AuthenticationPolicyOptions.SectionName}:{key}"] = value
    });

    Assert.Contains(expectedMessage, failure.Message, StringComparison.Ordinal);
  }

  // ---- ⚠⚠ THE RELATION, WHICH IS THE CLAUSE A DEFAULTS-ONLY CONFIGURATION IS LEAST ABLE TO REACH BY ACCIDENT.
  //
  // Both values are positive and each is individually legal — an idle lifetime of 40 days and an absolute of
  // 30 days pass every other clause in the block. **Only their order is wrong, and a session that idles out
  // after its own hard expiry never idles out at all.**
  [Fact]
  public void An_idle_session_lifetime_longer_than_the_absolute_lifetime_is_refused()
  {
    var failure = Refusal(new Dictionary<string, string?>
    {
      [$"{AuthenticationPolicyOptions.SectionName}:SessionIdleLifetime"] = "40.00:00:00",
      [$"{AuthenticationPolicyOptions.SectionName}:SessionAbsoluteLifetime"] = "30.00:00:00"
    });

    Assert.Contains(
      "Session lifetimes must be positive and idle lifetime cannot exceed absolute lifetime.",
      failure.Message,
      StringComparison.Ordinal);
  }

  // ---- ⚠⚠⚠ THE ANTI-VACUITY CONTROL, AND IT IS LOAD-BEARING FOR EVERY ROW ABOVE.
  //
  // Every row is an `Assert.Throws` on a service resolution. **If the graph failed to build for some unrelated
  // reason — a missing connection string, a registration that moved — all of them would pass while proving
  // nothing.** This is the arrangement they are each one key away from, and it must NOT throw.
  [Fact]
  public void The_shipped_defaults_resolve_without_refusal()
  {
    using var provider = Provider([]);

    var options = provider.GetRequiredService<IOptions<AuthenticationPolicyOptions>>().Value;

    Assert.Equal(12, options.MinimumPasswordLength);
    Assert.Equal(3, options.FailedAttemptConcurrencyRetries);
    Assert.Equal(10, options.MaximumActiveSessions);
  }

  private static OptionsValidationException Refusal(Dictionary<string, string?> overrides)
  {
    using var provider = Provider(overrides);

    // ⚠ RESOLVING THE OPTIONS AND NOTHING ELSE. `AuthenticationPolicy` is registered as a singleton built FROM
    // these options, and it is never asked for here — so the constructor's identical bounds cannot be the
    // source of this exception. The layer under test is the one that refuses first in production.
    return Assert.Throws<OptionsValidationException>(() =>
      provider.GetRequiredService<IOptions<AuthenticationPolicyOptions>>().Value);
  }

  private static ServiceProvider Provider(Dictionary<string, string?> overrides)
  {
    var settings = new Dictionary<string, string?>(overrides, StringComparer.Ordinal)
    {
      ["ConnectionStrings:Platform"] =
        "Server=localhost;Database=not-opened;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
    };

    var services = new ServiceCollection();
    services.AddPlatformInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    return services.BuildServiceProvider();
  }
}
