using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Abstractions.Localization;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationManagementAuditReadinessOptions
{
  public const string SectionName = "Localization:ManagementAuditReadiness";

  public bool DevelopmentBypassEnabled { get; set; }
}

public sealed class LocalizationManagementAuditReadiness(
  IOptions<LocalizationManagementAuditReadinessOptions> options,
  IHostEnvironment environment) : ILocalizationManagementAuditReadiness
{
  public Task<LocalizationManagementAuditReadinessResult> CheckAsync(
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var isExplicitDevelopmentOrTestBypass = options.Value.DevelopmentBypassEnabled &&
      (environment.IsDevelopment() || environment.IsEnvironment("Test"));
    return Task.FromResult(isExplicitDevelopmentOrTestBypass
      ? LocalizationManagementAuditReadinessResult.Ready
      : LocalizationManagementAuditReadinessResult.Unavailable);
  }
}
