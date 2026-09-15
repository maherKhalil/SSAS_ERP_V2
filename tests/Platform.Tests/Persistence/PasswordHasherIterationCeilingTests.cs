using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.Platform.Infrastructure;

namespace SSAS.Platform.Tests.Persistence;

// ==================================================================================================
// THE CONFIGURED ITERATION COUNT IS A CEILING, NOT ONLY A FLOOR (T-162).
// ==================================================================================================
//
// ---- ⚠⚠⚠ THE THREE NUMBERS ARE THE SAME NUMBER, AND THAT IS THE WHOLE PROBLEM.
//
// Measured 2026-09-07 by constructing `new PasswordHasherOptions()` and reading the property:
//
//     framework default   100000
//     house floor         100000   (`.Validate(options => options.IterationCount >= 100_000, …)`)
//     configured value    100000   (`appsettings.json`, `Authentication:PasswordHasher:IterationCount`)
//
// **The existing validator is a FLOOR and it works: lower the configured value and start-up fails.** *That is
// `Password_hasher_iteration_count_below_approved_minimum_fails_options_validation`, and it is not what this
// file is for.*
//
// ⚠⚠⚠ ***THE PIN IS ALSO A CEILING. `appsettings.json` SETS THE VALUE EXPLICITLY, SO IF A FUTURE .NET RAISES
// ITS DEFAULT FOR SECURITY REASONS, OUR OWN CONFIGURATION SILENTLY OVERRIDES IT DOWNWARD.*** **The setting
// reads as *"we chose 100,000"* and is actually *"we froze the framework default of the day"* — and nothing
// distinguishes those two readings from the outside.**
//
// ---- ⚠⚠ WHY A TRIPWIRE AND NOT A GUARD: THE INVALIDATING CHANGE IS A PACKAGE UPGRADE.
//
// Whoever raises `Microsoft.Extensions.Identity.Core` is not thinking about password hashing — **the
// regression is a SIDE EFFECT of their change, in a file they will not open, and it is silent by
// construction because a lower-than-default value is still a VALID configuration.** *That is the shape the
// store reserves the `Tripwire` trait for, and it is deliberately not a `Criterion`: no acceptance criterion
// asks for this, and citing one would misattribute the claim.*
//
// ---- ⚠ WHAT THIS DOES NOT COVER, STATED SO IT IS NOT READ AS MORE.
//
// **An ABSENT section still starts silently.** Delete `Authentication:PasswordHasher` entirely and the
// framework default satisfies the floor, so `ValidateOnStart` never fires — *the protection covers a WRONG
// value and not a MISSING one.* ***Whether that section should be required is a product question and it is
// the owner's, not this test's.***
public sealed class PasswordHasherIterationCeilingTests
{
  [Fact]
  [Trait("Tripwire", "FrameworkDefaultRaised")]
  public void The_configured_iteration_count_is_not_below_the_frameworks_own_default()
  {
    // ---- ⚠⚠⚠ READ AT RUNTIME, NEVER HARD-CODED.
    //
    // A literal `100000` here would be the SAME PIN one layer up: the assertion would agree with itself
    // forever and could not notice the thing it exists to notice. **Constructing the options type is the only
    // way to learn what the framework currently believes.**
    var frameworkDefault = new PasswordHasherOptions().IterationCount;

    var complaint = Weakening(Configured(), frameworkDefault);

    Assert.True(complaint is null, complaint);
  }

  // ---- ⚠⚠⚠ THE CONTROL, AND IT EXISTS BECAUSE THIS TRIPWIRE CANNOT BE PLANTED. MEASURED, NOT ASSUMED.
  //
  // **The obvious plant is to lower the configured value below the framework default. IT DOES NOT WORK, and
  // it produced a FALSE RED before this control was written:**
  //
  //     Microsoft.Extensions.Options.OptionsValidationException :
  //       Password hashing iteration count must be at least 100000.
  //
  // ***THE EXISTING FLOOR FIRED FIRST. Floor, framework default and configured value are all 100,000, so
  // there is NO configured value that is below the default and above the floor*** — the arrangement this
  // tripwire needs cannot be built while the three numbers coincide. **Raising the framework default is the
  // only thing that would fire it, and that requires a package upgrade, which is precisely the event it
  // watches for.**
  //
  // ⚠⚠ So the comparison is extracted into `Weakening` and exercised here with a SYNTHETIC default. **This is
  // not arithmetic-checking-arithmetic: it drives the same method the tripwire drives, so a `>=` written the
  // wrong way round, or a message that omits the numbers, fails HERE — and those are the two ways the
  // tripwire could be silently wrong on the day it matters.**
  //
  // ---- ⚠⚠⚠ TWO PLANTS, BECAUSE ONE PLANT REACHED ONLY THE FIRST ASSERTION (T-168).
  //
  // ***A PLANT CANNOT REACH AN ASSERTION THAT AN EARLIER ONE IN THE SAME METHOD HAS ALREADY FAILED ON.*** The
  // first plant below proves the PREDICATE and stops at line one; the message checks — the whole reason this
  // control exists — were unreached until the second was run. **Each plant names the assertion it reached, so
  // a later reader does not have to infer it:**
  //
  //   PLANT A — predicate inverted, `>=` to `<=` in `Weakening`.
  //     RED at `Assert.NotNull(complaint)`: `Weakening` returned null for a genuine weakening.
  //     ⚠ The three `Contains` and the two converse `Null` rows below it were NOT executed.
  //
  //   PLANT B — `{frameworkDefault}` removed from the interpolated message; predicate left correct.
  //     RED at `Assert.Contains("210000", …)`: `Assert.Contains() Failure: Sub-string not found`.
  //     **This is the plant that proves the message carries the numbers**, which PLANT A could not reach.
  //
  // *Both reverted; the retirement instruction is covered by the same `Contains` family as the numbers.*
  [Fact]
  public void A_framework_default_above_the_configured_value_is_reported_as_a_weakening()
  {
    var complaint = Weakening(configured: 100_000, frameworkDefault: 210_000);

    Assert.NotNull(complaint);
    Assert.Contains("100000", complaint, StringComparison.Ordinal);
    Assert.Contains("210000", complaint, StringComparison.Ordinal);
    Assert.Contains("DO NOT LOWER THIS ASSERTION", complaint, StringComparison.Ordinal);

    // And the converse, so the predicate's DIRECTION is pinned rather than its truthiness: an equal or higher
    // configured value is not a weakening.
    Assert.Null(Weakening(configured: 210_000, frameworkDefault: 210_000));
    Assert.Null(Weakening(configured: 300_000, frameworkDefault: 210_000));
  }

  // Returns the complaint, or null when the configuration still meets the framework's own default. ⚠ The
  // message carries the RETIREMENT INSTRUCTION because whoever reads it will be mid-package-upgrade and
  // looking for the fastest green.
  private static string? Weakening(int configured, int frameworkDefault) =>
    configured >= frameworkDefault
      ? null
      : $"the configured password-hashing iteration count is {configured} and the framework's own default " +
        $"is now {frameworkDefault}. A package upgrade has raised the default above the value this " +
        "repository pins, so our configuration is silently WEAKENING the framework's choice. Raise " +
        "`Authentication:PasswordHasher:IterationCount` in appsettings.json to at least the new default, " +
        "and raise the floor in `AddPlatformInfrastructure` to match. DO NOT LOWER THIS ASSERTION.";

  // The value the product actually resolves — read through the real registration, not from the JSON file, so
  // that a change to how the section is bound is visible here rather than silently bypassing the assertion.
  private static int Configured()
  {
    var services = new ServiceCollection();
    services.AddPlatformInfrastructure(RealAppSettings());

    using var provider = services.BuildServiceProvider();

    return provider.GetRequiredService<IOptions<PasswordHasherOptions>>().Value.IterationCount;
  }

  // The host's own `appsettings.json`, located from the repository root rather than copied: a duplicated
  // value here would be a third pin and would agree with the other two by construction.
  private static IConfiguration RealAppSettings() =>
    new ConfigurationBuilder()
      .AddJsonFile(Path.Combine(RepositoryRoot(), "src", "Host", "SSAS.Host.API", "appsettings.json"))
      .Build();

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
