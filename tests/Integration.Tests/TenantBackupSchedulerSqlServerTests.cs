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
[Collection(TenantBackupSerialSuites.Name)]
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

    using var competing = fixture.StartCompetingBackup();
    try
    {
      Assert.True(await fixture.WaitForCompetingBackupAsync(TimeSpan.FromSeconds(30)),
        "the competing backup never became visible, so the in-flight path was not exercised");

      TenantDatabaseBackupRunStatus? observed = null;
      for (var attempt = 0; attempt < 5 && observed is not TenantDatabaseBackupRunStatus.SkippedInFlightOperation; attempt++)
      {
        Assert.True(await fixture.WaitForCompetingBackupAsync(TimeSpan.FromSeconds(30)),
          "the competing backup stopped before the sweep could observe it");

        await Scheduler(fixture).RunSweepAsync();

        await using var platform = fixture.PlatformContext();
        observed = await platform.TenantDatabaseBackupRuns.AsNoTracking()
          .Where(run => run.TenantDatabaseId == id)
          .OrderByDescending(run => run.Id)
          .Select(run => (TenantDatabaseBackupRunStatus?)run.Status)
          .FirstOrDefaultAsync();
      }

      Assert.Equal(TenantDatabaseBackupRunStatus.SkippedInFlightOperation, observed);
    }
    finally
    {
      TenantBackupProviderSqlServerTests.BackupFixture.KillProcess(competing);
    }
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
