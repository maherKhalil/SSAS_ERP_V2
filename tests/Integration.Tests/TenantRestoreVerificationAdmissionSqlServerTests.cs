using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
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

  // Once an operation reaches a terminal state it no longer occupies the slot, so the NEXT due verification
  // can be admitted. The filter is what makes the index an admission control rather than a permanent lock.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_completed_verification_frees_the_slot_for_the_next_one()
  {
    await using var fixture = await VerificationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync("SSAS_Verify_Admission_D");
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    var firstId = (await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId))).Value;
    var store = fixture.RunStore();
    Assert.True((await store.BeginRestoreAsync(
      firstId, TenantDatabaseVerificationNaming.ForRun(databaseId, firstId), "test")).IsSuccess);
    Assert.True((await store.MarkSucceededAsync(firstId, "test")).IsSuccess);

    var secondResult = await fixture.RunStore().TryAdmitAsync(Request(databaseId, baselineId));

    Assert.True(secondResult.IsSuccess);
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
      "UPDATE [platform].[TenantDatabaseRestoreVerificationRuns] SET [Status] = N'Restoring' " +
      $"WHERE [TenantDatabaseRestoreVerificationRunId] = {runId}"));

    Assert.Contains("CK_TenantDatabaseRestoreVerificationRuns_RestoringHasDatabaseName",
      violation.Message, StringComparison.Ordinal);
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

  private static TenantDatabaseRestoreVerificationAdmissionRequest Request(
    long tenantDatabaseId,
    long baselineBackupRunId) =>
    new(tenantDatabaseId, baselineBackupRunId, TenantDatabaseRestoreDepth.Full, "verify", "test");

  private sealed class VerificationFixture : IAsyncDisposable
  {
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private VerificationFixture(string platformCatalog) => PlatformCatalog = platformCatalog;

    private string PlatformCatalog { get; }

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
    public async Task<long> RecordSuccessfulFullBackupAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      var run = TenantDatabaseBackupRun.Start(
        tenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull(), "primary", "verify-tests", Now).Value;
      platform.TenantDatabaseBackupRuns.Add(run);
      await platform.SaveChangesAsync();

      run.Succeed("evidence-identity", "artifact.bak", 1024, null, null, null, null, "verify-tests", Now);
      await platform.SaveChangesAsync();
      return run.Id;
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
}
