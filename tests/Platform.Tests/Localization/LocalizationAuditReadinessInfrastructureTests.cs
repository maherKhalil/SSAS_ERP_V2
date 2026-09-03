using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Localization;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationAuditReadinessInfrastructureTests
{
  // ⚠ CITES THE *"Production"* QUALIFIER OF `AC-LOC-0064` — the one element
  // `LocalizationAuditReadinessTests` explicitly disclaims, because that fixture stubs the readiness result
  // and the environment logic lives only here. **The two files partition the criterion: this one decides
  // WHEN readiness is unavailable, that one decides WHAT HAPPENS when it is.**
  //
  // ⚠⚠ AND THE ARRANGEMENT IS WHAT MAKES THIS DISCRIMINATING RATHER THAN MERELY TRUE. The bypass flag is
  // set **ON**, and Production refuses anyway. **With the flag off, Production would refuse FOR THE WRONG
  // REASON — the flag, not the environment — and the assertion would pass while proving nothing.** Same
  // move as `Operational_readiness_exception_fails_closed…`, which builds the stub `Ready` and then makes it
  // throw: arrange so that only the mechanism under test can produce the observed refusal.
  //
  // ⚠⚠⚠ THE TWO TESTS FORM A 2×2 OVER (ENVIRONMENT, FLAG) AND THREE CELLS ARE OCCUPIED — which is what
  // makes each one attributable:
  //
  //   Production  · flag ON   → NOT ready     ← here. Isolates the ENVIRONMENT against the row below.
  //   Development · flag ON   → ready         ← `Development_behavior_is_explicit_and_deterministic`
  //   Development · flag OFF  → NOT ready     ← same, and it isolates the FLAG.
  //   Production  · flag OFF  → not exercised, and trivially not-ready by either term.
  //
  // **Neither test alone attributes anything: the first without the second cannot tell a working
  // environment check from a dead flag.** Recorded because the pairing is invisible from either signature.
  [Fact]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Production_is_fail_closed_even_when_development_bypass_is_configured()
  {
    var readiness = Create(Environments.Production, developmentBypassEnabled: true);

    var result = await readiness.CheckAsync();

    Assert.False(result.IsReady);
  }

  // ⚠ THE COMPANION ROWS OF THE 2×2 ABOVE — cited for the same reason and carrying the other half of the
  // attribution. Development with the flag ON is ready; with it OFF it is not. **Together with the
  // Production test these three cells make the environment term and the flag term separately observable.**
  //
  // ⚠⚠⚠ RESIDUAL, AND IT IS A GENUINE HOLE RATHER THAN A SCOPE NOTE: THE BYPASS PREDICATE NAMES **TWO**
  // ENVIRONMENTS AND ONLY ONE IS EXERCISED. `LocalizationManagementAuditReadiness` reads
  // `environment.IsDevelopment() || environment.IsEnvironment("Test")`, and **`"Test"` is never passed.**
  // Searched by CONSTRUCTION SITE rather than by name — `new LocalizationManagementAuditReadiness(` occurs
  // in exactly one place across `tests/`, the `Create` helper below, and it is called with `Production` and
  // `Development` only. **A construction-site search over a class with one constructor is the completable
  // kind; a search for the string `"Test"` would not have been.**
  //
  // So the disjunct's second arm is unexercised: a change dropping it leaves this file green while silently
  // closing the bypass for the Test environment — ⚠ **which is the environment `LocalizationAuditReadiness
  // ApiTests` and the other API fixtures actually run under**, so the arm is not hypothetical.
  //
  // Stated as a hole in the DISJUNCTION rather than as a missing row, because the test name claims
  // *Development behavior* and is accurate about what it covers; the gap is that the product's condition is
  // wider than the name.
  [Theory]
  [InlineData(false, false)]
  [InlineData(true, true)]
  [Trait("Criterion", "AC-LOC-0064")]
  public async Task Development_behavior_is_explicit_and_deterministic(bool configured, bool expectedReady)
  {
    var result = await Create(Environments.Development, configured).CheckAsync();

    Assert.Equal(expectedReady, result.IsReady);
  }

  // ⚠ THE TWO HEALTH-CHECK TESTS BELOW ARE EXAMINED AND DELIBERATELY UNCITED. They assert non-disclosure —
  // empty `Data`, null `Exception`, and `provider-secret-reason` absent from the description — which reads
  // like `AC-LOC-0064`'s *"no internal-cause disclosure"*. **IT IS A DIFFERENT SURFACE.** That clause governs
  // what a REFUSED MUTATION returns to its caller; this governs what a HEALTH ENDPOINT reports to an
  // operator. Same property, different audience, different response.
  //
  // ⚠⚠ Citing them would be ADJACENT-SCOPE: subject and verb both right, the scope silently widened from one
  // response to every surface that reports readiness. `LocalizationAuditReadinessTests.Operational_readiness_
  // exception_fails_closed_without_disclosing_the_reason` is the one that carries the clause, on the
  // mutation path.
  //
  // ⚠⚠⚠ AND THE PAIR IS ITSELF WELL BUILT, WHICH IS WHY THE DISPOSAL IS WORTH WRITING RATHER THAN LEAVING
  // SILENT: the first supplies `Ready` AND `Unavailable` so *unhealthy* is attributable to the result rather
  // than to a check that always fails, and the second distinguishes a returned `Unavailable` from a THROWN
  // provider — two causes of one status, separated. **A reader who finds no criterion id here should not
  // conclude the tests are weak.**
  [Fact]
  public async Task Health_check_reports_ready_as_healthy_and_unavailable_as_unhealthy_without_internal_data()
  {
    var ready = await new LocalizationManagementAuditReadinessHealthCheck(
      new FixedReadiness(LocalizationManagementAuditReadinessResult.Ready))
      .CheckHealthAsync(new HealthCheckContext());
    var unavailable = await new LocalizationManagementAuditReadinessHealthCheck(
      new FixedReadiness(LocalizationManagementAuditReadinessResult.Unavailable))
      .CheckHealthAsync(new HealthCheckContext());

    Assert.Equal(HealthStatus.Healthy, ready.Status);
    Assert.Equal(HealthStatus.Unhealthy, unavailable.Status);
    Assert.Empty(unavailable.Data);
    Assert.Null(unavailable.Exception);
  }

  [Fact]
  public async Task Health_check_hides_provider_exception_details()
  {
    var result = await new LocalizationManagementAuditReadinessHealthCheck(new ThrowingReadiness())
      .CheckHealthAsync(new HealthCheckContext());

    Assert.Equal(HealthStatus.Unhealthy, result.Status);
    Assert.DoesNotContain("provider-secret-reason", result.Description, StringComparison.Ordinal);
    Assert.Null(result.Exception);
  }

  private static LocalizationManagementAuditReadiness Create(string environmentName, bool developmentBypassEnabled) => new(
    Options.Create(new LocalizationManagementAuditReadinessOptions
    {
      DevelopmentBypassEnabled = developmentBypassEnabled
    }),
    new Environment(environmentName));

  private sealed class FixedReadiness(LocalizationManagementAuditReadinessResult result)
    : ILocalizationManagementAuditReadiness
  {
    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(result);
  }

  private sealed class ThrowingReadiness : ILocalizationManagementAuditReadiness
  {
    public Task<LocalizationManagementAuditReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
      Task.FromException<LocalizationManagementAuditReadinessResult>(new InvalidOperationException("provider-secret-reason"));
  }

  private sealed class Environment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
}
