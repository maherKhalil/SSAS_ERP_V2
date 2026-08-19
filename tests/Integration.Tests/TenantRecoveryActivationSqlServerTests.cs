using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE RECOVERY ACTIVATION EVIDENCE QUERY, against real SQL (TS-Storage Phase E).
//
// The domain matrix is unit-tested; what needs a database is whether the EVIDENCE the gate reads is actually
// the evidence the Platform schema holds. Three things can only be established here: that the exact
// verification identity is projected rather than a timestamp, that a newer full backup genuinely supersedes
// a verified baseline in the run history, and that the whole boundary is keyed on the PHYSICAL database
// rather than on a tenant.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantRecoveryActivationSqlServerTests
{
  // A timestamp says WHEN. Activation needs WHICH — which baseline, at what depth, by which run.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Activation_evidence_projects_the_exact_verification_identity_and_not_only_a_timestamp()
  {
    await using var fixture = await ActivationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync(TenantDatabaseStorageMode.Dedicated);
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    var verificationRunId = await fixture.CompleteVerificationAsync(databaseId, baselineId);
    await fixture.RecordReadinessAsync(databaseId, TenantDatabaseRecoveryReadinessStatus.Protected);

    var evidence = await fixture.ActivationEvidenceAsync(databaseId);

    Assert.NotNull(evidence);
    Assert.Equal(baselineId, evidence!.CurrentBaselineBackupRunId);
    Assert.Equal(verificationRunId, evidence.VerifiedVerificationRunId);
    Assert.Equal(baselineId, evidence.VerifiedSourceBackupRunId);
    Assert.Equal(TenantDatabaseRestoreDepth.Full, evidence.VerifiedDepth);
    Assert.NotNull(evidence.VerificationCompletedUtc);
    Assert.Equal(TenantDatabaseRecoveryReadinessStatus.Protected, evidence.RecoveryReadinessStatus);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, evidence.StorageMode);

    // ...and with that evidence the gate authorises.
    Assert.True((await fixture.Gate().AuthorizeActivationAsync(databaseId)).IsSuccess);
  }

  // THE CASE A TIMESTAMP GATE WOULD PASS. A newer full backup moves the recovery path; the verification is
  // still recent, still successful, and now proves a chain a restore would no longer take.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task A_newer_full_backup_supersedes_the_verified_baseline_and_activation_is_refused()
  {
    await using var fixture = await ActivationFixture.CreateAsync();
    var databaseId = await fixture.RegisterAsync(TenantDatabaseStorageMode.Dedicated);
    var firstBaselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);
    await fixture.CompleteVerificationAsync(databaseId, firstBaselineId);
    await fixture.RecordReadinessAsync(databaseId, TenantDatabaseRecoveryReadinessStatus.Protected);

    var secondBaselineId = await fixture.RecordSuccessfulFullBackupAsync(databaseId);

    var evidence = await fixture.ActivationEvidenceAsync(databaseId);
    Assert.NotNull(evidence);
    Assert.Equal(secondBaselineId, evidence!.CurrentBaselineBackupRunId);
    Assert.Equal(firstBaselineId, evidence.VerifiedSourceBackupRunId);

    // The aggregate timestamp still looks entirely current — which is exactly why it cannot be the gate.
    Assert.NotNull(evidence.LastRestoreVerificationUtc);

    var authorized = await fixture.Gate().AuthorizeActivationAsync(databaseId);

    Assert.True(authorized.IsFailure);
    Assert.Equal(
      TenantStorageErrors.RecoveryActivationRestoreVerificationSuperseded.Code, authorized.Error.Code);
  }

  // A shared database is ONE recovery target however many tenants route to it. Evidence is keyed on the
  // physical database, so two tenants on the same database read one identical answer — and a tenant is
  // never a key into this boundary at all.
  [Fact]
  [Trait("Decision", "ADR-022")]
  public async Task Activation_evidence_is_scoped_to_the_physical_database_and_not_to_a_tenant()
  {
    await using var fixture = await ActivationFixture.CreateAsync();
    var sharedDatabaseId = await fixture.RegisterAsync(TenantDatabaseStorageMode.Shared);
    var baselineId = await fixture.RecordSuccessfulFullBackupAsync(sharedDatabaseId);
    var verificationRunId = await fixture.CompleteVerificationAsync(sharedDatabaseId, baselineId);
    await fixture.RecordReadinessAsync(sharedDatabaseId, TenantDatabaseRecoveryReadinessStatus.Protected);

    await fixture.AssignAsync("PEONE", sharedDatabaseId);
    await fixture.AssignAsync("PETWO", sharedDatabaseId);
    Assert.Equal(2, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] " +
      $"WHERE [TenantDatabaseId] = {sharedDatabaseId}"));

    var evidence = await fixture.ActivationEvidenceAsync(sharedDatabaseId);

    Assert.NotNull(evidence);
    Assert.Equal(TenantDatabaseStorageMode.Shared, evidence!.StorageMode);
    Assert.Equal(sharedDatabaseId, evidence.TenantDatabaseId);
    // One chain, one verification — not one per assignment.
    Assert.Equal(baselineId, evidence.CurrentBaselineBackupRunId);
    Assert.Equal(verificationRunId, evidence.VerifiedVerificationRunId);

    // A second physical database is entirely independent evidence, assignments notwithstanding.
    var otherDatabaseId = await fixture.RegisterAsync(TenantDatabaseStorageMode.Dedicated);
    var otherEvidence = await fixture.ActivationEvidenceAsync(otherDatabaseId);

    Assert.NotNull(otherEvidence);
    Assert.Null(otherEvidence!.CurrentBaselineBackupRunId);
    Assert.Null(otherEvidence.VerifiedVerificationRunId);
    Assert.True((await fixture.Gate().AuthorizeActivationAsync(otherDatabaseId)).IsFailure);
  }

  private sealed class ActivationFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string RestoreServerKey = "VerificationSqlServer";
    private const string Actor = "recovery-activation-tests";

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private string platformCatalog = string.Empty;
    private int registered;

    public static async Task<ActivationFixture> CreateAsync()
    {
      var fixture = new ActivationFixture();
      try
      {
        fixture.platformCatalog = $"SSAS_PhaseE_Platform_{fixture.token}";
        await ExecuteSqlAsync("master", $"CREATE DATABASE [{fixture.platformCatalog}]");
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

    public async Task<long> RegisterAsync(TenantDatabaseStorageMode storageMode)
    {
      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged,
        storageMode,
        ServerKey,
        $"SSAS_PhaseE_Tenant_{token}_{++registered}",
        TenantDatabaseProvisioningStatus.Ready,
        Actor,
        Now).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();

      var policy = TenantDatabaseBackupPolicy.Create(
        database.Id,
        enabled: true,
        TenantDatabaseBackupManagementMode.AutomaticByPlatform,
        "phase-e",
        fullBackupIntervalMinutes: 1_440,
        differentialBackupIntervalMinutes: null,
        transactionLogBackupIntervalMinutes: null,
        retentionExpectationDays: 30,
        restoreVerificationIntervalDays: 30,
        maximumBackupAgeMinutes: 2_880,
        Actor,
        Now).Value;
      platform.TenantDatabaseBackupPolicies.Add(policy);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    // A real tenant and a real assignment: the point of the shared-scope test is that two genuine routing
    // rows resolve to one physical database's evidence, which a synthetic identifier would not prove.
    public async Task AssignAsync(string tenantCode, long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      var tenant = Tenant.Create(
        TenantCode.Create(tenantCode).Value,
        TenantName.Create($"Phase E {tenantCode}").Value,
        Actor,
        Guid.NewGuid(),
        Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      var assignment = TenantDatabaseAssignment
        .CreateInitial(tenant.Id, tenantDatabaseId, "phase-e", Actor, Now).Value;
      platform.TenantDatabaseAssignments.Add(assignment);
      await platform.SaveChangesAsync();
    }

    // Recorded directly rather than by running BACKUP: this suite proves the evidence projection, not
    // backup execution.
    public async Task<long> RecordSuccessfulFullBackupAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      var run = TenantDatabaseBackupRun.Start(
        tenantDatabaseId, TenantDatabaseBackupOperation.SqlServerFull(), "phase-e", Actor, Now).Value;
      platform.TenantDatabaseBackupRuns.Add(run);
      await platform.SaveChangesAsync();

      run.Succeed(
        "evidence-identity", "artifact.bak", 1024, 500m, 520m, 0m, 500m, null, Actor, Now);
      await platform.SaveChangesAsync();
      return run.Id;
    }

    // The real admission/lifecycle path, so the verification row carries the identity a production run
    // would carry.
    public async Task<long> CompleteVerificationAsync(long tenantDatabaseId, long sourceBackupRunId)
    {
      await using var platform = PlatformContext();
      var store = new TenantDatabaseRestoreVerificationRunStore(platform, new TestClock(Now));

      var admitted = await store.TryAdmitAsync(new TenantDatabaseRestoreVerificationAdmissionRequest(
        tenantDatabaseId,
        sourceBackupRunId,
        ExpectedPreviousSuccessfulVerificationRunId: null,
        TenantDatabaseRestoreDepth.Full,
        RestoreServerKey,
        Actor));
      Assert.True(admitted.IsSuccess);

      var begun = await store.BeginRestoreAsync(
        admitted.Value,
        TenantDatabaseVerificationNaming.ForRun(tenantDatabaseId, admitted.Value),
        Actor);
      Assert.True(begun.IsSuccess);

      var succeeded = await store.MarkSucceededAndRecordEvidenceAsync(
        admitted.Value, sourceBackupRunId, Actor);
      Assert.True(succeeded.IsSuccess);
      return admitted.Value;
    }

    public async Task RecordReadinessAsync(
      long tenantDatabaseId,
      TenantDatabaseRecoveryReadinessStatus status)
    {
      await using var platform = PlatformContext();
      // The full-backup timestamp travels with the verdict: `CK_TenantDatabases_ProtectedRequiresFullBackup`
      // refuses a Protected row that claims no baseline, which is the schema enforcing the same rule the
      // readiness evaluator does.
      await new TenantDatabaseRecoveryReadinessWriter(platform, new TestClock(Now))
        .RecordRecoveryReadinessAsync(
          tenantDatabaseId,
          status,
          Actor,
          lastSuccessfulFullBackupUtc: Now.AddHours(-1),
          lastRestoreVerificationUtc: Now.AddDays(-1));
    }

    public async Task<TenantDatabaseRecoveryActivationEvidence?> ActivationEvidenceAsync(
      long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      return await new TenantDatabaseRecoveryActivationReadRepository(platform)
        .FindActivationEvidenceAsync(tenantDatabaseId);
    }

    public TenantDatabaseRecoveryActivationGate Gate() =>
      new TenantDatabaseRecoveryActivationGate(
        new TenantDatabaseRecoveryActivationReadRepository(PlatformContext()), new TestClock(Now));

    public async Task<int> ScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(),
        System.Globalization.CultureInfo.InvariantCulture);
    }

    private PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(
          ConnectionFor(platformCatalog),
          sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock(Now));
    }

    public async ValueTask DisposeAsync()
    {
      if (string.IsNullOrWhiteSpace(platformCatalog))
      {
        return;
      }

      try
      {
        await ExecuteSqlAsync("master",
          $"IF DB_ID(N'{platformCatalog}') IS NOT NULL BEGIN " +
          $"ALTER DATABASE [{platformCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"DROP DATABASE [{platformCatalog}]; END");
      }
      catch (SqlException error)
      {
        TestCatalogJanitor.RecordLeak(platformCatalog, error);
      }
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }.ConnectionString;

    private static async Task ExecuteSqlAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      command.CommandTimeout = 300;
      await command.ExecuteNonQueryAsync();
    }
  }

  private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => utcNow;
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "recovery-activation-tests";
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
}
