using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Infrastructure.TenantStorage;

public interface ITenantDatabaseRestoreVerificationScheduler
{
  Task<TenantDatabaseRestoreVerificationSweepSummary> RunSweepAsync(
    CancellationToken cancellationToken = default);
}

// One D9 sweep. Discovery is advisory, admission serialises each physical database, and only D7 may turn an
// admitted run into recovery evidence. This type deliberately has no dependency on the D6 provider.
public sealed class TenantDatabaseRestoreVerificationScheduler(
  ITenantDatabaseRestoreVerificationFleetReadRepository fleetReads,
  ITenantDatabaseRestoreVerificationRunStore runStore,
  ITenantDatabaseRestoreVerificationExecutor executor,
  ITenantDatabaseRestoreVerificationReconciler reconciler,
  ITenantDatabaseRecoveryReadinessRefresher readinessRefresher,
  IOptions<TenantDatabaseRestoreVerificationOptions> options,
  IDateTimeProvider clock,
  ILogger<TenantDatabaseRestoreVerificationScheduler> logger)
  : ITenantDatabaseRestoreVerificationScheduler
{
  private const string Actor = "tenant-restore-verification-scheduler";

  public async Task<TenantDatabaseRestoreVerificationSweepSummary> RunSweepAsync(
    CancellationToken cancellationToken = default)
  {
    var startedUtc = clock.UtcNow;
    var counters = new Counters();
    if (!options.Value.Enabled)
    {
      return counters.ToSummary(startedUtc, clock.UtcNow);
    }

    try
    {
      // Reconciliation is helpful before admission, but an unobservable server must retain its existing
      // slot rather than block or weaken unrelated scheduler discovery.
      await reconciler.ReconcileAsync(cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return counters.ToSummary(startedUtc, clock.UtcNow);
    }
    catch (Exception exception)
    {
      LogReconciliationFaulted(logger, exception.GetType().Name, exception);
    }

    var globalGate = new SemaphoreSlim(options.Value.MaxConcurrentVerifications);
    var serverGates = new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
    try
    {
      var sourceServers = await fleetReads.ListEligibleSourceServerKeysAsync(cancellationToken);
      var cursors = sourceServers.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal);
      var exhausted = new HashSet<string>(StringComparer.Ordinal);

      while (!cancellationToken.IsCancellationRequested && exhausted.Count < sourceServers.Count)
      {
        var round = new List<DueWork>();
        foreach (var sourceServer in sourceServers)
        {
          if (exhausted.Contains(sourceServer) || cancellationToken.IsCancellationRequested)
          {
            continue;
          }

          var page = await fleetReads.ListCandidatesAsync(
            sourceServer, cursors[sourceServer], options.Value.SchedulerBatchSize, cancellationToken);
          if (page.Count == 0)
          {
            exhausted.Add(sourceServer);
            continue;
          }

          cursors[sourceServer] = page[^1].TenantDatabaseId;
          counters.Eligible += page.Count;
          foreach (var candidate in page)
          {
            await RefreshReadinessAsync(candidate.TenantDatabaseId, cancellationToken);
            if (TryCreateDueWork(candidate, clock.UtcNow, out var due))
            {
              round.Add(due);
            }
          }
        }

        counters.Due += round.Count;
        await DispatchRoundAsync(round, globalGate, serverGates, counters, cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Shutdown stops discovery and waits; already-started D7 execution receives CancellationToken.None.
    }
    finally
    {
      globalGate.Dispose();
      foreach (var gate in serverGates.Values)
      {
        gate.Dispose();
      }
    }

    return counters.ToSummary(startedUtc, clock.UtcNow);
  }

  private async Task RefreshReadinessAsync(long tenantDatabaseId, CancellationToken cancellationToken)
  {
    try
    {
      await readinessRefresher.RefreshAsync(tenantDatabaseId, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      LogReadinessRefreshFaulted(logger, tenantDatabaseId, exception.GetType().Name, exception);
    }
  }

  private bool TryCreateDueWork(
    TenantDatabaseRestoreVerificationDueCandidate candidate,
    DateTimeOffset nowUtc,
    out DueWork due)
  {
    due = default!;
    if (candidate.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      candidate.ProvisioningStatus != TenantDatabaseProvisioningStatus.Ready ||
      !candidate.PolicyEnabled ||
      candidate.ManagementMode != TenantDatabaseBackupManagementMode.AutomaticByPlatform ||
      candidate.SourceBackupRunId is not { } baseline ||
      candidate.RestoreVerificationIntervalDays is not { } interval || interval <= 0)
    {
      return false;
    }

    // Equality is still current; the obligation becomes due strictly after its cadence boundary.
    if (candidate.PreviousSuccessfulVerificationCompletedUtc is { } completedUtc &&
      nowUtc <= completedUtc.AddDays(interval))
    {
      return false;
    }

    var depth = candidate.TransactionLogBackupIntervalMinutes is > 0
      ? TenantDatabaseRestoreDepth.FullWithDifferentialAndLog
      : candidate.DifferentialBackupIntervalMinutes is > 0
        ? TenantDatabaseRestoreDepth.FullWithDifferential
        : TenantDatabaseRestoreDepth.Full;
    due = new DueWork(candidate, baseline, candidate.PreviousSuccessfulVerificationRunId, depth,
      options.Value.RestoreServerKey!.Trim());
    return true;
  }

  private async Task DispatchRoundAsync(
    List<DueWork> round,
    SemaphoreSlim globalGate,
    Dictionary<string, SemaphoreSlim> serverGates,
    Counters counters,
    CancellationToken cancellationToken)
  {
    var tasks = new List<Task>(round.Count);
    // Source-server round-robin gives every source bucket a chance in each keyset round; execution capacity
    // is separately keyed by RestoreServerKey, the server actually carrying the restore load.
    foreach (var work in Interleave(round))
    {
      if (!serverGates.TryGetValue(work.RestoreServerKey, out var serverGate))
      {
        serverGate = new SemaphoreSlim(options.Value.MaxConcurrentVerificationsPerServer);
        serverGates.Add(work.RestoreServerKey, serverGate);
      }

      tasks.Add(DispatchOneAsync(work, globalGate, serverGate, counters, cancellationToken));
    }
    await Task.WhenAll(tasks);
  }

  private static IEnumerable<DueWork> Interleave(IReadOnlyList<DueWork> work)
  {
    var buckets = work.GroupBy(item => item.Candidate.SourceServerKey, StringComparer.Ordinal)
      .Select(group => group.ToList())
      .ToList();
    for (var index = 0; ; index++)
    {
      var emitted = false;
      foreach (var bucket in buckets)
      {
        if (index < bucket.Count)
        {
          emitted = true;
          yield return bucket[index];
        }
      }

      if (!emitted)
      {
        yield break;
      }
    }
  }

  private async Task DispatchOneAsync(
    DueWork work,
    SemaphoreSlim globalGate,
    SemaphoreSlim serverGate,
    Counters counters,
    CancellationToken cancellationToken)
  {
    await globalGate.WaitAsync(cancellationToken);
    try
    {
      await serverGate.WaitAsync(cancellationToken);
      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        var admission = await runStore.TryAdmitAsync(
          new TenantDatabaseRestoreVerificationAdmissionRequest(
            work.Candidate.TenantDatabaseId,
            work.SourceBackupRunId,
            work.PreviousSuccessfulVerificationRunId,
            work.Depth,
            work.RestoreServerKey,
            Actor),
          cancellationToken);
        if (admission.IsFailure)
        {
          Interlocked.Increment(ref counters.Skipped);
          return;
        }

        // A cancellation after admission intentionally leaves an Admitted row for D8; normal shutdown must
        // not start new D7 work and must not delete durable evidence of the pending operation.
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = await executor.ExecuteAsync(
          work.Candidate.TenantDatabaseId, admission.Value, work.Depth, CancellationToken.None);
        if (outcome.IsSuccess && outcome.Value.RestoreVerified)
        {
          Interlocked.Increment(ref counters.Succeeded);
        }
        else
        {
          Interlocked.Increment(ref counters.Failed);
        }
        Interlocked.Increment(ref counters.Dispatched);
      }
      finally
      {
        serverGate.Release();
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception exception)
    {
      Interlocked.Increment(ref counters.Failed);
      LogCandidateFaulted(logger, work.Candidate.TenantDatabaseId, exception.GetType().Name, exception);
    }
    finally
    {
      globalGate.Release();
    }
  }

  private sealed record DueWork(
    TenantDatabaseRestoreVerificationDueCandidate Candidate,
    long SourceBackupRunId,
    long? PreviousSuccessfulVerificationRunId,
    TenantDatabaseRestoreDepth Depth,
    string RestoreServerKey);

  private sealed class Counters
  {
    public int Eligible;
    public int Due;
    public int Dispatched;
    public int Succeeded;
    public int Failed;
    public int Skipped;

    public TenantDatabaseRestoreVerificationSweepSummary ToSummary(
      DateTimeOffset startedUtc, DateTimeOffset completedUtc) =>
      new(startedUtc, completedUtc, Eligible, Due, Dispatched, Succeeded, Failed, Skipped);
  }

  private static readonly Action<ILogger, string, Exception?> LogReconciliationFaulted =
    LoggerMessage.Define<string>(LogLevel.Warning,
      new EventId(4350, nameof(LogReconciliationFaulted)),
      "Restore verification reconciliation faulted with {ExceptionType}; scheduling continues with existing active slots intact.");

  private static readonly Action<ILogger, long, string, Exception?> LogCandidateFaulted =
    LoggerMessage.Define<long, string>(LogLevel.Error,
      new EventId(4351, nameof(LogCandidateFaulted)),
      "Restore verification scheduling faulted for database {TenantDatabaseId} with {ExceptionType}; the sweep continues.");

  private static readonly Action<ILogger, long, string, Exception?> LogReadinessRefreshFaulted =
    LoggerMessage.Define<long, string>(LogLevel.Warning,
      new EventId(4352, nameof(LogReadinessRefreshFaulted)),
      "Recovery-readiness refresh faulted for database {TenantDatabaseId} with {ExceptionType}; scheduling continues.");
}

public sealed record TenantDatabaseRestoreVerificationSweepSummary(
  DateTimeOffset StartedUtc,
  DateTimeOffset CompletedUtc,
  int Eligible,
  int Due,
  int Dispatched,
  int Succeeded,
  int Failed,
  int Skipped);
