using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRestoreVerificationSchedulerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

  [Theory]
  [InlineData(false, -1, 0)]
  [InlineData(true, -30, 0)]
  [InlineData(true, -31, 1)]
  public async Task Due_calculation_pins_null_never_verified_and_exact_interval_boundaries(
    bool hasInterval, int previousAgeDays, int expectedExecutions)
  {
    var candidate = Candidate() with
    {
      RestoreVerificationIntervalDays = hasInterval ? 30 : null,
      PreviousSuccessfulVerificationRunId = previousAgeDays < 0 ? 9 : null,
      PreviousSuccessfulVerificationCompletedUtc = previousAgeDays < 0 ? Now.AddDays(previousAgeDays) : null
    };
    var fixture = Fixture.For(candidate);

    await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(expectedExecutions, fixture.Executor.Calls);
  }

  [Fact]
  public async Task Never_verified_database_with_platform_full_baseline_is_due()
  {
    var fixture = Fixture.For(Candidate() with
    {
      PreviousSuccessfulVerificationRunId = null,
      PreviousSuccessfulVerificationCompletedUtc = null
    });

    var summary = await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(1, summary.Due);
    Assert.Equal(1, fixture.Executor.Calls);
  }

  [Theory]
  [InlineData(TenantDatabaseHostingMode.CustomerManaged, TenantDatabaseBackupManagementMode.AutomaticByPlatform)]
  [InlineData(TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseBackupManagementMode.PlatformAfterApproval)]
  [InlineData(TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseBackupManagementMode.CustomerDba)]
  public async Task Ineligible_management_or_hosting_modes_are_not_automatically_scheduled(
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseBackupManagementMode managementMode)
  {
    var fixture = Fixture.For(Candidate() with { HostingMode = hostingMode, ManagementMode = managementMode });

    await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(0, fixture.Executor.Calls);
  }

  [Fact]
  public async Task A_stale_due_admission_does_not_invoke_d7_twice()
  {
    var fixture = Fixture.For(Candidate());
    fixture.Store.AdmissionError = TenantStorageErrors.RestoreVerificationAlreadySatisfied;

    var summary = await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(1, summary.Skipped);
    Assert.Equal(0, fixture.Executor.Calls);
  }

  [Theory]
  [InlineData("AlreadyAdmitted")]
  [InlineData("AlreadySatisfied")]
  public async Task Readiness_refresh_is_not_contingent_on_admission(string reason)
  {
    var fixture = Fixture.For(Candidate());
    fixture.Store.AdmissionError = reason == "AlreadyAdmitted"
      ? TenantStorageErrors.RestoreVerificationAlreadyAdmitted
      : TenantStorageErrors.RestoreVerificationAlreadySatisfied;

    await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(1, fixture.Readiness.Refreshes);
    Assert.Equal(0, fixture.Executor.Calls);
  }

  [Fact]
  public async Task One_readiness_refresh_failure_does_not_stop_the_fleet_sweep()
  {
    var first = Candidate();
    var second = Candidate() with { TenantDatabaseId = 2 };
    var fleet = new FakeFleet(first, second);
    var readiness = new FakeReadinessRefresher { FailingTenantDatabaseId = first.TenantDatabaseId };
    var scheduler = Fixture.Create(fleet, new FakeRunStore(), new FakeExecutor(), readiness);

    var summary = await scheduler.RunSweepAsync();

    Assert.Equal(2, readiness.Refreshes);
    Assert.Equal(1, summary.Dispatched);
  }

  [Fact]
  public async Task Two_scheduler_instances_admit_and_execute_one_effective_operation()
  {
    var candidate = Candidate();
    var fleet = new FakeFleet(candidate);
    var store = new FakeRunStore();
    var executor = new FakeExecutor();
    var first = Fixture.Create(fleet, store, executor);
    var second = Fixture.Create(fleet, store, executor);

    await Task.WhenAll(first.RunSweepAsync(), second.RunSweepAsync());

    Assert.Equal(1, store.SuccessfulAdmissions);
    Assert.Equal(1, executor.Calls);
  }

  [Fact]
  public async Task One_shared_physical_database_produces_one_admission_and_executor_invocation()
  {
    var fixture = Fixture.For(Candidate());

    await fixture.Scheduler.RunSweepAsync();

    Assert.Equal(1, fixture.Store.SuccessfulAdmissions);
    Assert.Equal(1, fixture.Executor.Calls);
  }

  private static TenantDatabaseRestoreVerificationDueCandidate Candidate() => new(
    1, "source", TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseProvisioningStatus.Ready,
    TenantDatabaseBackupManagementMode.AutomaticByPlatform, true, null, null, 30, 101, null, null);

  private sealed class Fixture
  {
    private Fixture(FakeFleet fleet, FakeRunStore store, FakeExecutor executor)
    {
      Store = store;
      Executor = executor;
      Readiness = new FakeReadinessRefresher();
      Scheduler = Create(fleet, store, executor, Readiness);
    }

    public FakeRunStore Store { get; }
    public FakeExecutor Executor { get; }
    public FakeReadinessRefresher Readiness { get; }
    public TenantDatabaseRestoreVerificationScheduler Scheduler { get; }

    public static Fixture For(TenantDatabaseRestoreVerificationDueCandidate candidate) =>
      new(new FakeFleet(candidate), new FakeRunStore(), new FakeExecutor());

    public static TenantDatabaseRestoreVerificationScheduler Create(
      FakeFleet fleet, FakeRunStore store, FakeExecutor executor,
      FakeReadinessRefresher? readiness = null) => new(
      fleet, store, executor, new FakeReconciler(), readiness ?? new FakeReadinessRefresher(), Options.Create(new TenantDatabaseRestoreVerificationOptions
      {
        Enabled = true, RestoreServerKey = "verify", RestoreDataRoot = "D:\\verify", RestoreLogRoot = "L:\\verify",
        SchedulerBatchSize = 10, SchedulerSweepInterval = TimeSpan.FromMinutes(1),
        MaxConcurrentVerifications = 1, MaxConcurrentVerificationsPerServer = 1
      }), new FakeClock(), NullLogger<TenantDatabaseRestoreVerificationScheduler>.Instance);
  }

  private sealed class FakeFleet(params TenantDatabaseRestoreVerificationDueCandidate[] candidates)
    : ITenantDatabaseRestoreVerificationFleetReadRepository
  {
    public Task<IReadOnlyList<string>> ListEligibleSourceServerKeysAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<string>>(["source"]);

    public Task<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>> ListCandidatesAsync(
      string sourceServerKey, long afterTenantDatabaseId, int take, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>>(
        candidates.Where(candidate => candidate.TenantDatabaseId > afterTenantDatabaseId).Take(take).ToArray());

    public Task<DateTimeOffset?> FindLatestSuccessfulVerificationCompletedUtcAsync(
      long tenantDatabaseId, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
  }

  private sealed class FakeRunStore : ITenantDatabaseRestoreVerificationRunStore
  {
    private int admission;
    public int SuccessfulAdmissions { get; private set; }
    public Error? AdmissionError { get; set; }

    public Task<Result<long>> TryAdmitAsync(TenantDatabaseRestoreVerificationAdmissionRequest request,
      CancellationToken cancellationToken = default)
    {
      if (AdmissionError is { } error || Interlocked.CompareExchange(ref admission, 1, 0) != 0)
      {
        return Task.FromResult(Result.Failure<long>(AdmissionError ?? TenantStorageErrors.RestoreVerificationAlreadyAdmitted));
      }

      SuccessfulAdmissions++;
      return Task.FromResult(Result.Success(10L));
    }

    public Task<TenantDatabaseRestoreVerificationRunRecord?> FindAsync(long id, CancellationToken ct = default) =>
      Task.FromResult<TenantDatabaseRestoreVerificationRunRecord?>(null);
    public Task<Result> BeginRestoreAsync(long id, string name, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> MarkSucceededAsync(long id, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result<DateTimeOffset>> MarkSucceededAndRecordEvidenceAsync(long id, long backupId, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success(Now));
    public Task<Result> MarkFailedAsync(long id, string? reason, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> MarkInfrastructureUnavailableAsync(long id, string? reason, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> RecordCleanupAsync(long id, TenantDatabaseVerificationCleanupState state, string? reason, string actor, CancellationToken ct = default) => Task.FromResult(Result.Success());
  }

  private sealed class FakeExecutor : ITenantDatabaseRestoreVerificationExecutor
  {
    public int Calls { get; private set; }
    public Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> ExecuteAsync(long tenantDatabaseId,
      long expectedVerificationRunId, TenantDatabaseRestoreDepth requestedDepth, CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(Result.Success(new TenantDatabaseRestoreVerificationExecutionOutcome(
        tenantDatabaseId, expectedVerificationRunId, TenantDatabaseRestoreVerificationStatus.Succeeded,
        requestedDepth, true, null)));
    }
  }

  private sealed class FakeReconciler : ITenantDatabaseRestoreVerificationReconciler
  {
    public Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(new TenantDatabaseRestoreVerificationReconciliationSummary(Now, 0, 0, 0, 0, 0, 0, 0));
  }

  private sealed class FakeReadinessRefresher : ITenantDatabaseRecoveryReadinessRefresher
  {
    public int Refreshes { get; private set; }
    public long? FailingTenantDatabaseId { get; init; }

    public Task RefreshAsync(long tenantDatabaseId, CancellationToken cancellationToken = default)
    {
      Refreshes++;
      return tenantDatabaseId == FailingTenantDatabaseId
        ? Task.FromException(new InvalidOperationException("refresh failed"))
        : Task.CompletedTask;
    }
  }

  private sealed class FakeClock : IDateTimeProvider { public DateTimeOffset UtcNow => Now; }
}
