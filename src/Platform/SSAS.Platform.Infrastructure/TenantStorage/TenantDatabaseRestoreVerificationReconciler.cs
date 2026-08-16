using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

public interface ITenantDatabaseRestoreVerificationReconciler
{
  Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(
    CancellationToken cancellationToken = default);
}

// D8's orchestration around the already-pure reconciliation decision. It observes one exact durable run at
// a time, holds no Platform transaction across a server call, and can only release a slot through a CAS.
// It never deletes a verification database or synthesizes restore-success evidence.
public sealed class TenantDatabaseRestoreVerificationReconciler(
  ITenantDatabaseRestoreVerificationReconciliationStore reconciliationStore,
  ITenantDatabaseRestoreVerificationServerObserver serverObserver,
  IOptions<TenantDatabaseRestoreVerificationOptions> options,
  IDateTimeProvider clock,
  ILogger<TenantDatabaseRestoreVerificationReconciler> logger)
  : ITenantDatabaseRestoreVerificationReconciler
{
  private const string Actor = "tenant-restore-verification-reconciler";

  public async Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(
    CancellationToken cancellationToken = default)
  {
    var summary = new Counters();
    long afterId = 0;

    while (!cancellationToken.IsCancellationRequested)
    {
      var page = await reconciliationStore.ListActiveAsync(
        afterId, options.Value.SchedulerBatchSize, cancellationToken);
      if (page.Count == 0)
      {
        break;
      }

      afterId = page[^1].VerificationRunId;
      foreach (var run in page)
      {
        cancellationToken.ThrowIfCancellationRequested();
        summary.Inspected++;
        await ReconcileOneAsync(run, summary, cancellationToken);
      }
    }

    return summary.ToSummary(clock.UtcNow);
  }

  private async Task ReconcileOneAsync(
    TenantDatabaseRestoreVerificationActiveRunRecord run,
    Counters summary,
    CancellationToken cancellationToken)
  {
    var expectedName = TenantDatabaseVerificationNaming.ForRun(run.TenantDatabaseId, run.VerificationRunId);
    if (run.Status == TenantDatabaseRestoreVerificationStatus.Restoring &&
      !string.Equals(run.VerificationDatabaseName, expectedName, StringComparison.Ordinal))
    {
      summary.Conflicts++;
      LogConflict(logger, run.VerificationRunId, run.TenantDatabaseId, "DurableDatabaseNameMismatch", null);
      return;
    }

    var observation = await serverObserver.ObserveAsync(
      new TenantDatabaseRestoreVerificationServerObservationRequest(
        run.VerificationRunId,
        run.TenantDatabaseId,
        run.RestoreServerKey,
        run.SourceServerKey,
        expectedName),
      cancellationToken);

    var decision = TenantDatabaseVerificationReconciliation.Decide(
      new TenantDatabaseVerificationReconciliationInputs(
        run.Status,
        observation.ServerStateObserved,
        observation.RestoreIsActiveOnServer,
        observation.VerificationDatabaseExists,
        clock.UtcNow - run.StartedUtc,
        options.Value.ReconciliationGracePeriod));

    switch (decision)
    {
      case TenantDatabaseVerificationReconciliationDecision.LeaveAlone:
        summary.LeftActive++;
        if (!observation.ServerStateObserved)
        {
          summary.Unobservable++;
          LogUnobservable(logger, run.VerificationRunId, run.TenantDatabaseId,
            observation.UnavailableReason ?? "Unknown", null);
        }
        return;

      case TenantDatabaseVerificationReconciliationDecision.ReportInconsistent:
        summary.Conflicts++;
        LogConflict(logger, run.VerificationRunId, run.TenantDatabaseId, "AdmittedDatabasePresent", null);
        return;

      case TenantDatabaseVerificationReconciliationDecision.ReleaseAbandoned:
      case TenantDatabaseVerificationReconciliationDecision.ReleaseAbandonedWithOrphan:
        var orphan = decision == TenantDatabaseVerificationReconciliationDecision.ReleaseAbandonedWithOrphan;
        var transition = await reconciliationStore.ReconcileAbandonedAsync(
          new TenantDatabaseRestoreVerificationReconciliationTransitionRequest(
            run,
            orphan ? "ReconciledAbandonedWithOrphan" : "ReconciledAbandoned",
            Actor),
          cancellationToken);
        if (transition.IsSuccess)
        {
          summary.Reconciled++;
          if (orphan)
          {
            summary.OrphansObserved++;
          }
        }
        else
        {
          summary.RacesLost++;
        }
        return;
    }
  }

  private sealed class Counters
  {
    public int Inspected { get; set; }
    public int Reconciled { get; set; }
    public int OrphansObserved { get; set; }
    public int LeftActive { get; set; }
    public int Unobservable { get; set; }
    public int Conflicts { get; set; }
    public int RacesLost { get; set; }

    public TenantDatabaseRestoreVerificationReconciliationSummary ToSummary(DateTimeOffset completedUtc) =>
      new(completedUtc, Inspected, Reconciled, OrphansObserved, LeftActive, Unobservable, Conflicts, RacesLost);
  }

  private static readonly Action<ILogger, long, long, string, Exception?> LogUnobservable =
    LoggerMessage.Define<long, long, string>(LogLevel.Warning,
      new EventId(4340, nameof(LogUnobservable)),
      "Restore verification reconciliation left run {VerificationRunId} for database {TenantDatabaseId} active because verification-server observation was unavailable: {Reason}.");

  private static readonly Action<ILogger, long, long, string, Exception?> LogConflict =
    LoggerMessage.Define<long, long, string>(LogLevel.Warning,
      new EventId(4341, nameof(LogConflict)),
      "Restore verification reconciliation left run {VerificationRunId} for database {TenantDatabaseId} active because durable and server state conflict: {Reason}.");
}

public sealed record TenantDatabaseRestoreVerificationReconciliationSummary(
  DateTimeOffset CompletedUtc,
  int Inspected,
  int Reconciled,
  int OrphansObserved,
  int LeftActive,
  int Unobservable,
  int Conflicts,
  int RacesLost);
