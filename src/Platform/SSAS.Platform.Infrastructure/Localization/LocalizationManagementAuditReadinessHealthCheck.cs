using Microsoft.Extensions.Diagnostics.HealthChecks;
using SSAS.Platform.Application.Abstractions.Localization;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationManagementAuditReadinessHealthCheck(
  ILocalizationManagementAuditReadiness readiness) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken cancellationToken = default)
  {
    try
    {
      var result = await readiness.CheckAsync(cancellationToken);
      return result.IsReady
        ? HealthCheckResult.Healthy()
        : HealthCheckResult.Unhealthy("Localization management audit readiness is unavailable.");
    }
    // Same pair as `LocalizationManagementAuditGuard`, for the same reason: cancellation is not an
    // unhealthy audit trail, and everything else is.
    //
    // ⚠ **A HEALTH CHECK THAT THROWS IS WORSE THAN ONE REPORTING UNHEALTHY.** The probe's job is to answer,
    // and an exception escaping here turns a degraded subsystem into a broken health endpoint -- which is
    // the signal operators use to decide whether anything is wrong at all.
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch
    {
      return HealthCheckResult.Unhealthy("Localization management audit readiness is unavailable.");
    }
  }
}
