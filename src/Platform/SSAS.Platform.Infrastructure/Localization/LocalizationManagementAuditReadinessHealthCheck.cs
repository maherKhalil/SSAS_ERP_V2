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
