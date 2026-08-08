using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Localization;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationAuditReadinessInfrastructureTests
{
  [Fact]
  public async Task Production_is_fail_closed_even_when_development_bypass_is_configured()
  {
    var readiness = Create(Environments.Production, developmentBypassEnabled: true);

    var result = await readiness.CheckAsync();

    Assert.False(result.IsReady);
  }

  [Theory]
  [InlineData(false, false)]
  [InlineData(true, true)]
  public async Task Development_behavior_is_explicit_and_deterministic(bool configured, bool expectedReady)
  {
    var result = await Create(Environments.Development, configured).CheckAsync();

    Assert.Equal(expectedReady, result.IsReady);
  }

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
