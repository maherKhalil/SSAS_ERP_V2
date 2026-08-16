using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

[Trait("Decision", "ADR-022")]
public sealed class TenantDatabaseRestoreVerificationReconcilerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Old_admitted_run_with_observable_absent_database_and_no_restore_is_reconciled_once()
  {
    var fixture = Fixture.Admitted();

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Reconciled);
    Assert.Equal(1, fixture.Store.Transitions);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable, fixture.Store.Status);
  }

  [Fact]
  public async Task Old_admitted_run_with_unobservable_server_is_left_unchanged()
  {
    var fixture = Fixture.Admitted(observed: false);

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Unobservable);
    Assert.Equal(0, fixture.Store.Transitions);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted, fixture.Store.Status);
  }

  [Fact]
  public async Task Old_admitted_run_with_database_present_is_reported_not_reconciled()
  {
    var fixture = Fixture.Admitted(databaseExists: true);

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Conflicts);
    Assert.Equal(0, fixture.Store.Transitions);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Admitted, fixture.Store.Status);
  }

  [Fact]
  public async Task Old_restoring_run_with_active_restore_is_left_active()
  {
    var fixture = Fixture.Restoring(activeRestore: true);

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.LeftActive);
    Assert.Equal(0, fixture.Store.Transitions);
    Assert.Equal(TenantDatabaseRestoreVerificationStatus.Restoring, fixture.Store.Status);
  }

  [Fact]
  public async Task Old_restoring_run_with_unobservable_server_is_left_active()
  {
    var fixture = Fixture.Restoring(observed: false);

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Unobservable);
    Assert.Equal(0, fixture.Store.Transitions);
  }

  [Fact]
  public async Task Old_restoring_run_without_database_or_active_restore_is_reconciled()
  {
    var fixture = Fixture.Restoring();

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Reconciled);
    Assert.Equal(0, result.OrphansObserved);
    Assert.Equal(1, fixture.Store.Transitions);
  }

  [Fact]
  public async Task Old_restoring_run_with_database_present_is_reconciled_without_restore_evidence_or_deletion()
  {
    var fixture = Fixture.Restoring(databaseExists: true);

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.Reconciled);
    Assert.Equal(1, result.OrphansObserved);
    Assert.Equal("ReconciledAbandonedWithOrphan", fixture.Store.Reason);
    Assert.Equal(0, fixture.Observer.DeleteCalls);
  }

  [Fact]
  public async Task Two_reconcilers_racing_the_same_run_apply_exactly_one_transition()
  {
    var store = new FakeStore(Run(TenantDatabaseRestoreVerificationStatus.Restoring));
    var observer = new FakeObserver();
    var first = Create(store, observer);
    var second = Create(store, observer);

    var results = await Task.WhenAll(first.ReconcileAsync(), second.ReconcileAsync());

    Assert.Equal(1, store.Transitions);
    Assert.Equal(1, results.Sum(result => result.Reconciled));
    Assert.Equal(1, results.Sum(result => result.RacesLost));
  }

  [Fact]
  public async Task Run_inside_reconciliation_grace_is_left_unchanged()
  {
    var run = Run(TenantDatabaseRestoreVerificationStatus.Restoring) with { StartedUtc = Now.AddMinutes(-1) };
    var fixture = new Fixture(new FakeStore(run), new FakeObserver());

    var result = await fixture.Reconciler.ReconcileAsync();

    Assert.Equal(1, result.LeftActive);
    Assert.Equal(0, fixture.Store.Transitions);
  }

  private static TenantDatabaseRestoreVerificationActiveRunRecord Run(
    TenantDatabaseRestoreVerificationStatus status) => new(
      10,
      1,
      101,
      TenantDatabaseRestoreDepth.Full,
      "verify",
      "source",
      status,
      status == TenantDatabaseRestoreVerificationStatus.Restoring
        ? TenantDatabaseVerificationNaming.ForRun(1, 10)
        : null,
      Now.AddHours(-12));

  private static TenantDatabaseRestoreVerificationReconciler Create(FakeStore store, FakeObserver observer) =>
    new(store, observer, Options.Create(new TenantDatabaseRestoreVerificationOptions
    {
      Enabled = true,
      RestoreServerKey = "verify",
      RestoreDataRoot = "D:\\verify",
      RestoreLogRoot = "L:\\verify",
      ReconciliationGracePeriod = TimeSpan.FromHours(6)
    }), new FakeClock(), NullLogger<TenantDatabaseRestoreVerificationReconciler>.Instance);

  private sealed class Fixture
  {
    public Fixture(FakeStore store, FakeObserver observer)
    {
      Store = store;
      Observer = observer;
      Reconciler = Create(store, observer);
    }

    public FakeStore Store { get; }
    public FakeObserver Observer { get; }
    public TenantDatabaseRestoreVerificationReconciler Reconciler { get; }

    public static Fixture Admitted(bool observed = true, bool databaseExists = false) =>
      new(new FakeStore(Run(TenantDatabaseRestoreVerificationStatus.Admitted)),
        new FakeObserver(observed, databaseExists));

    public static Fixture Restoring(
      bool observed = true,
      bool databaseExists = false,
      bool activeRestore = false) =>
      new(new FakeStore(Run(TenantDatabaseRestoreVerificationStatus.Restoring)),
        new FakeObserver(observed, databaseExists, activeRestore));
  }

  private sealed class FakeStore(TenantDatabaseRestoreVerificationActiveRunRecord run)
    : ITenantDatabaseRestoreVerificationReconciliationStore
  {
    private int active = 1;

    public TenantDatabaseRestoreVerificationStatus Status { get; private set; } = run.Status;
    public int Transitions { get; private set; }
    public string? Reason { get; private set; }

    public Task<IReadOnlyList<TenantDatabaseRestoreVerificationActiveRunRecord>> ListActiveAsync(
      long afterVerificationRunId, int take, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseRestoreVerificationActiveRunRecord>>(
        afterVerificationRunId == 0 ? [run] : []);

    public Task<Result> ReconcileAbandonedAsync(
      TenantDatabaseRestoreVerificationReconciliationTransitionRequest request,
      CancellationToken cancellationToken = default)
    {
      if (Interlocked.CompareExchange(ref active, 0, 1) != 1)
      {
        return Task.FromResult(Result.Failure(TenantStorageErrors.RestoreVerificationReconciliationStale));
      }

      Transitions++;
      Reason = request.ReasonSummary;
      Status = TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable;
      return Task.FromResult(Result.Success());
    }
  }

  private sealed class FakeObserver(
    bool observed = true,
    bool databaseExists = false,
    bool activeRestore = false) : ITenantDatabaseRestoreVerificationServerObserver
  {
    public int DeleteCalls { get; private set; }

    public Task<TenantDatabaseRestoreVerificationServerObservation> ObserveAsync(
      TenantDatabaseRestoreVerificationServerObservationRequest request,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new TenantDatabaseRestoreVerificationServerObservation(
        observed, databaseExists, activeRestore, UnavailableReason: observed ? null : "network"));
  }

  private sealed class FakeClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
