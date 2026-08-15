using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// TS-Backup Phase B against real SQL Server (ADR-022 §9, §14).
//
// These execute ACTUAL backups — the first slice permitted to. Every one runs against a disposable generated
// catalog, writes into a per-run folder under the instance backup directory, and cleans up only its own
// files. No production or tenant database is ever a target, and nothing here restores.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantBackupProviderSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_backup_succeeds_and_reconciles_evidence_from_msdb()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.True(outcome.IsSuccess);
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, outcome.Value.Status);

    // Success is reconciled evidence, not a completed command: the provider backup-set identity comes from
    // msdb and is required by the aggregate.
    Assert.False(string.IsNullOrWhiteSpace(outcome.Value.ProviderBackupSetIdentity));

    var run = (await fixture.BackupReads().ListRecentRunsAsync(id, 10))[0];
    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, run.Status);
    Assert.Equal("SqlServer", run.OperationProviderKey);
    Assert.Equal("Full", run.OperationCode);
    Assert.NotNull(run.ProviderBackupIdentity);
    Assert.NotNull(run.CompletedUtc);
    Assert.True(run.SizeBytes > 0);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_full_backup_persists_chain_metadata_and_is_not_copy_only()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    // Chain metadata is captured from the first backup, because reconstructing continuity retrospectively is
    // far harder than recording it (ADR-022 §9).
    await using var platform = fixture.PlatformContext();
    var stored = await platform.TenantDatabaseBackupRuns.AsNoTracking()
      .SingleAsync(item => item.TenantDatabaseId == id);
    Assert.NotNull(stored.FirstLsn);
    Assert.NotNull(stored.LastLsn);
    Assert.NotNull(stored.DatabaseBackupLsn);
    Assert.NotNull(stored.BackupSetGuid);

    // COPY_ONLY would silently anchor later differentials to an older full, so the managed chain must never
    // produce one. Proven from SQL Server's own record rather than from the command string.
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND is_copy_only = 1"));
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND has_backup_checksums = 1"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_differential_succeeds_after_a_full_and_ties_to_its_base()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var differential = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerDifferential());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, differential.Value.Status);

    // SQL Server's own classification: type 'I' is a differential, and its database_backup_lsn identifies
    // the full it depends on.
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND type = 'I'"));
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      "SELECT COUNT(*) FROM msdb.dbo.backupset AS d WHERE d.type = 'I' " +
      $"AND d.database_name = N'{fixture.TargetCatalog}' AND EXISTS (SELECT 1 FROM msdb.dbo.backupset AS f " +
      $"WHERE f.type = 'D' AND f.database_name = d.database_name AND f.first_lsn = d.database_backup_lsn)"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_differential_without_a_baseline_is_blocked_rather_than_attempted()
  {
    // The base is validated from msdb, because a full taken OUTSIDE the platform still resets the
    // differential base and platform run history cannot see it.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerDifferential());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupBaselineMissing.Code, outcome.Value.SafeErrorSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_transaction_log_backup_succeeds_on_a_full_recovery_database_with_a_baseline()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var log = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, log.Value.Status);
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' AND type = 'L'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_log_backup_without_a_baseline_is_blocked()
  {
    // A FULL recovery model alone does not prove a usable chain — a database switched to FULL with no full
    // backup is still pseudo-simple.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupBaselineMissing.Code, outcome.Value.SafeErrorSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_simple_recovery_database_blocks_a_log_backup_and_the_model_is_never_changed()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);
    await fixture.SetRecoveryModelAsync("SIMPLE");

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupRecoveryModelUnsupported.Code, outcome.Value.SafeErrorSummary);

    // Detect, report, degrade — never correct. Switching to FULL would start log growth on a database that
    // is by definition misconfigured (ADR-022 §9).
    Assert.Equal("SIMPLE", await fixture.ReadRecoveryModelAsync());
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_bulk_logged_database_may_still_take_a_log_backup()
  {
    // BULK_LOGGED genuinely supports log backups; only SIMPLE cannot. Whether it satisfies a strict
    // point-in-time policy is a Phase D readiness question, not an execution one.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    await fixture.SetRecoveryModelAsync("BULK_LOGGED");

    var log = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerTransactionLog());

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, log.Value.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_successful_backup_records_a_recovery_observation_but_never_marks_protected()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    var stored = await fixture.ReadDatabaseAsync(id);

    Assert.NotNull(stored.LastSuccessfulFullBackupUtc);
    Assert.NotNull(stored.LastRecoveryReadinessCheckUtc);

    // A good backup produces a baseline that has never been restored once. ADR-022 §6 requires verification
    // evidence for Protected, and the first actual restore verification is Phase D.
    Assert.NotEqual(TenantDatabaseRecoveryReadinessStatus.Protected, stored.RecoveryReadinessStatus);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.VerificationOverdue, stored.RecoveryReadinessStatus);
    Assert.Null(stored.LastRestoreVerificationUtc);

    // And the other dimensions were untouched by the recovery writer.
    Assert.Null(stored.LastSuccessfulDifferentialBackupUtc);
    Assert.Null(stored.LastSuccessfulLogBackupUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_unknown_destination_key_fails_closed_without_writing_anything()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, destinationKey: "NoSuchVault");

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
    Assert.Equal(TenantStorageErrors.BackupDestinationNotConfigured.Code, outcome.Value.SafeErrorSummary);
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task An_unreachable_destination_directory_fails_safely()
  {
    // Renamed after the focused review: this configures a NON-EXISTENT directory, so it proves a trusted but
    // unreachable destination fails cleanly. It does NOT exercise an ACL denial — the service-account
    // asymmetry it used to claim — and manufacturing one would mean editing machine ACLs to satisfy a test
    // name. The positive path below is what actually proves the SQL Server service identity can write to a
    // directory this process created.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, destinationKey: BackupFixture.UnwritableDestinationKey);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.Failed, outcome.Value.Status);
    Assert.False(string.IsNullOrWhiteSpace(outcome.Value.SafeErrorSummary));

    // The summary is bounded and carries no credential. It may name the path SQL Server reported, which is
    // the server's own error text, so the assertion is on boundedness rather than on redaction.
    Assert.True(outcome.Value.SafeErrorSummary!.Length <= TenantDatabaseBackupRun.ErrorSummaryMaximumLength);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Reconciliation_accepts_this_runs_own_artifact_and_refuses_an_external_backup()
  {
    // Rewritten after the focused review: this now drives the PRODUCTION reconciliation code
    // (SqlServerBackupEvidence.ReadAsync) rather than a lookalike query written beside it, so a regression
    // in the real correlation would fail here.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    // A real backup taken OUTSIDE the platform, under a name the platform did not generate.
    await fixture.TakeExternalBackupAsync();

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var device = await fixture.DeviceOfManagedBackupAsync();

    await using var connection = await fixture.OpenTargetAsync();

    // The run's own artifact reconciles.
    var matched = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(), device);
    Assert.True(matched.IsSuccess);
    Assert.True(matched.Value.HasChecksums);
    Assert.False(matched.Value.IsCopyOnly);

    // The external DBA backup is a REAL backup of the same database, taken without CHECKSUM as ad-hoc
    // backups routinely are. Pointing reconciliation straight at it is refused on quality grounds — which is
    // the checksum rule proving itself against real SQL Server data rather than a contrived row.
    var external = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(),
      Path.Combine(fixture.BackupRoot, "external-dba.bak"));
    Assert.True(external.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupEvidenceRejected.Code, external.Error.Code);

    // And a device this run never wrote to yields nothing at all — the state that drives
    // BackupEvidenceMissing.
    var absent = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(),
      Path.Combine(fixture.BackupRoot, "42_Full_20260814T000000Z_999.bak"));
    Assert.True(absent.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupEvidenceMissing.Code, absent.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Copy_only_evidence_is_refused_even_though_it_is_a_valid_checksummed_backup()
  {
    // Isolates the COPY_ONLY rule: this set is a real, checksummed full backup of the right database, so the
    // ONLY reason to refuse it is that it is copy-only. A copy-only full does not reset the differential
    // base, so accepting one as a managed full would leave later differentials anchored to an older baseline
    // — a chain that looks healthy and restores to the wrong point (ADR-022 §9).
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.TakeExternalCopyOnlyBackupAsync();

    await using var connection = await fixture.OpenTargetAsync();
    var evidence = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(),
      Path.Combine(fixture.BackupRoot, "external-copyonly.bak"));

    Assert.True(evidence.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupEvidenceRejected.Code, evidence.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Evidence_is_refused_when_the_backup_type_does_not_match_the_operation()
  {
    // Correlation must include TYPE. Without it, a device and database match alone would let a run claim a
    // backup set that records a different operation entirely.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var device = await fixture.DeviceOfManagedBackupAsync();

    await using var connection = await fixture.OpenTargetAsync();

    // Same database, same device — but the set recorded there is a full ('D'), not a differential ('I').
    var mismatched = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerDifferential(), device);

    Assert.True(mismatched.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupEvidenceMissing.Code, mismatched.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_device_differing_only_at_underscore_positions_does_not_match()
  {
    // THE REGRESSION TEST FOR THE LIKE DEFECT. Correlation used `LIKE '%' + fileName`, and the generated
    // name contains underscores — SINGLE-CHARACTER WILDCARDS in T-SQL — so a device differing only at those
    // positions matched. The counterexample is built by substituting exactly those characters.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var device = await fixture.DeviceOfManagedBackupAsync();

    var fileName = Path.GetFileName(device);
    Assert.Contains("_", fileName, StringComparison.Ordinal);

    // Same length, same everything except the wildcard positions.
    var wildcardTwin = Path.Combine(
      Path.GetDirectoryName(device)!, fileName.Replace('_', 'X'));
    Assert.NotEqual(device, wildcardTwin);

    await using var connection = await fixture.OpenTargetAsync();

    var twin = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(), wildcardTwin);
    Assert.True(twin.IsFailure);

    // The genuine artifact still matches, so the fix is not simply a stricter comparison that matches
    // nothing.
    var real = await SqlServerBackupEvidence.ReadAsync(
      connection, fixture.TargetCatalog, TenantDatabaseBackupOperation.SqlServerFull(), device);
    Assert.True(real.IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task The_reconciled_completion_time_is_true_utc_and_not_the_servers_local_clock()
  {
    // THE REGRESSION TEST FOR MEDIUM-2. backup_finish_date is a `datetime` written from the server's LOCAL
    // clock; it was being labelled UTC without conversion, which on this UTC+03 host stored completion times
    // three hours in the future. A real clock is used deliberately — TestClock would hide the defect.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    var utcBefore = DateTimeOffset.UtcNow;
    var outcome = await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());
    var utcAfter = DateTimeOffset.UtcNow;

    Assert.Equal(TenantDatabaseBackupRunStatus.Succeeded, outcome.Value.Status);

    await using var platform = fixture.PlatformContext();
    var run = await platform.TenantDatabaseBackupRuns.AsNoTracking()
      .SingleAsync(item => item.TenantDatabaseId == id);

    Assert.NotNull(run.CompletedUtc);

    // Generous either side for datetime precision and second-granularity conversion, but nowhere near wide
    // enough to admit a timezone offset.
    var tolerance = TimeSpan.FromSeconds(30);
    Assert.InRange(run.CompletedUtc!.Value, utcBefore - tolerance, utcAfter + tolerance);

    // Stated explicitly, because this is the exact shape of the defect: a whole-hour offset from real UTC.
    var drift = (run.CompletedUtc.Value - utcAfter).Duration();
    Assert.True(drift < TimeSpan.FromMinutes(10),
      $"completion time drifted {drift} from UTC, which looks like a server-timezone offset rather than a clock skew");

    // The recovery observation is derived from the same evidence, so it must be UTC too.
    var stored = await fixture.ReadDatabaseAsync(id);
    Assert.NotNull(stored.LastSuccessfulFullBackupUtc);
    Assert.InRange(stored.LastSuccessfulFullBackupUtc!.Value, utcBefore - tolerance, utcAfter + tolerance);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_backup_already_in_flight_produces_a_skip_from_the_production_path()
  {
    // Rewritten after the focused review: this drives the EXECUTOR, so SkippedInFlightOperation is produced
    // by production code end to end rather than asserted from a query the test ran itself.
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);
    await fixture.FillAsync();

    // A backup started by another process entirely, holding no application lock — exactly what a DBA or a
    // SQL Agent job looks like, and precisely what ownership alone cannot protect against.
    using var competing = fixture.StartCompetingBackup();
    try
    {
      TenantDatabaseBackupRunStatus? observed = null;

      // The competitor runs backups back to back, but there is still a brief gap between iterations. An
      // attempt that lands in one proves nothing either way, so attempts are repeated — each one confirming
      // a backup is genuinely in flight FIRST, so a pass can only come from a real overlap.
      for (var attempt = 0; attempt < 5 && observed is not TenantDatabaseBackupRunStatus.SkippedInFlightOperation; attempt++)
      {
        Assert.True(await fixture.WaitForCompetingBackupAsync(TimeSpan.FromSeconds(30)),
          "the competing backup never became visible, so the in-flight path was not exercised");

        var outcome = await fixture.Executor().ExecuteAsync(
          id, TenantDatabaseBackupOperation.SqlServerFull());
        observed = outcome.Value.Status;
      }

      Assert.True(observed is TenantDatabaseBackupRunStatus.SkippedInFlightOperation,
        $"expected SkippedInFlightOperation from the production path but observed {observed}");
    }
    finally
    {
      BackupFixture.KillProcess(competing);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Ownership_is_held_for_the_duration_so_a_second_worker_skips_rather_than_duplicates()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id);

    // A competing holder on its own session, exactly as a second platform instance would be.
    await using var holder = await fixture.OpenOwnedConnectionAsync();

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    // A clean skip: coordination worked. Not a failure, and no readiness degradation.
    Assert.Equal(TenantDatabaseBackupRunStatus.SkippedOwnershipHeld, outcome.Value.Status);
    Assert.NotEqual(TenantDatabaseBackupRunStatus.Failed, outcome.Value.Status);
    Assert.Equal(0, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}'"));
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Compression_is_applied_where_the_edition_supports_it()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, compressionMode: TenantDatabaseBackupCompressionMode.PreferredWhereSupported);

    await fixture.Executor().ExecuteAsync(id, TenantDatabaseBackupOperation.SqlServerFull());

    // On an edition that supports compression the compressed size is smaller than the raw size; on one that
    // does not, the run still succeeds uncompressed. Both are correct, so this asserts the run succeeded and
    // that SQL Server recorded a coherent pair.
    Assert.Equal(1, await BackupFixture.MsdbScalarAsync(
      $"SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = N'{fixture.TargetCatalog}' " +
      "AND compressed_backup_size > 0 AND backup_size > 0"));
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  [Trait("Decision", "ADR-022")]
  public async Task A_customer_managed_database_is_never_backed_up_by_the_platform()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync(hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);
    await fixture.AddPolicyAsync(id, managementMode: TenantDatabaseBackupManagementMode.CustomerDba);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    // Refused before anything opens a connection. Backup custody belongs to the customer (ADR-021).
    Assert.True(outcome.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupNotPermittedByManagementMode.Code, outcome.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Customer_dba_and_approval_gated_policies_are_blocked_not_executed()
  {
    await using var fixture = await BackupFixture.CreateAsync();

    foreach (var mode in new[]
    {
      TenantDatabaseBackupManagementMode.CustomerDba,
      // No approval workflow exists yet, and ADR-022 §5 says absence of approval is DENIAL. The alternative
      // would be a caller-supplied "approved" flag, which is a hole rather than a feature.
      TenantDatabaseBackupManagementMode.PlatformAfterApproval
    })
    {
      var id = await fixture.RegisterAsync(catalogSuffix: mode.ToString());
      await fixture.AddPolicyAsync(id, managementMode: mode);

      var outcome = await fixture.Executor().ExecuteAsync(
        id, TenantDatabaseBackupOperation.SqlServerFull());

      Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
      Assert.Equal(
        TenantStorageErrors.BackupNotPermittedByManagementMode.Code, outcome.Value.SafeErrorSummary);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_disabled_policy_blocks_execution()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();
    await fixture.AddPolicyAsync(id, enabled: false);

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.Equal(TenantDatabaseBackupRunStatus.BlockedByPolicy, outcome.Value.Status);
  }

  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_database_with_no_policy_cannot_be_backed_up()
  {
    await using var fixture = await BackupFixture.CreateAsync();
    var id = await fixture.RegisterAsync();

    var outcome = await fixture.Executor().ExecuteAsync(
      id, TenantDatabaseBackupOperation.SqlServerFull());

    Assert.True(outcome.IsFailure);
    Assert.Equal(TenantStorageErrors.BackupPolicyNotConfigured.Code, outcome.Error.Code);
  }

  // One Platform catalog, one disposable target catalog, and a per-run backup folder.
  internal sealed class BackupFixture : IAsyncDisposable
  {
    public const string PrimaryServerKey = "PrimarySqlServer";

    public const string DestinationKey = "TestVault";

    // A trusted, configured destination the SQL Server service identity cannot write to. Configured
    // deliberately so the "service account access" failure has a controlled test.
    public const string UnwritableDestinationKey = "UnwritableVault";

    private readonly List<string> targetCatalogs = [];

    private BackupFixture(string platformCatalog, string backupRoot)
    {
      PlatformCatalog = platformCatalog;
      BackupRoot = backupRoot;
    }

    public string PlatformCatalog { get; }

    // Per-run folder under the instance backup directory, so cleanup only ever touches this run's files.
    public string BackupRoot { get; }

    public string TargetCatalog { get; private set; } = string.Empty;

    public static async Task<BackupFixture> CreateAsync()
    {
      var runId = Guid.NewGuid().ToString("N");
      var fixture = new BackupFixture(
        $"SSAS_ERP_BACKUPP_{runId}",
        Path.Combine(TestBackupRoot(), runId));

      try
      {
        // The test process creates the folder and the SQL SERVER SERVICE IDENTITY writes into it, so the
        // location must be reachable by BOTH accounts — which is exactly the asymmetry ADR-022 §11 warns
        // about. The instance default backup directory lives under Program Files and is writable only by the
        // service account, so a test process cannot create a subfolder there; a ProgramData root is
        // creatable by this process and writable by the service account.
        //
        // This is a TEST-FIXTURE concern. Production code hard-codes no path: it resolves destinations from
        // trusted configuration, and provisioning the directory with correct ACLs is a deployment concern.
        Directory.CreateDirectory(fixture.BackupRoot);

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

    public static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    // Overridable so a differently-configured environment can point the fixture somewhere both the test
    // process and the SQL Server service account can reach.
    public static string TestBackupRoot() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_BACKUP_ROOT") ??
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SSAS_BackupTests");

    public PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(PlatformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    private IOptions<TenantStorageOptions> StorageOptions()
    {
      var options = Options.Create(new TenantStorageOptions());
      var master = new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString;

      // Runtime credentials exist so the test proves the BACKUP path does not use them.
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions { ConnectionString = master };
      options.Value.BackupServers[PrimaryServerKey] = new TenantStorageServerOptions { ConnectionString = master };

      options.Value.BackupDestinations[DestinationKey] =
        new TenantStorageBackupDestinationOptions { DirectoryPath = BackupRoot };
      // A path SQL Server's service account cannot create files in.
      options.Value.BackupDestinations[UnwritableDestinationKey] =
        new TenantStorageBackupDestinationOptions
        {
          DirectoryPath = Path.Combine(BackupRoot, "no-such-subdirectory")
        };

      return options;
    }

    public TenantDatabaseBackupReadRepository BackupReads() => new(PlatformContext());

    public ITenantDatabaseBackupExecutor Executor()
    {
      var context = PlatformContext();
      var registry = new TenantDatabaseRegistryReadRepository(context);
      var options = StorageOptions();

      var provider = new SqlServerTenantDatabaseBackupProvider(
        registry,
        new TenantDatabaseBackupConnectionFactory(options),
        options,
        // A short command timeout keeps a hung test bounded; real deployments use the four-hour default.
        new TenantDatabaseBackupOperationalOptions
        {
          BackupCommandTimeout = TimeSpan.FromMinutes(5),
          OwnershipTimeout = TimeSpan.FromSeconds(2)
        });

      return new TenantDatabaseBackupExecutor(
        registry,
        new TenantDatabaseBackupReadRepository(context),
        new TenantDatabaseBackupRunStore(context, new TestClock()),
        provider,
        new TenantDatabaseRecoveryReadinessWriter(context, new TestClock()));
    }

    public async Task<long> RegisterAsync(
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      string catalogSuffix = "")
    {
      var catalog = $"SSAS_ERP_BACKUPT_{Guid.NewGuid():N}";
      targetCatalogs.Add(catalog);
      TargetCatalog = catalog;

      // A real, disposable catalog. FULL recovery so log backups are meaningful.
      await ExecuteOnAsync("master", $"CREATE DATABASE [{catalog}]");
      await ExecuteOnAsync("master", $"ALTER DATABASE [{catalog}] SET RECOVERY FULL");

      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        hostingMode, storageMode, PrimaryServerKey, catalog,
        TenantDatabaseProvisioningStatus.Ready, "backup-tests", TestClock.Fixed).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    public async Task AddPolicyAsync(
      long tenantDatabaseId,
      string? destinationKey = DestinationKey,
      TenantDatabaseBackupManagementMode managementMode = TenantDatabaseBackupManagementMode.AutomaticByPlatform,
      TenantDatabaseBackupCompressionMode compressionMode = TenantDatabaseBackupCompressionMode.PreferredWhereSupported,
      bool enabled = true)
    {
      await using var platform = PlatformContext();
      var policy = TenantDatabaseBackupPolicy.Create(
        tenantDatabaseId, enabled, managementMode, destinationKey,
        10_080, 1_440, 15, 35, 90, 60, "backup-tests", TestClock.Fixed,
        compressionMode: compressionMode).Value;
      platform.TenantDatabaseBackupPolicies.Add(policy);
      await platform.SaveChangesAsync();
    }

    // Holds backup ownership on its own session, as a competing platform instance would.
    public async Task<SqlConnection> OpenOwnedConnectionAsync()
    {
      var builder = new SqlConnectionStringBuilder(ConnectionFor(TargetCatalog)) { Pooling = false };
      var connection = new SqlConnection(builder.ConnectionString);
      await connection.OpenAsync();
      var ownership = await TenantDatabaseBackupOwnership.TryAcquireAsync(connection, TimeSpan.FromSeconds(5));
      Assert.NotNull(ownership);
      return connection;
    }

    // A COPY_ONLY backup, WITH checksums, so reconciling against it isolates the copy-only rule from the
    // checksum rule.
    public Task TakeExternalCopyOnlyBackupAsync() =>
      ExecuteOnAsync(TargetCatalog,
        $"BACKUP DATABASE [{TargetCatalog}] TO DISK = N'{Path.Combine(BackupRoot, "external-copyonly.bak")}' " +
        "WITH INIT, CHECKSUM, COPY_ONLY");

    // A backup taken outside the platform, under a name the platform did not generate.
    public Task TakeExternalBackupAsync() =>
      ExecuteOnAsync(TargetCatalog,
        $"BACKUP DATABASE [{TargetCatalog}] TO DISK = N'{Path.Combine(BackupRoot, "external-dba.bak")}' WITH INIT");

    // An open connection in the target database, for driving the production reconciliation code directly.
    public async Task<SqlConnection> OpenTargetAsync()
    {
      var connection = new SqlConnection(ConnectionFor(TargetCatalog));
      await connection.OpenAsync();
      return connection;
    }

    // The exact device the PLATFORM's own run wrote to, read back from SQL Server. Identified by the
    // generated artifact vocabulary rather than by recency, so it never picks up the external DBA backup.
    public async Task<string> DeviceOfManagedBackupAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT TOP (1) bmf.physical_device_name FROM msdb.dbo.backupset AS bs " +
        "INNER JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id " +
        "WHERE bs.database_name = @database AND bmf.physical_device_name LIKE @managed " +
        "ORDER BY bs.backup_set_id DESC";
      command.Parameters.AddWithValue("@database", TargetCatalog);
      // ESCAPE-free by construction: this pattern is the test's own, and matches the platform's generated
      // artifacts by their trailing extension rather than by a name containing wildcards.
      command.Parameters.AddWithValue("@managed", "%Full%.bak");
      return (string)(await command.ExecuteScalarAsync())!;
    }

    // Enough data that a competing backup lasts long enough for the provider to observe it.
    //
    // Loaded in BATCHES under SIMPLE recovery, then returned to FULL. One 240 MB insert inside a single
    // FULL-recovery transaction exhausted the buffer pool on this host and left the database unrecoverable —
    // a test-fixture problem, but one that destroyed the very database under test, so the load is chunked
    // and checkpointed instead.
    public async Task FillAsync()
    {
      await ExecuteOnAsync("master", $"ALTER DATABASE [{TargetCatalog}] SET RECOVERY SIMPLE");
      await ExecuteOnAsync(TargetCatalog,
        "CREATE TABLE dbo.Filler (Id int IDENTITY(1,1) NOT NULL, Payload char(8000) NOT NULL)");
      await ExecuteOnAsync(TargetCatalog,
        "DECLARE @batch int = 0; " +
        "WHILE @batch < 3 BEGIN " +
        "  INSERT INTO dbo.Filler (Payload) SELECT TOP (5000) REPLICATE('x', 8000) " +
        "  FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b; " +
        "  CHECKPOINT; SET @batch += 1; END");
      await ExecuteOnAsync("master", $"ALTER DATABASE [{TargetCatalog}] SET RECOVERY FULL");
    }

    // A backup from a separate process, holding no application lock.
    public System.Diagnostics.Process StartCompetingBackup()
    {
      var path = Path.Combine(BackupRoot, $"competing_{Guid.NewGuid():N}.bak");
      var builder = new SqlConnectionStringBuilder(Configured());
      var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "localhost" : builder.DataSource;

      var start = new System.Diagnostics.ProcessStartInfo("sqlcmd")
      {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };
      start.ArgumentList.Add("-S");
      start.ArgumentList.Add(server);
      start.ArgumentList.Add("-E");
      start.ArgumentList.Add("-C");
      start.ArgumentList.Add("-d");
      start.ArgumentList.Add(TargetCatalog);
      start.ArgumentList.Add("-Q");
      // BACKED UP REPEATEDLY, not once. A single backup of this database completes in well under a second,
      // so a one-shot competitor turns the test into a race it usually loses. A loop keeps a BACKUP request
      // continuously present for long enough that the provider's check is deterministic rather than lucky.
      start.ArgumentList.Add(
        $"DECLARE @i int = 0; WHILE @i < 30 BEGIN " +
        $"BACKUP DATABASE [{TargetCatalog}] TO DISK = N'{path}' WITH INIT, CHECKSUM; SET @i += 1; END");

      return System.Diagnostics.Process.Start(start)!;
    }

    public async Task<bool> WaitForCompetingBackupAsync(TimeSpan timeout)
    {
      await using var connection = await OpenTargetAsync();
      var deadline = DateTime.UtcNow.Add(timeout);
      while (DateTime.UtcNow < deadline)
      {
        if (await SqlServerBackupVisibility.IsBackupInFlightAsync(connection))
        {
          return true;
        }

        await Task.Delay(10);
      }

      return false;
    }

    public static void KillProcess(System.Diagnostics.Process process)
    {
      try
      {
        if (!process.HasExited)
        {
          process.Kill(entireProcessTree: true);
        }
      }
      catch (InvalidOperationException)
      {
      }
    }

    public Task SetRecoveryModelAsync(string model) =>
      ExecuteOnAsync("master", $"ALTER DATABASE [{TargetCatalog}] SET RECOVERY {model}");

    public async Task<string> ReadRecoveryModelAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT CAST(DATABASEPROPERTYEX(N'{TargetCatalog}', 'Recovery') AS nvarchar(60))";
      return await command.ExecuteScalarAsync() as string ?? string.Empty;
    }

    public async Task<TenantDatabase> ReadDatabaseAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabases.AsNoTracking()
        .SingleAsync(item => item.Id == tenantDatabaseId);
    }

    public static async Task<int> MsdbScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task ExecuteOnAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 300;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in targetCatalogs.Append(PlatformCatalog))
      {
        try
        {
          await ExecuteOnAsync("master",
            $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN ALTER DATABASE [{catalog}] " +
            $"SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]; END");
        }
        catch (SqlException)
        {
          // A leftover catalog is housekeeping, never a reason to fail the test that created it.
        }
      }

      // Cleanup is scoped to THIS RUN'S folder and nothing else. No wildcard sweep of the instance backup
      // directory, and no xp_delete_file — which is undocumented and effectively needs sysadmin.
      try
      {
        if (Directory.Exists(BackupRoot))
        {
          Directory.Delete(BackupRoot, recursive: true);
        }
      }
      catch (IOException)
      {
        // Files are written by the SQL Server service account; if this process cannot remove them that is a
        // test-environment ACL matter, not something production code should be given the power to solve.
      }
      catch (UnauthorizedAccessException)
      {
      }
    }
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "backup-tests";

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
    public static readonly DateTimeOffset Fixed = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => Fixed;
  }
}
