using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Localization;

namespace SSAS.Platform.Application.Localization;

public static class LocalizationManagementErrors
{
  public static readonly Error AuditReadinessUnavailable = new(
    "localization.audit_readiness_unavailable",
    "Localization management is temporarily unavailable.");
}

internal static class LocalizationManagementAuditGuard
{
  public static async Task<Result> CheckAsync(
    ILocalizationManagementAuditReadiness readiness,
    CancellationToken cancellationToken)
  {
    try
    {
      var result = await readiness.CheckAsync(cancellationToken);
      return result.IsReady
        ? Result.Success()
        : Result.Failure(LocalizationManagementErrors.AuditReadinessUnavailable);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch
    {
      return Result.Failure(LocalizationManagementErrors.AuditReadinessUnavailable);
    }
  }
}
