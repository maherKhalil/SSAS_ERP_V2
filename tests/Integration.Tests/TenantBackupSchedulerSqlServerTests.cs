using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// Phase C against real SQL Server (ADR-022 §13).
//
// Deliberately few. The scheduler's selection, paging, bounding and isolation rules are proven exhaustively
// against a fake executor in Platform.Tests, where they are deterministic and cost nothing. What can only be
// established here is the INTERACTION: that a sweep drives the real executor and provider end to end, and
// that Phase B's ownership and in-flight guards behave as the fleet layer assumes when two sweeps or an
// outside backup collide.
// LEFT THE SERIAL COLLECTION on 2026-08-23 (gate-economics round 2).
// Its msdb reads ARE instance-wide table reads, and every one of them is PREDICATED ON ITS OWN
// Guid-named TargetCatalog — re-read line by line on 2026-08-23 before removal. The count is
// `WHERE database_name = N'{TargetCatalog}'`; the overlap self-join carries
// `a.database_name = b.database_name` AND `WHERE a.database_name = N'{TargetCatalog}'`, so BOTH sides
// are pinned. A concurrent backup of any other catalog cannot satisfy either predicate.
//
// Reading an instance-wide table is not sharing it. Sharing would be reading it UNPREDICATED, and this
// class does not.
public sealed class TenantBackupSchedulerSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_sweep_backs_up_every_due_database_on_one_server()
  {
    // Two databases behind one ServerKey with a per-server cap of one: they run in sequence, and both end
    // up genuinely backed up. This is the end-to-end path — fleet query, due evaluation, executor, provider,
    // msdb reconciliation — with nothing faked.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();

    var first = await fixture.RegisterAsync(catalogSuffix: "one");
    await fixture.AddPolicyAsync(first);
    var second = await fixture.RegisterAsync(catalogSuffix: "two");
    await fixture.AddPolicyAsync(second);

    var summary = await Scheduler(fixture, maxConcurrent: 2, maxPerServer: 1).RunSweepAsync();

    Assert.Equal(2, summary.Dispatched);
    Assert.Equal(2, summary.Succeeded);

    // Both databases carry a real successful full, and the recovery observation followed it.
    foreach (var id in new[] { first, second })
    {
      var stored = await fixture.ReadDatabaseAsync(id);
      Assert.NotNull(stored.LastSuccessfulFullBackupUtc);
      Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, stored.RecoveryReadinessStatus);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Two_scheduler_instances_selecting_one_database_collapse_to_a_single_backup()
  {
    // THE MULTI-INSTANCE ASSERTION. Phase C deliberately adds no fleet lock, no lease and no leader
    // election: two application instances may both decide the same database is due, and correctness comes
    // from Phase B's session applock plus an ownership-bound revalidation of the decision.
    //
    // This asserts EXACTLY ONE managed backup for one due event. An earlier version asserted only that no
    // two backups overlapped, and it passed while the second instance quietly took a redundant sequential
    // backup — serialised, but still duplicated work nobody asked for.
    //
    // NO FILLER DATA, deliberately. A small database backs up in well under the ownership timeout, which is
    // precisely the race that produced the duplicate: the first worker finishes and releases the lock before
    // the second times out waiting for it. A large database would hide the bug behind a timeout.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();

    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var instanceOne = Scheduler(fixture);
    var instanceTwo = Scheduler(fixture);

    var summaries = await Task.WhenAll(
      instanceOne.RunSweepAsync(),
      instanceTwo.RunSweepAsync());

    await using var platform = fixture.PlatformContext();
    var runs = await platform.TenantDatabaseBackupRuns.AsNoTracking()
      .Where(run => run.TenantDatabaseId == id)
      .ToListAsync();

    // Every outcome is safe: a backup, or a controlled skip. Never a failure caused by the collision.
    Assert.All(runs, run => Assert.True(
      run.Status is TenantDatabaseBackupRunStatus.Succeeded
        or TenantDatabaseBackupRunStatus.SkippedOwnershipHeld
        or TenantDatabaseBackupRunStatus.SkippedInFlightOperation,
      $"unexpected duplicate-execution outcome {run.Status}"));

    // EXACTLY ONE managed backup for one due event, asserted three ways.
    var succeeded = runs.Count(run => run.Status == TenantDatabaseBackupRunStatus.Succeeded);
    Assert.Equal(1, succeeded);

    // The second instance recorded a controlled skip carrying the supersession reason, not a second backup.
    var superseded = runs.Count(run =>
      run.Status == TenantDatabaseBackupRunStatus.SkippedOwnershipHeld &&
      run.ErrorSummary == TenantStorageErrors.BackupSupersededByRecentRun.Code);
    Assert.Equal(1, superseded);

    // And SQL Server's own record agrees: one managed backup set for this database, and no overlap.
    var managedBackupSets = await TenantBackupProviderSqlServerTests.BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'");
    Assert.Equal(1, managedBackupSets);

    var overlapping = await TenantBackupProviderSqlServerTests.BackupFixture.MsdbScalarAsync(
      "SELECT COUNT(*) FROM msdb.dbo.backupset AS a " +
      "INNER JOIN msdb.dbo.backupset AS b ON a.backup_set_id < b.backup_set_id " +
      "AND a.database_name = b.database_name " +
      $"WHERE a.database_name = N'{fixture.TargetCatalog}' " +
      "AND a.backup_start_date < b.backup_finish_date AND b.backup_start_date < a.backup_finish_date");

    Assert.Equal(0, overlapping);

    Assert.Equal(2, summaries.Sum(summary => summary.Dispatched));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_backup_started_outside_the_platform_makes_the_sweep_skip()
  {
    // The case ownership alone can never cover: a DBA or SQL Agent backup takes no platform lock, so only
    // the provider's in-flight inspection sees it. Phase C runs unattended, which is exactly when this
    // matters — and the guard is now unconditional, with no configuration that could switch it off.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();

    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);
    await fixture.FillAsync();

    // ---- ONE SWEEP, ONE ASSERTION. The five-attempt loop that used to stand here was removed on
    // 2026-08-23, and the reason is worth stating because it looked like a reasonable mitigation.
    //
    // IT COULD NOT RETRY. The sweep picks work via `SelectDue`, which anchors on
    // `LastSuccessfulFullBackupUtc`. If attempt 1 misses the competitor, the sweep BACKS THE DATABASE UP —
    // so on attempt 2 the database is no longer due, the sweep does nothing, and the query re-reads the very
    // same `Succeeded` row. `SuppressRecentAttemptsAsync` closes the other door: a recent skip is suppressed
    // by `SkipRetryBackoff` too. Five attempts were one attempt wearing a loop, which is why the recorded
    // failure was the FINAL assertion (`Expected: SkippedInFlightOperation, Actual: Succeeded`) rather than
    // one of the precondition messages.
    //
    // So the precondition was made non-lapsable instead — see `StartCompetingBackup`, which now runs two
    // overlapping processes so a BACKUP request is admitted at every instant. The wait below is a READINESS
    // BARRIER, not a retry: it establishes once that the competitor has started, and fails hard if it never
    // does. Nothing here sleeps, and the assertion is the same equality it always was.
    using var competing = fixture.StartCompetingBackup();

    Assert.True(await fixture.WaitForCompetingBackupAsync(TimeSpan.FromSeconds(30)),
      "the competing backup never became visible, so the in-flight path was not exercised");

    await Scheduler(fixture).RunSweepAsync();

    await using var platform = fixture.PlatformContext();
    var observed = await platform.TenantDatabaseBackupRuns.AsNoTracking()
      .Where(run => run.TenantDatabaseId == id)
      .OrderByDescending(run => run.Id)
      .Select(run => (TenantDatabaseBackupRunStatus?)run.Status)
      .FirstOrDefaultAsync();

    Assert.Equal(TenantDatabaseBackupRunStatus.SkippedInFlightOperation, observed);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Fleet_discovery_pages_by_keyset_and_excludes_ineligible_databases()
  {
    // The repository half, against real SQL: eligibility is filtered server-side, and a shared physical
    // database appears exactly once no matter how the estate is shaped.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();

    var automatic = await fixture.RegisterAsync(catalogSuffix: "auto");
    await fixture.AddPolicyAsync(automatic);

    var customerDba = await fixture.RegisterAsync(catalogSuffix: "dba");
    await fixture.AddPolicyAsync(customerDba, managementMode: TenantDatabaseBackupManagementMode.CustomerDba);

    var disabled = await fixture.RegisterAsync(catalogSuffix: "off");
    await fixture.AddPolicyAsync(disabled, enabled: false);

    // Registered with no policy at all — unprotected, and not a scheduling candidate.
    var unpolicied = await fixture.RegisterAsync(catalogSuffix: "nopolicy");

    await using var context = fixture.PlatformContext();
    var reads = new TenantDatabaseBackupFleetReadRepository(context);

    var candidates = await reads.ListBackupCandidatesAsync(TenantBackupProviderSqlServerTests.BackupFixture.PrimaryServerKey, 0, 100);
    var ids = candidates.Select(candidate => candidate.TenantDatabaseId).ToArray();

    Assert.Contains(automatic, ids);
    Assert.DoesNotContain(customerDba, ids);
    Assert.DoesNotContain(disabled, ids);
    Assert.DoesNotContain(unpolicied, ids);

    // Ascending by id, no duplicates — the property keyset paging depends on.
    Assert.Equal(ids.OrderBy(id => id).ToArray(), ids);
    Assert.Equal(ids.Distinct().Count(), ids.Length);

    // And the cursor genuinely advances: asking past the first row never returns it again.
    var afterFirst = await reads.ListBackupCandidatesAsync(TenantBackupProviderSqlServerTests.BackupFixture.PrimaryServerKey, ids[0], 100);
    Assert.DoesNotContain(ids[0], afterFirst.Select(candidate => candidate.TenantDatabaseId));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Supersession_recognises_platform_artifacts_written_under_either_path_separator()
  {
    // LOW-B. The supersession match anchors the platform's generated file name to a path separator so a
    // directory cannot masquerade as one. Anchoring on a backslash alone meant a destination configured as
    // `C:/backups/` produced a forward slash before the file name, the match missed, and deduplication
    // silently reverted to taking a redundant backup. Separators are normalised before matching.
    //
    // Exercised against real msdb, because the behaviour under test is SQL Server's record of the device
    // name — not a string helper.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var operation = TenantDatabaseBackupOperation.SqlServerFull();
    var before = DateTimeOffset.UtcNow.AddMinutes(-1);

    await using var connection = await fixture.OpenTargetAsync();

    // Nothing yet: a database with no platform backup must never look superseded, or the first backup would
    // never happen.
    Assert.False(await SqlServerBackupEvidence.HasPlatformBackupCompletedSinceAsync(
      connection, fixture.TargetCatalog, operation, id, before));

    // A backup written through a FORWARD-SLASH directory, exactly as a destination configured that way
    // would produce. The file name still carries the platform's generated vocabulary.
    var forwardSlashDevice =
      $"{fixture.BackupRoot.Replace('\\', '/')}/{id}_Full_20260815T120000Z_1.bak";
    await TenantBackupProviderSqlServerTests.BackupFixture.ExecuteOnAsync(
      fixture.TargetCatalog,
      $"BACKUP DATABASE [{fixture.TargetCatalog}] TO DISK = N'{forwardSlashDevice}' WITH INIT, CHECKSUM");

    Assert.True(await SqlServerBackupEvidence.HasPlatformBackupCompletedSinceAsync(
      connection, fixture.TargetCatalog, operation, id, before));

    // A DIFFERENT database identifier must not be satisfied by it — the anchor still discriminates.
    Assert.False(await SqlServerBackupEvidence.HasPlatformBackupCompletedSinceAsync(
      connection, fixture.TargetCatalog, operation, id + 1_000, before));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_external_backup_never_satisfies_the_platform_schedule()
  {
    // The property that must survive every loosening of the match: a DBA, SQL Agent or third-party backup
    // changes SQL Server's chain state without discharging the platform's scheduling obligation. Its file
    // name carries none of the platform's generated vocabulary, so it cannot supersede a due decision.
    await using var fixture = await TenantBackupProviderSqlServerTests.BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var before = DateTimeOffset.UtcNow.AddMinutes(-1);
    await fixture.TakeExternalBackupAsync();

    await using var connection = await fixture.OpenTargetAsync();

    Assert.False(await SqlServerBackupEvidence.HasPlatformBackupCompletedSinceAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(), id, before));
  }

  private static TenantDatabaseBackupScheduler Scheduler(
    TenantBackupProviderSqlServerTests.BackupFixture fixture,
    int maxConcurrent = 2,
    int maxPerServer = 1)
  {
    var context = fixture.PlatformContext();

    return new TenantDatabaseBackupScheduler(
      new TenantDatabaseBackupFleetReadRepository(context),
      fixture.Executor(),
      new SweepClock(),
      Microsoft.Extensions.Options.Options.Create(new TenantDatabaseBackupSchedulerOptions
      {
        Enabled = true,
        BatchSize = 100,
        MaxConcurrentBackups = maxConcurrent,
        MaxConcurrentPerServer = maxPerServer
      }),
      NullLogger<TenantDatabaseBackupScheduler>.Instance);
  }

  // A real clock. Due evaluation here is driven by databases that have never been backed up, so the reading
  // only needs to be monotonic and truthful rather than pinned.
  private sealed class SweepClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
