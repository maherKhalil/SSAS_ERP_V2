using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// ONE fleet sweep (ADR-022 §13, TS-Backup Phase C).
//
// Deliberately not the loop. This service runs a single pass over the estate and returns; the hosted service
// owns repetition, jitter and shutdown. Keeping them apart is what makes a sweep testable without a timer.
//
// The division of authority is the same one Phase B established, extended outward:
//
//   scheduler  — decides WHICH database and WHICH operation, from a projection
//   executor   — decides WHETHER it may happen, and owns the run lifecycle
//   provider   — decides HOW SQL Server performs it, and produces the evidence
//
// The scheduler therefore knows no connection string, no destination and no credential. It names an id and
// an operation. That is the whole contract, and an architecture guard enforces it.
public interface ITenantDatabaseBackupScheduler
{
  Task<TenantDatabaseBackupSweepSummary> RunSweepAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseBackupScheduler(
  ITenantDatabaseBackupFleetReadRepository fleetReads,
  ITenantDatabaseBackupExecutor executor,
  IDateTimeProvider clock,
  IOptions<TenantDatabaseBackupSchedulerOptions> schedulerOptions,
  ILogger<TenantDatabaseBackupScheduler> logger) : ITenantDatabaseBackupScheduler
{
  public async Task<TenantDatabaseBackupSweepSummary> RunSweepAsync(
    CancellationToken cancellationToken = default)
  {
    var startedUtc = clock.UtcNow;
    var summary = new SweepCounters();

    // Per-server gates live for the duration of ONE sweep. A long-lived dictionary keyed by ServerKey would
    // accumulate an entry per server ever seen and never release one.
    var serverGates = new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
    using var globalGate = new SemaphoreSlim(schedulerOptions.Value.MaxConcurrentBackups);

    try
    {
      // SERVER-AWARE, ROUND-ROBIN DISCOVERY.
      //
      // Paging the fleet by identifier alone confined fairness to whatever shared a page: a page of one
      // server's databases had to be fully worked — at one backup at a time, per the per-server cap — before
      // any other server was even discovered. Enumerating servers first and advancing a cursor per server
      // means every server contributes to every round, and no server's backlog can hide another's overdue
      // database behind it.
      //
      // EACH SERVER KEEPS ITS OWN KEYSET CURSOR, reset at the start of every sweep. A database that becomes
      // due behind a cursor waits for the next pass, which is bounded and acceptable; a persisted cursor
      // would carry state that drifts out of step with a fleet changing underneath it.
      var serverKeys = await fleetReads.ListEligibleServerKeysAsync(cancellationToken);
      var cursors = serverKeys.ToDictionary(serverKey => serverKey, _ => 0L, StringComparer.Ordinal);
      var exhausted = new HashSet<string>(StringComparer.Ordinal);

      while (!cancellationToken.IsCancellationRequested && exhausted.Count < serverKeys.Count)
      {
        var round = new List<DueWork>();

        foreach (var serverKey in serverKeys)
        {
          if (cancellationToken.IsCancellationRequested || exhausted.Contains(serverKey))
          {
            continue;
          }

          var page = await fleetReads.ListBackupCandidatesAsync(
            serverKey, cursors[serverKey], schedulerOptions.Value.BatchSize, cancellationToken);

          if (page.Count == 0)
          {
            exhausted.Add(serverKey);
            continue;
          }

          cursors[serverKey] = page[^1].TenantDatabaseId;
          summary.Eligible += page.Count;
          round.AddRange(SelectDue(page, clock.UtcNow));
        }

        if (round.Count == 0)
        {
          continue;
        }

        var runnable = await SuppressRecentAttemptsAsync(round, cancellationToken);
        summary.Due += runnable.Count;

        if (runnable.Count == 0)
        {
          continue;
        }

        await DispatchAsync(runnable, serverGates, globalGate, summary, cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Shutdown, not failure. Whatever was dispatched has already been awaited by DispatchAsync.
      LogSweepCancelled(logger, summary.Dispatched, null);
    }
    finally
    {
      foreach (var gate in serverGates.Values)
      {
        gate.Dispose();
      }
    }

    var completedUtc = clock.UtcNow;
    var result = summary.ToSummary(startedUtc, completedUtc);

    LogSweepCompleted(
      logger, result.Due, result.Dispatched, result.Succeeded, result.Failed,
      result.Skipped, (completedUtc - startedUtc).TotalMilliseconds, null);

    return result;
  }

  // One operation per database, by ADR-022 precedence. Databases with nothing due drop out here and never
  // reach the executor, so no run row is created for them.
  private static List<DueWork> SelectDue(
    IReadOnlyList<TenantDatabaseBackupDueCandidate> page,
    DateTimeOffset nowUtc)
  {
    var due = new List<DueWork>();
    foreach (var candidate in page)
    {
      var operation = TenantDatabaseBackupDueEvaluator.SelectDueOperation(candidate, nowUtc);
      if (operation is not null)
      {
        // The anchor this decision rests on: the last successful timestamp for the chosen operation. It
        // travels with the work so the executor can revalidate the decision under database ownership, where
        // a second instance's stale copy of the same decision can finally be detected.
        due.Add(new DueWork(candidate, operation, AnchorFor(candidate, operation)));
      }
    }

    return due;
  }

  private static DateTimeOffset? AnchorFor(
    TenantDatabaseBackupDueCandidate candidate,
    TenantDatabaseBackupOperation operation) =>
    operation.OperationCode switch
    {
      "Full" => candidate.LastSuccessfulFullBackupUtc,
      "Differential" => candidate.LastSuccessfulDifferentialBackupUtc,
      "TransactionLog" => candidate.LastSuccessfulLogBackupUtc,
      _ => null
    };

  // Backoff is applied only to work already established as due, so the run-history read is proportional to
  // the work needing doing rather than to the size of the estate.
  private async Task<List<DueWork>> SuppressRecentAttemptsAsync(
    List<DueWork> due,
    CancellationToken cancellationToken)
  {
    var latestRuns = await fleetReads.ListLatestRunsAsync(
      due.Select(work => work.Candidate.TenantDatabaseId).ToArray(), cancellationToken);

    var nowUtc = clock.UtcNow;
    var runnable = new List<DueWork>(due.Count);

    foreach (var work in due)
    {
      latestRuns.TryGetValue(work.Candidate.TenantDatabaseId, out var latest);

      if (TenantDatabaseBackupRetryPolicy.ShouldSuppress(
        latest, nowUtc, schedulerOptions.Value.FailureRetryBackoff, schedulerOptions.Value.SkipRetryBackoff))
      {
        continue;
      }

      runnable.Add(work);
    }

    return runnable;
  }

  // Bounded on two axes at once: a global cap per instance, and a per-ServerKey cap that stops one SQL
  // Server absorbing every slot. Work is interleaved across servers first so a server with many due
  // databases cannot starve one with a single overdue database.
  private async Task DispatchAsync(
    List<DueWork> runnable,
    Dictionary<string, SemaphoreSlim> serverGates,
    SemaphoreSlim globalGate,
    SweepCounters summary,
    CancellationToken cancellationToken)
  {
    var tasks = new List<Task>(runnable.Count);

    foreach (var work in Interleave(runnable))
    {
      var gate = ServerGate(serverGates, work.Candidate.ServerKey, schedulerOptions.Value.MaxConcurrentPerServer);
      tasks.Add(ExecuteOneAsync(work, gate, globalGate, summary, cancellationToken));
    }

    await Task.WhenAll(tasks);
  }

  private async Task ExecuteOneAsync(
    DueWork work,
    SemaphoreSlim serverGate,
    SemaphoreSlim globalGate,
    SweepCounters summary,
    CancellationToken cancellationToken)
  {
    // Global first, then per-server, and always in that order: two tasks acquiring the same pair in opposite
    // orders is how a deadlock is written.
    await globalGate.WaitAsync(cancellationToken);
    try
    {
      await serverGate.WaitAsync(cancellationToken);
      try
      {
        var startedUtc = clock.UtcNow;

        // THE CANCELLATION BOUNDARY (ADR-022 §14).
        //
        // Everything up to here honours the host's stopping token: a shutdown prevents new work being
        // dispatched. Past this line it must NOT, and `CancellationToken.None` is the point of the change.
        //
        // Phase B established that cancelling the client does not reliably stop a server-side BACKUP. So
        // propagating shutdown into a running backup buys nothing and costs plenty: it tears down the
        // session while SQL Server keeps writing, releases the applock under an operation still in progress,
        // and strands the run row. A backup that has started is allowed to finish and record its own truth.
        //
        // The scheduler passes an identifier, an operation and the due anchor. Everything authoritative —
        // policy, hosting mode, routing, destination, credentials — is re-read by the executor and provider,
        // because the projection that selected this work is advisory and may already be stale.
        var outcome = await executor.ExecuteScheduledAsync(
          work.Candidate.TenantDatabaseId, work.Operation, work.DueAnchorUtc, CancellationToken.None);

        var elapsed = (clock.UtcNow - startedUtc).TotalMilliseconds;
        summary.Record(outcome.IsSuccess ? outcome.Value.Status : null);

        if (outcome.IsSuccess)
        {
          LogOperationOutcome(
            logger, work.Candidate.TenantDatabaseId, work.Candidate.ServerKey,
            work.Operation.OperationCode, outcome.Value.Status.ToString(), elapsed, null);
        }
        else
        {
          LogOperationRefused(
            logger, work.Candidate.TenantDatabaseId, work.Candidate.ServerKey,
            work.Operation.OperationCode, outcome.Error.Code, null);
        }
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
#pragma warning disable CA1031 // One database must never take down a sweep.
    catch (Exception exception)
#pragma warning restore CA1031
    {
      // PER-ITEM ISOLATION. A database whose execution throws is recorded and stepped over; the rest of the
      // fleet still gets its backup. The exception type is deliberately unconstrained because a provider,
      // a driver or a repository can each surface something different, and none of them is a reason to stop
      // protecting every other database.
      summary.Record(null);
      LogOperationFaulted(
        logger, work.Candidate.TenantDatabaseId, work.Candidate.ServerKey,
        work.Operation.OperationCode, exception.GetType().Name, exception);
    }
    finally
    {
      globalGate.Release();
    }
  }

  // Round-robin across ServerKey buckets: one item from each server, then the next, so the global cap is
  // shared out rather than consumed by whichever server happens to sort first.
  private static IEnumerable<DueWork> Interleave(List<DueWork> runnable)
  {
    var buckets = runnable
      .GroupBy(work => work.Candidate.ServerKey, StringComparer.Ordinal)
      .Select(group => group.ToList())
      .ToList();

    for (var index = 0; buckets.Count > 0; index++)
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

  private static SemaphoreSlim ServerGate(
    Dictionary<string, SemaphoreSlim> gates,
    string serverKey,
    int perServerLimit)
  {
    if (!gates.TryGetValue(serverKey, out var gate))
    {
      gate = new SemaphoreSlim(perServerLimit);
      gates[serverKey] = gate;
    }

    return gate;
  }

  private sealed record DueWork(
    TenantDatabaseBackupDueCandidate Candidate,
    TenantDatabaseBackupOperation Operation,
    DateTimeOffset? DueAnchorUtc);

  private sealed class SweepCounters
  {
    private int succeeded;
    private int failed;
    private int skipped;
    private int dispatched;

    public int Eligible { get; set; }

    public int Due { get; set; }

    public int Dispatched => dispatched;

    public void Record(TenantDatabaseBackupRunStatus? status)
    {
      Interlocked.Increment(ref dispatched);

      switch (status)
      {
        case TenantDatabaseBackupRunStatus.Succeeded:
          Interlocked.Increment(ref succeeded);
          break;
        case TenantDatabaseBackupRunStatus.SkippedOwnershipHeld:
        case TenantDatabaseBackupRunStatus.SkippedInFlightOperation:
        case TenantDatabaseBackupRunStatus.BlockedByPolicy:
          Interlocked.Increment(ref skipped);
          break;
        default:
          Interlocked.Increment(ref failed);
          break;
      }
    }

    public TenantDatabaseBackupSweepSummary ToSummary(DateTimeOffset startedUtc, DateTimeOffset completedUtc) =>
      new(startedUtc, completedUtc, Eligible, Due, dispatched, succeeded, failed, skipped);
  }

  // ---- Structured logging. Identifiers and outcomes only: never a resolved destination, a connection
  // string or anything derived from one.

  // Six parameters, because LoggerMessage.Define stops there. Eligible is dropped from the message rather
  // than the summary — "how many rows were paged" matters least of the counts to an operator reading a log.
  private static readonly Action<ILogger, int, int, int, int, int, double, Exception?> LogSweepCompleted =
    LoggerMessage.Define<int, int, int, int, int, double>(
      LogLevel.Information,
      new EventId(4310, nameof(LogSweepCompleted)),
      "Tenant backup sweep completed: {Due} due, {Dispatched} dispatched, " +
      "{Succeeded} succeeded, {Failed} failed, {Skipped} skipped in {ElapsedMilliseconds}ms.");

  private static readonly Action<ILogger, long, string, string, string, double, Exception?> LogOperationOutcome =
    LoggerMessage.Define<long, string, string, string, double>(
      LogLevel.Information,
      new EventId(4311, nameof(LogOperationOutcome)),
      "Tenant backup {Operation} for database {TenantDatabaseId} on {ServerKey} finished as {Status} " +
      "in {ElapsedMilliseconds}ms.");

  private static readonly Action<ILogger, long, string, string, string, Exception?> LogOperationRefused =
    LoggerMessage.Define<long, string, string, string>(
      LogLevel.Warning,
      new EventId(4312, nameof(LogOperationRefused)),
      "Tenant backup {Operation} for database {TenantDatabaseId} on {ServerKey} was refused: {Reason}.");

  private static readonly Action<ILogger, long, string, string, string, Exception?> LogOperationFaulted =
    LoggerMessage.Define<long, string, string, string>(
      LogLevel.Error,
      new EventId(4313, nameof(LogOperationFaulted)),
      "Tenant backup {Operation} for database {TenantDatabaseId} on {ServerKey} faulted with {ExceptionType}; " +
      "the sweep continues.");

  private static readonly Action<ILogger, int, Exception?> LogSweepCancelled =
    LoggerMessage.Define<int>(
      LogLevel.Information,
      new EventId(4314, nameof(LogSweepCancelled)),
      "Tenant backup sweep cancelled after dispatching {Dispatched} operations.");
}

// What one sweep did. Process-local, returned to the caller and logged; nothing is persisted.
public sealed record TenantDatabaseBackupSweepSummary(
  DateTimeOffset StartedUtc,
  DateTimeOffset CompletedUtc,
  int Eligible,
  int Due,
  int Dispatched,
  int Succeeded,
  int Failed,
  int Skipped);
