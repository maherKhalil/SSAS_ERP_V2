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
    // ⚠ THE PAIR IS THE REASON, AND NEITHER HALF MAKES SENSE ALONE.
    //
    // Cancellation is the one failure that must NOT become `AuditReadinessUnavailable`: the caller asked
    // to stop, and reporting that as a readiness fault would make every cancelled request look like a
    // degraded audit trail. It is rethrown so it stays cancellation.
    //
    // Everything else is discarded ON PURPOSE. **This guard's answer is binary** -- readiness is either
    // established or it is not -- and a caller cannot act differently on a timeout than on a broken
    // connection. Narrowing the catch would let an unanticipated failure reach the caller as an unhandled
    // exception, which is the one outcome worse than reporting unavailable.
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
