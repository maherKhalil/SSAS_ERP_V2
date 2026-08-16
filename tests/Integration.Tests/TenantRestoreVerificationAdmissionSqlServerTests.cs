using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// MULTI-INSTANCE ADMISSION for restore verification (ADR-022 §17, compliance rule 43).
//
// THE TEST THIS SLICE EXISTS TO PASS. Phase C shipped a duplicate-execution defect whose shape was: two
// instances observe the same work as due, each creates its own record, each successfully claims the record
// it created, both execute. ADR-022 v1.2 was revised specifically because the first draft of its own
// ownership rule would have permitted exactly that under a new key.
//
// So the proof has to be about ADMISSION, not about claiming: two application instances, the same due
// state, and exactly one effective operation. It runs against real SQL because the mechanism IS a database
// artifact — a filtered unique index — and an in-memory provider would enforce nothing.
//
// NOTHING HERE RESTORES ANYTHING. No RESTORE, no CREATE DATABASE, no DROP DATABASE: this proves who is
// allowed to proceed, which is settled entirely in the Platform database.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRestoreVerificationAdmissionSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_platform_migration_creates_the_verification_run_table_and_its_admission_index()
  {
    await using var fixture = await VerificationFixture.CreateAsync();

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.tables WHERE name = N'TenantDatabaseRestoreVerificationRuns' " +
      "AND SCHEMA_NAME(schema_id) = N'platform'"));

    // The index is the admission mechanism, so its existence, its uniqueness and its filter are all asserted
    // rather than assumed — a non-unique or unfiltered variant would silently stop enforcing the invariant.
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes WHERE name = N'UX_TenantDatabaseRestoreVerificationRuns_ActiveTenantDatabase' " +
      "AND is_unique = 1 AND has_filter = 1"));
  }

  // TWO INSTANCES, ONE DUE STATE, ONE EFFECTIVE OPERATION.
  //
  // Deterministic rather than timing-based: both contexts are prepared first and their admissions are
  // started together, and the assertion is on the OUTCOME COUNT, not on who won. Whichever ordering the
  // database happens to pick, exactly one may proceed.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Two_instances_observing_the_same_due_state_admit_exactly_one_verification()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_A");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    // Two independent stores over two independent contexts — the closest a single process gets to two
    // application instances sharing one Platform database.
    var first = fixture.RunStore();
    var second = fixture.RunStore();

    var admissions = await Task.WhenAll(
      Task.Run(() => first.TryAdmitAsync(Request(databaseId, baselineId))),
      Task.Run(() => second.TryAdmitAsync(Request(databaseId, baselineId))));

    var admitted = admissions.Count(result => result.IsSuccess);
    var rejected = admissions.Where(result => result.IsFailure).ToArray();

    Assert.Equal(1, admitted);
    Assert.Single(rejected);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationAlreadyAdmitted.Code,
      rejected[0].Error.Code);

    // And the database agrees: one active operation, not two.
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      "WHERE [Status] IN (N'Admitted', N'Restoring')"));
  }

  // A CLAIM ON AN ALREADY-CREATED ROW WOULD NOT HAVE CAUGHT THIS. Sequentially, each instance would create
  // its own record and claim it successfully — which is why the invariant binds creation.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_second_instance_cannot_create_a_parallel_operation_for_the_same_database()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_B");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    var firstResult = await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId));
    Assert.True(firstResult.IsSuccess);

    var secondResult = await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId));

    Assert.True(secondResult.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationAlreadyAdmitted.Code,
      secondResult.Error.Code);
  }

  // THE AUTHORITATIVE RECHECK, closing the sequential half of the duplicate. A scheduler's view of "due" can
  // be stale by the time it reaches admission — the Phase C lesson — so a baseline that has been superseded
  // is refused rather than verified.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_stale_baseline_decision_is_refused_at_admission()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_C");
    var staleBaselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    // A newer full lands between the scheduler's decision and admission.
    await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    var result = await fixture.RunStore().TryAdmitAsync(Request(databaseId, staleBaselineId));

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreVerificationNotDue.Code, result.Error.Code);
    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns]"));
  }

  // THE SEQUENTIAL STALE-DUE DUPLICATE — the case a baseline-only recheck cannot see.
  //
  // A and B are created from the SAME due snapshot. A completes successfully. B then acts on its old
  // snapshot: the baseline has not moved, and A's terminal status has freed the active slot, so neither the
  // baseline recheck nor the unique index would stop it. The verification anchor is what rejects it.
  //
  // An earlier version of this suite asserted the OPPOSITE here — that re-admitting against the same
  // completed due state succeeds — which documented the defect as intended behaviour and let it pass a green
  // suite.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_stale_decision_is_rejected_after_another_instance_satisfied_the_same_due_state()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_D");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    // Both instances snapshot the same due state: this baseline, and no previous successful verification.
    var snapshot = Request(databaseId, baselineId, previousVerificationId: null);

    var firstId = (await fixture.RunStore().TryAdmitAsync(snapshot)).Value;
    await fixture.CompleteSuccessfullyAsync(databaseId, firstId);

    var staleResult = await fixture.RunStore().TryAdmitAsync(snapshot);

    Assert.True(staleResult.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationAlreadySatisfied.Code,
      staleResult.Error.Code);

    // And no second operation exists for that due state.
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns]"));
  }

  // THE ORDERING TEST. The verification run reaches Succeeded before anything propagates a derived timestamp
  // onto the TenantDatabase row — the fixture never writes one at all — and the stale decision must STILL be
  // rejected.
  //
  // This is what proves admission reads authoritative durable evidence rather than the lagging aggregate
  // field. Depending on LastRestoreVerificationUtc would leave a gap between the run going terminal (slot
  // freed) and the timestamp catching up, which is Phase C's ordering defect rebuilt under a new name.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_stale_decision_is_rejected_even_before_the_aggregate_timestamp_is_propagated()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_H");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var snapshot = Request(databaseId, baselineId, previousVerificationId: null);

    var firstId = (await fixture.RunStore().TryAdmitAsync(snapshot)).Value;
    await fixture.CompleteSuccessfullyAsync(databaseId, firstId);

    // The aggregate observation is deliberately still empty.
    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabases] " +
      $"WHERE [TenantDatabaseId] = {databaseId} AND [LastRestoreVerificationUtc] IS NOT NULL"));

    var staleResult = await fixture.RunStore().TryAdmitAsync(snapshot);

    Assert.True(staleResult.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationAlreadySatisfied.Code,
      staleResult.Error.Code);
  }

  // AND WE HAVE NOT OVER-CORRECTED. The same full baseline legitimately needs verifying again when the
  // policy's interval expires and no newer full exists — that is a NEW due state, anchored to the
  // verification that has since gone stale, and it must be admissible.
  //
  // A rule like "never verify a baseline twice" would have closed the duplicate and broken this.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_new_due_state_against_the_same_baseline_is_admissible()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_I");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    // V1 against the same baseline, with no prior verification.
    var firstId = (await fixture.RunStore()
      .TryAdmitAsync(Request(databaseId, baselineId, previousVerificationId: null))).Value;
    await fixture.CompleteSuccessfullyAsync(databaseId, firstId);

    // The interval later expires. A NEW decision is made, anchored to V1 — same baseline, newer due state.
    var secondResult = await fixture.RunStore()
      .TryAdmitAsync(Request(databaseId, baselineId, previousVerificationId: firstId));

    Assert.True(secondResult.IsSuccess);
    var secondId = secondResult.Value;
    await fixture.CompleteSuccessfullyAsync(databaseId, secondId);

    // Terminal completion freed the active slot — but the decision anchored to V1 is now stale in its turn.
    var replayResult = await fixture.RunStore()
      .TryAdmitAsync(Request(databaseId, baselineId, previousVerificationId: firstId));

    Assert.True(replayResult.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RestoreVerificationAlreadySatisfied.Code,
      replayResult.Error.Code);

    // ...while a decision anchored to V2 is the current one and may proceed.
    var nextResult = await fixture.RunStore()
      .TryAdmitAsync(Request(databaseId, baselineId, previousVerificationId: secondId));

    Assert.True(nextResult.IsSuccess);
  }

  // A FAILED verification satisfies no obligation, so it must not move the anchor and make a legitimate
  // retry of the same due state look stale. Retry timing itself remains a scheduler concern.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_failed_verification_does_not_satisfy_the_due_state()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_J");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var snapshot = Request(databaseId, baselineId, previousVerificationId: null);

    var firstId = (await fixture.RunStore().TryAdmitAsync(snapshot)).Value;
    var store = fixture.RunStore();
    await store.BeginRestoreAsync(
      firstId, TenantDatabaseVerificationNaming.ForRun(databaseId, firstId), "test");
    Assert.True((await store.MarkFailedAsync(firstId, "restore failed", "test")).IsSuccess);

    var retryResult = await fixture.RunStore().TryAdmitAsync(snapshot);

    Assert.True(retryResult.IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_policy_drift_refusal_frees_the_slot_for_a_fresh_later_admission()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_PolicyDrift");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var snapshot = Request(databaseId, baselineId, previousVerificationId: null);

    var staleRunId = (await fixture.RunStore().TryAdmitAsync(snapshot)).Value;
    Assert.True((await fixture.RunStore().MarkInfrastructureUnavailableAsync(
      staleRunId,
      "RestoreVerificationPolicyDrifted",
      "test")).IsSuccess);
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {staleRunId} " +
      $"AND [VerificationDatabaseName] = N'{TenantDatabaseVerificationNaming.ForRun(databaseId, staleRunId)}' " +
      "AND [CleanupState] = N'NotRequired' " +
      "AND [Status] = N'InfrastructureUnavailable'"));

    var freshAdmission = await fixture.RunStore().TryAdmitAsync(snapshot);

    Assert.True(freshAdmission.IsSuccess);
    Assert.NotEqual(staleRunId, freshAdmission.Value);
  }

  // Different physical databases never contend: the invariant is per database, not a fleet lock.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Different_physical_databases_admit_independently()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var firstDatabase = await fixture.RegisterAsync("SSAS_Verify_Admission_E1");
    var secondDatabase = await fixture.RegisterAsync("SSAS_Verify_Admission_E2");
    var firstBaseline = await fixture.RecordSuccessfulFullBackupAsync(firstDatabase);
    var secondBaseline = await fixture.RecordSuccessfulFullBackupAsync(secondDatabase);

    var first = await fixture.RunStore().TryAdmitAsync(Request(firstDatabase, firstBaseline));
    var second = await fixture.RunStore().TryAdmitAsync(Request(secondDatabase, secondBaseline));

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Concurrent_scheduler_workers_overlap_real_persistence_in_independent_dbcontexts()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var firstDatabase = await fixture.RegisterAsync("SSAS_Verify_Scope_A");
    var secondDatabase = await fixture.RegisterAsync("SSAS_Verify_Scope_B");
    var firstBaseline = await fixture.RecordSuccessfulFullBackupAsync(firstDatabase);
    var secondBaseline = await fixture.RecordSuccessfulFullBackupAsync(secondDatabase);
    var probe = new SqlPersistenceOverlapProbe(expectedWorkers: 2);
    var clock = new SchedulerClock();

    var services = new ServiceCollection();
    services.AddSingleton(probe);
    services.AddSingleton<IDateTimeProvider>(clock);
    services.AddSingleton<ICurrentUser>(new SchedulerUser());
    services.AddSingleton<ICurrentTenant>(new SchedulerTenant());
    services.AddDbContext<PlatformDbContext>(options => options.UseSqlServer(
      fixture.PlatformConnectionString,
      sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform")));
    services.AddScoped<TenantDatabaseRestoreVerificationRunStore>();
    services.AddScoped<ITenantDatabaseRestoreVerificationRunStore>(provider =>
      provider.GetRequiredService<TenantDatabaseRestoreVerificationRunStore>());
    services.AddScoped<ITenantDatabaseRestoreVerificationExecutor, SqlPersistenceOverlapExecutor>();
    await using var provider = services.BuildServiceProvider();

    var scheduler = new TenantDatabaseRestoreVerificationScheduler(
      new SchedulerFleet(
        Due(firstDatabase, firstBaseline),
        Due(secondDatabase, secondBaseline)),
      new NoOpReconciler(),
      new NoOpReadinessRefresher(),
      provider.GetRequiredService<IServiceScopeFactory>(),
      Options.Create(new TenantDatabaseRestoreVerificationOptions
      {
        Enabled = true,
        RestoreServerKey = "verify",
        RestoreDataRoot = "D:\\verify",
        RestoreLogRoot = "L:\\verify",
        SchedulerBatchSize = 10,
        MaxConcurrentVerifications = 2,
        MaxConcurrentVerificationsPerServer = 2
      }),
      clock,
      NullLogger<TenantDatabaseRestoreVerificationScheduler>.Instance);

    var summary = await scheduler.RunSweepAsync();

    Assert.Equal(2, probe.ContextIds.Count);
    Assert.Equal(2, probe.ContextIds.Distinct().Count());
    Assert.Equal(2, probe.CompletedPersistenceOperations);
    Assert.Equal(2, summary.Dispatched);
    Assert.Equal(2, summary.Failed);
    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      "WHERE [Status] IN (N'Admitted', N'Restoring')"));
  }

  private static TenantDatabaseRestoreVerificationDueCandidate Due(long databaseId, long baselineId) =>
    new(databaseId, "source", TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseProvisioningStatus.Ready,
      TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      PolicyEnabled: true,
      DifferentialBackupIntervalMinutes: null,
      TransactionLogBackupIntervalMinutes: null,
      RestoreVerificationIntervalDays: 30,
      SourceBackupRunId: baselineId,
      PreviousSuccessfulVerificationRunId: null,
      PreviousSuccessfulVerificationCompletedUtc: null);

  // The crash-survivability guarantee, enforced by the database rather than by convention: a restoring run
  // must name the database it is holding.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_restoring_run_cannot_exist_without_naming_its_verification_database()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_F");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var runId = (await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId))).Value;

    var violation = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      "UPDATE [platform].[TenantDatabaseRestoreVerificationRuns] " +
      "SET [Status] = N'Restoring', [VerificationDatabaseName] = NULL " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {runId}"));

    Assert.Contains("CK_TenantDatabaseRestoreVerificationRuns_RestoringHasDatabaseName",
      violation.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Exactly_one_executor_can_compare_and_set_admitted_to_restoring()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_CAS");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var runId = (await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId))).Value;
    var name = TenantDatabaseVerificationNaming.ForRun(databaseId, runId);

    var attempts = await Task.WhenAll(
      fixture.RunStore().BeginRestoreAsync(runId, name, "first"),
      fixture.RunStore().BeginRestoreAsync(runId, name, "second"));

    Assert.Single(attempts, result => result.IsSuccess);
    Assert.Single(attempts, result => result.IsFailure &&
      result.Error.Code == TenantStorageErrors.RestoreVerificationNotAdmitted.Code);
  }

  // THE RESERVED NAME IS NOT NEGOTIABLE AFTER ADMISSION.
  //
  // Admission reserves this run's database name durably. A caller arriving with a different one — a stale
  // worker, a regenerated guess, an off-by-one identity — must be refused rather than allowed to redirect the
  // restore, and the durable value must survive the attempt untouched. Without this, the reservation would
  // constrain nothing.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Begin_restore_refuses_a_name_that_is_not_the_reserved_one_and_leaves_it_intact()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_NameBinding");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var runId = (await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId))).Value;

    var reserved = TenantDatabaseVerificationNaming.ForRun(databaseId, runId);
    var wrong = TenantDatabaseVerificationNaming.ForRun(databaseId, runId + 1);

    var refused = await fixture.RunStore().BeginRestoreAsync(runId, wrong, "stale-worker");

    Assert.True(refused.IsFailure);

    // Still Admitted, still holding its own name: no transition, no overwrite.
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {runId} " +
      $"AND [Status] = N'Admitted' AND [VerificationDatabaseName] = N'{reserved}'"));

    // ...and the exact reserved name still transitions normally afterwards.
    var accepted = await fixture.RunStore().BeginRestoreAsync(runId, reserved, "owner");

    Assert.True(accepted.IsSuccess);
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {runId} " +
      $"AND [Status] = N'Restoring' AND [VerificationDatabaseName] = N'{reserved}'"));
  }

  // Cleanup failure and verification result are separate columns as well as separate concepts, so a proven
  // restore survives a failed drop in storage, not only in memory.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_cleanup_failure_is_persisted_without_disturbing_a_succeeded_verification()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_G");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var runId = (await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId))).Value;

    var store = fixture.RunStore();
    await store.BeginRestoreAsync(runId, TenantDatabaseVerificationNaming.ForRun(databaseId, runId), "test");
    await store.MarkSucceededAsync(runId, "test");
    await store.RecordCleanupAsync(
      runId, TenantDatabaseVerificationCleanupState.Failed, "drop blocked", "test");

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseRestoreVerificationRuns] " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {runId} " +
      "AND [Status] = N'Succeeded' AND [CleanupState] = N'Failed'"));
  }

  // A due-state snapshot. Both halves matter: the baseline being verified, and the successful verification
  // that existed when the decision was made (null = none had).
  // THE PRODUCTION PATH FOR CHECKPOINT METADATA (TS-Backup Phase D7).
  //
  // This closes the fixture gap that hid a real defect: the D5 chain tests read `checkpoint_lsn` straight
  // from `msdb` into their candidates, which gave them richer metadata than production ever receives. The
  // selector looked correctly wired while the Platform run row had no checkpoint column at all.
  //
  // Here the value travels the way it does in a deployment — reconciled from provider evidence, persisted on
  // the run, reloaded through the read repository — and chain selection consumes THAT.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_successful_backup_persists_its_checkpoint_lsn_for_chain_selection()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Checkpoint_A");

    // A successful full recorded through the run store with reconciled evidence, checkpoint included.
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId, checkpointLsn: 512m);

    var candidates = await fixture.BackupReads().ListChainCandidatesAsync(databaseId);

    var baseline = Assert.Single(candidates);
    Assert.Equal(baselineId, baseline.BackupRunId);
    Assert.Equal(512m, baseline.CheckpointLsn);

    // And a differential anchored to that persisted checkpoint is selectable from it.
    await fixture.RecordSuccessfulDifferentialAsync(databaseId, databaseBackupLsn: 512m);
    var withDifferential = await fixture.BackupReads().ListChainCandidatesAsync(databaseId);

    var chain = TenantDatabaseBackupChainSelector.Select(
      withDifferential, TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.True(chain.IsSuccess);
    Assert.Equal(TenantDatabaseRestoreDepth.FullWithDifferential, chain.Value.AchievedDepth);
  }

  // A run captured before the column existed stays NULL — never backfilled from FirstLsn — and depth is
  // refused rather than guessed.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_baseline_without_a_persisted_checkpoint_refuses_deeper_depth()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Checkpoint_B");
    await fixture.RecordSuccessfulFullBackupAsync(databaseId, checkpointLsn: null);

    var candidates = await fixture.BackupReads().ListChainCandidatesAsync(databaseId);
    Assert.Null(Assert.Single(candidates).CheckpointLsn);

    var result = TenantDatabaseBackupChainSelector.Select(
      candidates, TenantDatabaseRestoreDepth.FullWithDifferential);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RestoreChainMetadataUnavailable.Code, result.Error.Code);

    // ...while a full-only verification is unaffected.
    Assert.True(TenantDatabaseBackupChainSelector
      .Select(candidates, TenantDatabaseRestoreDepth.Full).IsSuccess);
  }

  private static TenantDatabaseRestoreVerificationAdmissionRequest Request(
    long tenantDatabaseId,
    long baselineBackupRunId,
    long? previousVerificationId = null) =>
    new(tenantDatabaseId, baselineBackupRunId, previousVerificationId,
      TenantDatabaseRestoreDepth.Full, "verify", "test");

  private sealed class VerificationFixture : IAsyncDisposable
  {
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private VerificationFixture(string platformCatalog) => PlatformCatalog = platformCatalog;

    private string PlatformCatalog { get; }

    public string PlatformConnectionString => ConnectionFor(PlatformCatalog);

    public static async Task<VerificationFixture> CreateAsync()
    {
      var fixture = new VerificationFixture($"SSAS_ERP_VERIFY_P_{Guid.NewGuid():N}");
      try
      {
        await using var platform = fixture.PlatformContext();
        await platform.Database.MigrateAsync();
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    public PlatformDbContext PlatformContext()
    {
      var builder = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(
          ConnectionFor(PlatformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"));

      return new PlatformDbContext(builder.Options, new TestUser(), new NoTenant(), new TestClock());
    }

    // A separate store over a SEPARATE context, so two of these contend the way two application instances
    // would rather than sharing a change tracker.
    public TenantDatabaseRestoreVerificationRunStore RunStore() =>
      new(PlatformContext(), new TestClock());

    public async Task<long> RegisterAsync(string databaseName)
    {
      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Dedicated,
        PrimaryServerKey, databaseName, TenantDatabaseProvisioningStatus.Ready, "verify-tests", Now).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    // A successful full backup row — the baseline a verification restores. Recorded directly rather than by
    // running BACKUP: this suite proves admission, not execution.
    public async Task<long> RecordSuccessfulFullBackupAsync(
      long tenantDatabaseId,
      decimal? checkpointLsn = null)
    {
      await using var platform = PlatformContext();
      var run = TenantDatabaseBackupRun.Start(
        tenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull(), "primary", "verify-tests", Now).Value;
      platform.TenantDatabaseBackupRuns.Add(run);
      await platform.SaveChangesAsync();

      // A run captured before Phase D7 still carries its first/last/database-backup LSNs — only the
      // CHECKPOINT is absent. Nulling the others too would make the row fail the candidate filter for an
      // unrelated reason and stop this exercising the missing-checkpoint path at all.
      run.Succeed(
        "evidence-identity", "artifact.bak", 1024,
        500m, 520m, 0m, checkpointLsn,
        null, "verify-tests", Now);
      await platform.SaveChangesAsync();
      return run.Id;
    }

    public async Task<long> RecordSuccessfulDifferentialAsync(
      long tenantDatabaseId,
      decimal databaseBackupLsn)
    {
      await using var platform = PlatformContext();
      var run = TenantDatabaseBackupRun.Start(
        tenantDatabaseId, TenantDatabaseBackupOperation.SqlServerDifferential(),
        "primary", "verify-tests", Now).Value;
      platform.TenantDatabaseBackupRuns.Add(run);
      await platform.SaveChangesAsync();

      run.Succeed(
        "diff-identity", "artifact-diff.bak", 512,
        600m, 620m, databaseBackupLsn, 600m, null, "verify-tests", Now);
      await platform.SaveChangesAsync();
      return run.Id;
    }

    public TenantDatabaseBackupReadRepository BackupReads() => new(PlatformContext());

    // Drives one admitted run to a successful terminal state. Deliberately does NOT propagate any derived
    // observation onto the TenantDatabase row, so the ordering test above exercises the gap between a run
    // going terminal and an aggregate timestamp catching up.
    public async Task CompleteSuccessfullyAsync(long tenantDatabaseId, long verificationRunId)
    {
      var store = RunStore();
      var begun = await store.BeginRestoreAsync(
        verificationRunId,
        TenantDatabaseVerificationNaming.ForRun(tenantDatabaseId, verificationRunId),
        "test");
      Assert.True(begun.IsSuccess);
      Assert.True((await store.MarkSucceededAsync(verificationRunId, "test")).IsSuccess);
    }

    public async Task<int> ScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(PlatformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(
        await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(PlatformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      try
      {
        await using var connection = new SqlConnection(
          new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
          $"IF DB_ID(N'{PlatformCatalog}') IS NOT NULL BEGIN " +
          $"ALTER DATABASE [{PlatformCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"DROP DATABASE [{PlatformCatalog}]; END";
        await command.ExecuteNonQueryAsync();
      }
      catch (SqlException)
      {
        // A leftover test catalog is an untidy environment, not a failed assertion.
      }
    }

    private sealed class TestUser : ICurrentUser
    {
      public string? UserId => "verify-tests";

      public string? UserName => null;

      public string? Email => null;

      public Guid? CompanyId => null;

      public string? SessionId => null;

      public string? TokenId => null;

      public IReadOnlyCollection<string> Roles => [];

      public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class NoTenant : ICurrentTenant
    {
      public Guid? TenantId => null;
    }

    private sealed class TestClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => Now;
    }
  }

  private sealed class SchedulerFleet(params TenantDatabaseRestoreVerificationDueCandidate[] candidates)
    : ITenantDatabaseRestoreVerificationFleetReadRepository
  {
    public Task<IReadOnlyList<string>> ListEligibleSourceServerKeysAsync(
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<string>>(["source"]);

    public Task<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>> ListCandidatesAsync(
      string sourceServerKey,
      long afterTenantDatabaseId,
      int take,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseRestoreVerificationDueCandidate>>(
        candidates.Where(candidate => candidate.TenantDatabaseId > afterTenantDatabaseId)
          .OrderBy(candidate => candidate.TenantDatabaseId)
          .Take(take)
          .ToArray());

    public Task<TenantDatabaseDurableRecoveryEvidence?> FindDurableRecoveryEvidenceAsync(
      long tenantDatabaseId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantDatabaseDurableRecoveryEvidence?>(null);
  }

  private sealed class NoOpReconciler : ITenantDatabaseRestoreVerificationReconciler
  {
    public Task<TenantDatabaseRestoreVerificationReconciliationSummary> ReconcileAsync(
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new TenantDatabaseRestoreVerificationReconciliationSummary(
        DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, 0, 0));
  }

  private sealed class NoOpReadinessRefresher : ITenantDatabaseRecoveryReadinessRefresher
  {
    public Task RefreshAsync(long tenantDatabaseId, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class SqlPersistenceOverlapProbe(int expectedWorkers)
  {
    private int arrived;
    private int completed;
    private readonly TaskCompletionSource<bool> allArrived =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConcurrentBag<int> ContextIds { get; } = [];
    public int CompletedPersistenceOperations => Volatile.Read(ref completed);

    public async Task ArriveAsync(PlatformDbContext dbContext)
    {
      ContextIds.Add(RuntimeHelpers.GetHashCode(dbContext));
      if (Interlocked.Increment(ref arrived) == expectedWorkers)
      {
        allArrived.TrySetResult(true);
      }
      await allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public void Complete() => Interlocked.Increment(ref completed);
  }

  private sealed class SqlPersistenceOverlapExecutor(
    PlatformDbContext dbContext,
    ITenantDatabaseRestoreVerificationRunStore runStore,
    SqlPersistenceOverlapProbe probe) : ITenantDatabaseRestoreVerificationExecutor
  {
    public async Task<SSAS.BuildingBlocks.Domain.Result<TenantDatabaseRestoreVerificationExecutionOutcome>>
      ExecuteAsync(
        long tenantDatabaseId,
        long expectedVerificationRunId,
        TenantDatabaseRestoreDepth requestedDepth,
        CancellationToken cancellationToken = default)
    {
      await probe.ArriveAsync(dbContext);
      await dbContext.Database.ExecuteSqlRawAsync("WAITFOR DELAY '00:00:01'", cancellationToken);
      probe.Complete();
      var terminal = await runStore.MarkInfrastructureUnavailableAsync(
        expectedVerificationRunId, "scope-overlap-proof", "test", cancellationToken);
      Assert.True(terminal.IsSuccess);
      return SSAS.BuildingBlocks.Domain.Result.Success(
        new TenantDatabaseRestoreVerificationExecutionOutcome(
          tenantDatabaseId,
          expectedVerificationRunId,
          TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable,
          AchievedDepth: null,
          RestoreVerified: false,
          SafeErrorSummary: "scope-overlap-proof"));
    }
  }

  private sealed class SchedulerClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }

  private sealed class SchedulerUser : ICurrentUser
  {
    public string? UserId => "scope-overlap-test";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class SchedulerTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }
}
