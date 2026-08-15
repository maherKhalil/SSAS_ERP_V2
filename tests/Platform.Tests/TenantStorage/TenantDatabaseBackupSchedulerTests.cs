using Microsoft.Extensions.Logging.Abstractions;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// Sweep mechanics (ADR-022 §13, TS-Backup Phase C).
//
// Driven entirely by fakes: no SQL Server, no BACKUP. The scheduler's job is selection, paging, bounding and
// isolation, and every one of those is a decision made before any database is touched — so proving them here
// is both cheaper and stricter than proving them through a real backup.
public sealed class TenantDatabaseBackupSchedulerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_sweep_dispatches_one_operation_per_due_database()
  {
    var reads = new FakeFleetReads(Due(1, "A"), Due(2, "A"));
    var executor = new FakeExecutor();

    var summary = await Scheduler(reads, executor).RunSweepAsync();

    Assert.Equal(2, summary.Dispatched);
    Assert.Equal(2, executor.Calls.Count);
    Assert.Equal([1L, 2L], executor.Calls.Select(call => call.TenantDatabaseId).Order());
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_database_with_several_operations_due_receives_exactly_one_call()
  {
    // ADR-022 compliance rule 31: one operation per physical database per sweep, and the log wins.
    var candidate = Due(1, "A") with
    {
      FullBackupIntervalMinutes = 60,
      DifferentialBackupIntervalMinutes = 60,
      TransactionLogBackupIntervalMinutes = 15,
      LastSuccessfulFullBackupUtc = Now.AddDays(-7),
      LastSuccessfulDifferentialBackupUtc = Now.AddDays(-7),
      LastSuccessfulLogBackupUtc = Now.AddDays(-7)
    };

    var executor = new FakeExecutor();
    await Scheduler(new FakeFleetReads(candidate), executor).RunSweepAsync();

    Assert.Single(executor.Calls);
    Assert.Equal("TransactionLog", executor.Calls[0].Operation.OperationCode);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Databases_that_are_not_due_produce_no_executor_call_and_no_run()
  {
    // The run-history-noise guard: nothing due means nothing dispatched, so no run row is created to record
    // that the scheduler looked and moved on.
    var notDue = Due(1, "A") with
    {
      FullBackupIntervalMinutes = 10_080,
      LastSuccessfulFullBackupUtc = Now.AddMinutes(-5)
    };

    var executor = new FakeExecutor();
    var summary = await Scheduler(new FakeFleetReads(notDue), executor).RunSweepAsync();

    Assert.Empty(executor.Calls);
    Assert.Equal(0, summary.Dispatched);
    Assert.Equal(1, summary.Eligible);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Paging_walks_the_fleet_by_ascending_id_without_duplicates()
  {
    var candidates = Enumerable.Range(1, 25).Select(id => Due(id, "A")).ToArray();
    var reads = new FakeFleetReads(candidates) { PageSize = 10 };
    var executor = new FakeExecutor();

    await Scheduler(reads, executor, batchSize: 10).RunSweepAsync();

    var dispatched = executor.Calls.Select(call => call.TenantDatabaseId).Order().ToArray();
    Assert.Equal(25, dispatched.Length);
    Assert.Equal(dispatched.Distinct().Count(), dispatched.Length);

    // Keyset, not OFFSET: every page was requested by the last id of the previous one.
    Assert.Equal([0L, 10L, 20L, 25L], reads.RequestedAfterIds);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_recent_failure_suppresses_dispatch_without_creating_a_run()
  {
    var reads = new FakeFleetReads(Due(1, "A"))
    {
      LatestRuns = { [1L] = Run(TenantDatabaseBackupRunStatus.Failed, Now.AddMinutes(-1)) }
    };

    var executor = new FakeExecutor();
    await Scheduler(reads, executor).RunSweepAsync();

    Assert.Empty(executor.Calls);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_failure_beyond_the_backoff_is_retried()
  {
    var reads = new FakeFleetReads(Due(1, "A"))
    {
      LatestRuns = { [1L] = Run(TenantDatabaseBackupRunStatus.Failed, Now.AddHours(-2)) }
    };

    var executor = new FakeExecutor();
    await Scheduler(reads, executor).RunSweepAsync();

    Assert.Single(executor.Calls);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Global_concurrency_is_bounded()
  {
    var reads = new FakeFleetReads(Enumerable.Range(1, 8).Select(id => Due(id, $"Server{id}")).ToArray());
    var executor = new FakeExecutor { HoldMilliseconds = 40 };

    await Scheduler(reads, executor, maxConcurrent: 2, maxPerServer: 1).RunSweepAsync();

    Assert.Equal(8, executor.Calls.Count);
    Assert.True(executor.MaximumObservedConcurrency <= 2,
      $"observed {executor.MaximumObservedConcurrency} concurrent backups against a global cap of 2");
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Per_server_concurrency_is_bounded_independently_of_the_global_cap()
  {
    // The cap that matters on shared hosting: four databases behind one ServerKey, a global cap that would
    // otherwise allow three at once, and one SQL Server that should still see one backup at a time.
    var reads = new FakeFleetReads(Enumerable.Range(1, 4).Select(id => Due(id, "Shared")).ToArray());
    var executor = new FakeExecutor { HoldMilliseconds = 40 };

    await Scheduler(reads, executor, maxConcurrent: 3, maxPerServer: 1).RunSweepAsync();

    Assert.Equal(4, executor.Calls.Count);
    Assert.True(executor.MaximumConcurrencyFor("Shared") <= 1,
      $"observed {executor.MaximumConcurrencyFor("Shared")} concurrent backups on one server against a cap of 1");
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Different_servers_run_concurrently()
  {
    var reads = new FakeFleetReads(Due(1, "A"), Due(2, "B"));
    var executor = new FakeExecutor { HoldMilliseconds = 60 };

    await Scheduler(reads, executor, maxConcurrent: 2, maxPerServer: 1).RunSweepAsync();

    Assert.Equal(2, executor.MaximumObservedConcurrency);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_busy_server_does_not_starve_another_servers_overdue_database()
  {
    // Round-robin interleaving: without it, six databases on one server would consume both global slots and
    // the single database on the quiet server would wait behind all of them.
    var candidates = Enumerable.Range(1, 6).Select(id => Due(id, "Busy")).Append(Due(99, "Quiet")).ToArray();
    var executor = new FakeExecutor { HoldMilliseconds = 20 };

    await Scheduler(new FakeFleetReads(candidates), executor, maxConcurrent: 2, maxPerServer: 1).RunSweepAsync();

    // The quiet server's database should be reached early rather than last.
    var quietPosition = executor.StartOrder.IndexOf(99L);
    Assert.True(quietPosition <= 1,
      $"the quiet server's database started at position {quietPosition}, behind the busy server's queue");
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task One_failing_database_does_not_stop_the_sweep()
  {
    var executor = new FakeExecutor { ThrowForDatabaseId = 2 };
    var reads = new FakeFleetReads(Due(1, "A"), Due(2, "A"), Due(3, "A"));

    var summary = await Scheduler(reads, executor).RunSweepAsync();

    Assert.Equal(3, summary.Dispatched);
    Assert.Contains(1L, executor.Calls.Select(call => call.TenantDatabaseId));
    Assert.Contains(3L, executor.Calls.Select(call => call.TenantDatabaseId));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_refusal_from_the_executor_is_recorded_rather_than_thrown()
  {
    // The scheduler treats an authority refusal as information, not as a sweep failure — the policy may have
    // changed between the projection being read and the execution being attempted.
    var executor = new FakeExecutor { RefuseWithError = TenantStorageErrors.BackupNotPermittedByManagementMode };

    var summary = await Scheduler(new FakeFleetReads(Due(1, "A")), executor).RunSweepAsync();

    Assert.Equal(1, summary.Dispatched);
    Assert.Single(executor.Calls);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Cancellation_stops_dispatching_further_work()
  {
    using var cancellation = new CancellationTokenSource();
    var executor = new FakeExecutor { OnCall = () => cancellation.Cancel() };
    var reads = new FakeFleetReads(Enumerable.Range(1, 20).Select(id => Due(id, $"S{id}")).ToArray())
    {
      PageSize = 1
    };

    var summary = await Scheduler(reads, executor, batchSize: 1)
      .RunSweepAsync(cancellation.Token);

    // It stops promptly rather than working through the remaining fleet.
    Assert.True(executor.Calls.Count < 20,
      $"cancellation did not stop dispatch; {executor.Calls.Count} of 20 databases were still dispatched");
    Assert.True(summary.Dispatched <= executor.Calls.Count + 1);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_started_backup_never_receives_the_hosts_stopping_token()
  {
    // MEDIUM-2 REGRESSION. Phase B established that cancelling the client does not reliably stop a
    // server-side BACKUP, so propagating host shutdown into a started operation tears down the session for
    // nothing and strands the run. Everything before dispatch honours the token; nothing after it does.
    using var cancellation = new CancellationTokenSource();
    var executor = new FakeExecutor();

    await Scheduler(new FakeFleetReads(Due(1, "A")), executor).RunSweepAsync(cancellation.Token);

    Assert.Single(executor.ObservedCancellableTokens);
    Assert.False(executor.ObservedCancellableTokens[0],
      "the scheduler handed a cancellable token to a started backup");
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Shutdown_between_acquiring_a_permit_and_starting_prevents_the_backup()
  {
    // LOW-A. Waiting for a concurrency permit can take as long as the backup ahead of it, so cancellation
    // frequently lands in exactly this gap. The executor must not be called at all — and the permits must
    // still be released, or a later sweep in the same process would deadlock against a drained semaphore.
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var executor = new FakeExecutor();
    var scheduler = Scheduler(new FakeFleetReads(Due(1, "A"), Due(2, "A")), executor);

    var summary = await scheduler.RunSweepAsync(cancellation.Token);

    Assert.Empty(executor.Calls);
    Assert.Equal(0, summary.Dispatched);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_due_anchor_travels_with_scheduled_work()
  {
    // The anchor is what lets the executor revalidate the decision under database ownership. Without it the
    // second instance has nothing to compare against and the duplicate cannot be detected.
    var lastFull = Now.AddDays(-8);
    var candidate = Due(1, "A") with { FullBackupIntervalMinutes = 60, LastSuccessfulFullBackupUtc = lastFull };

    var executor = new FakeExecutor();
    await Scheduler(new FakeFleetReads(candidate), executor).RunSweepAsync();

    Assert.Single(executor.ObservedAnchors);
    Assert.Equal(lastFull, executor.ObservedAnchors[0]);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_server_with_a_large_backlog_does_not_hide_another_servers_database()
  {
    // MEDIUM-4 REGRESSION. Server A has more due databases than a full page and every id below B's, so
    // identifier-ordered paging would have discovered B only after working through all of A. Server-aware
    // round-robin discovery means B contributes to the very first round.
    var busy = Enumerable.Range(1, 250).Select(id => Due(id, "Busy"));
    var quiet = Due(9_999, "Quiet");

    var executor = new FakeExecutor { HoldMilliseconds = 5 };
    var reads = new FakeFleetReads([.. busy, quiet]) { PageSize = 100 };

    await Scheduler(reads, executor, batchSize: 100, maxConcurrent: 2, maxPerServer: 1).RunSweepAsync();

    // Tightened from "within the first page" to what the design actually guarantees. Round-robin emits
    // Busy[0] then Quiet[0], and the global cap of two admits exactly those two before any third can start,
    // so the quiet server's database is structurally among the first two dispatched — not merely ahead of
    // the busy server's backlog.
    var quietPosition = executor.StartOrder.IndexOf(9_999L);
    Assert.True(quietPosition >= 0, "the quiet server's database was never dispatched");
    Assert.True(quietPosition <= 1,
      $"the quiet server's database started at position {quietPosition}, behind the busy server's backlog");
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Every_server_keeps_its_own_keyset_cursor()
  {
    // Cursors advance per server rather than across the fleet, so one server's identifiers cannot skip
    // another's. Proven by every database on both servers being dispatched exactly once.
    var candidates = Enumerable.Range(1, 5).Select(id => Due(id, "A"))
      .Concat(Enumerable.Range(6, 5).Select(id => Due(id, "B")))
      .ToArray();

    var executor = new FakeExecutor();
    await Scheduler(new FakeFleetReads(candidates) { PageSize = 2 }, executor, batchSize: 2).RunSweepAsync();

    var dispatched = executor.Calls.Select(call => call.TenantDatabaseId).Order().ToArray();
    Assert.Equal(Enumerable.Range(1, 10).Select(id => (long)id).ToArray(), dispatched);
    Assert.Equal(dispatched.Distinct().Count(), dispatched.Length);
  }

  private static TenantDatabaseBackupScheduler Scheduler(
    FakeFleetReads reads,
    FakeExecutor executor,
    int batchSize = 100,
    int maxConcurrent = 4,
    int maxPerServer = 4)
  {
    var options = new TenantDatabaseBackupSchedulerOptions
    {
      Enabled = true,
      BatchSize = batchSize,
      MaxConcurrentBackups = maxConcurrent,
      MaxConcurrentPerServer = maxPerServer
    };

    // The executor contract carries only an id, by design — the scheduler must not hand connection or
    // routing detail downstream. The fake therefore learns the id-to-server mapping from the same candidate
    // set the reads serve, purely so the per-server concurrency assertions can attribute a call.
    foreach (var candidate in reads.Candidates)
    {
      executor.ServerKeys[candidate.TenantDatabaseId] = candidate.ServerKey;
    }

    return new TenantDatabaseBackupScheduler(
      reads, executor, new FixedClock(), Microsoft.Extensions.Options.Options.Create(options),
      NullLogger<TenantDatabaseBackupScheduler>.Instance);
  }

  private static TenantDatabaseBackupDueCandidate Due(long id, string serverKey) =>
    new(
      id, serverKey,
      TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseProvisioningStatus.Ready,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      PolicyEnabled: true,
      FullBackupIntervalMinutes: 60,
      DifferentialBackupIntervalMinutes: null,
      TransactionLogBackupIntervalMinutes: null,
      LastSuccessfulFullBackupUtc: null,
      LastSuccessfulDifferentialBackupUtc: null,
      LastSuccessfulLogBackupUtc: null);

  private static TenantDatabaseBackupRunRecord Run(TenantDatabaseBackupRunStatus status, DateTimeOffset when) =>
    new(1, 1, "SqlServer", "Full", status, when, when, null, null, null, null,
      TenantDatabaseBackupVerificationState.NotVerified, null, null);

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }

  private sealed class FakeFleetReads(params TenantDatabaseBackupDueCandidate[] candidates)
    : ITenantDatabaseBackupFleetReadRepository
  {
    public int PageSize { get; init; } = 100;

    public IReadOnlyList<TenantDatabaseBackupDueCandidate> Candidates => candidates;

    public List<long> RequestedAfterIds { get; } = [];

    public Dictionary<long, TenantDatabaseBackupRunRecord> LatestRuns { get; } = [];

    public Task<IReadOnlyList<string>> ListEligibleServerKeysAsync(CancellationToken cancellationToken = default)
    {
      IReadOnlyList<string> serverKeys =
      [
        .. candidates.Select(candidate => candidate.ServerKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
      ];

      return Task.FromResult(serverKeys);
    }

    public Task<IReadOnlyList<TenantDatabaseBackupDueCandidate>> ListBackupCandidatesAsync(
      string serverKey, long afterId, int take, CancellationToken cancellationToken = default)
    {
      RequestedAfterIds.Add(afterId);

      IReadOnlyList<TenantDatabaseBackupDueCandidate> page =
      [
        .. candidates
          .Where(candidate => string.Equals(candidate.ServerKey, serverKey, StringComparison.Ordinal))
          .Where(candidate => candidate.TenantDatabaseId > afterId)
          .OrderBy(candidate => candidate.TenantDatabaseId)
          .Take(Math.Min(take, PageSize))
      ];

      return Task.FromResult(page);
    }

    public Task<IReadOnlyDictionary<long, TenantDatabaseBackupRunRecord>> ListLatestRunsAsync(
      IReadOnlyCollection<long> tenantDatabaseIds, CancellationToken cancellationToken = default)
    {
      IReadOnlyDictionary<long, TenantDatabaseBackupRunRecord> result =
        LatestRuns.Where(entry => tenantDatabaseIds.Contains(entry.Key))
          .ToDictionary(entry => entry.Key, entry => entry.Value);

      return Task.FromResult(result);
    }
  }

  private sealed class FakeExecutor : ITenantDatabaseBackupExecutor
  {
    private readonly object gate = new();
    private readonly Dictionary<string, int> activePerServer = [];
    private readonly Dictionary<string, int> peakPerServer = [];
    private int active;

    public List<(long TenantDatabaseId, TenantDatabaseBackupOperation Operation)> Calls { get; } = [];

    public List<long> StartOrder { get; } = [];

    public int MaximumObservedConcurrency { get; private set; }

    public int HoldMilliseconds { get; init; }

    public long? ThrowForDatabaseId { get; init; }

    public Error? RefuseWithError { get; init; }

    public Action? OnCall { get; init; }

    public int MaximumConcurrencyFor(string serverKey) =>
      peakPerServer.TryGetValue(serverKey, out var peak) ? peak : 0;

    // Records whether the scheduler handed down a live cancellation token. The shutdown contract is that it
    // must not: a started backup finishes on its own terms.
    public List<bool> ObservedCancellableTokens { get; } = [];

    public List<DateTimeOffset?> ObservedAnchors { get; } = [];

    public Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteAsync(
      long tenantDatabaseId,
      TenantDatabaseBackupOperation operation,
      CancellationToken cancellationToken = default) =>
      RunAsync(tenantDatabaseId, operation, null, cancellationToken);

    public Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteScheduledAsync(
      long tenantDatabaseId,
      TenantDatabaseBackupOperation operation,
      DateTimeOffset? dueAnchorUtc,
      CancellationToken cancellationToken = default) =>
      RunAsync(tenantDatabaseId, operation, dueAnchorUtc, cancellationToken);

    private async Task<Result<TenantDatabaseBackupExecutionOutcome>> RunAsync(
      long tenantDatabaseId,
      TenantDatabaseBackupOperation operation,
      DateTimeOffset? dueAnchorUtc,
      CancellationToken cancellationToken = default)
    {
      // The fake does not know the ServerKey, so concurrency per server is tracked by the scheduler's own
      // grouping: databases 1..n on one server all carry that server's key in the candidate. The scheduler
      // passes only an id, so the server is recovered from the call order the test set up.
      var serverKey = ServerKeyFor(tenantDatabaseId);

      lock (gate)
      {
        OnCall?.Invoke();
        Calls.Add((tenantDatabaseId, operation));
        StartOrder.Add(tenantDatabaseId);
        ObservedCancellableTokens.Add(cancellationToken.CanBeCanceled);
        ObservedAnchors.Add(dueAnchorUtc);

        active++;
        MaximumObservedConcurrency = Math.Max(MaximumObservedConcurrency, active);

        activePerServer[serverKey] = activePerServer.GetValueOrDefault(serverKey) + 1;
        peakPerServer[serverKey] = Math.Max(peakPerServer.GetValueOrDefault(serverKey), activePerServer[serverKey]);
      }

      try
      {
        if (HoldMilliseconds > 0)
        {
          await Task.Delay(HoldMilliseconds, cancellationToken);
        }

        if (ThrowForDatabaseId == tenantDatabaseId)
        {
          throw new InvalidOperationException("fake executor failure");
        }

        if (RefuseWithError is not null)
        {
          return Result.Failure<TenantDatabaseBackupExecutionOutcome>(RefuseWithError);
        }

        return Result.Success(new TenantDatabaseBackupExecutionOutcome(
          tenantDatabaseId, 1, operation.ProviderKey, operation.OperationCode,
          TenantDatabaseBackupRunStatus.Succeeded, "identity", null));
      }
      finally
      {
        lock (gate)
        {
          active--;
          activePerServer[serverKey]--;
        }
      }
    }

    // Server attribution for the concurrency assertions. Tests name servers per database, and the mapping is
    // recorded by the candidate set the fake reads served.
    public Dictionary<long, string> ServerKeys { get; } = [];

    private string ServerKeyFor(long tenantDatabaseId) =>
      ServerKeys.TryGetValue(tenantDatabaseId, out var key) ? key : "unknown";
  }
}
