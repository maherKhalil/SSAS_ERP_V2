namespace SSAS.Platform.Application.Abstractions.Localization;

public readonly record struct LocalizationManagementAuditReadinessResult(bool IsReady)
{
  public static LocalizationManagementAuditReadinessResult Ready => new(true);

  public static LocalizationManagementAuditReadinessResult Unavailable => new(false);
}

public interface ILocalizationManagementAuditReadiness
{
  Task<LocalizationManagementAuditReadinessResult> CheckAsync(
    CancellationToken cancellationToken = default);
}
