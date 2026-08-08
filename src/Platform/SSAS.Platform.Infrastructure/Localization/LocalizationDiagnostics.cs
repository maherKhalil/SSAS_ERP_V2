using Microsoft.Extensions.Logging;
using SSAS.Platform.Application.Abstractions.Localization;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationDiagnostics(ILogger<LocalizationDiagnostics> logger) : ILocalizationDiagnostics
{
  public void RecordMissingResource(string resourceKey) =>
    LogMissingResource(logger, resourceKey, null);

  public void RecordDegradedTenant(Guid tenantId) =>
    LogDegradedTenant(logger, tenantId, null);

  private static readonly Action<ILogger, string, Exception?> LogMissingResource = LoggerMessage.Define<string>(
    LogLevel.Warning,
    new EventId(4201, nameof(RecordMissingResource)),
    "Localization resource {ResourceKey} was not found; a neutral fallback was used.");

  private static readonly Action<ILogger, Guid, Exception?> LogDegradedTenant = LoggerMessage.Define<Guid>(
    LogLevel.Warning,
    new EventId(4202, nameof(RecordDegradedTenant)),
    "Tenant localization SQL validation is degraded for Tenant {TenantId}; system defaults may be used.");
}
