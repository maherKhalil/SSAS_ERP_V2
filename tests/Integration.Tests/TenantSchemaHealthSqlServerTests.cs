using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// ADR-018 schema health and migration orchestration against real SQL.
//
// The properties proven here cannot be established in memory: migration history is a database artifact,
// and single-writer ownership is a SQL Server session behaviour. Everything below runs against genuinely
// created catalogs.
public sealed class TenantSchemaHealthSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_fully_migrated_database_is_reported_up_to_date()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true);

    var result = await fixture.HealthService().CheckAsync(id);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseConnectivityStatus.Healthy, result.Value.ConnectivityStatus);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.UpToDate, result.Value.SchemaCompatibilityStatus);
    Assert.Empty(result.Value.PendingMigrations);

    // Persisted onto the PHYSICAL row, with the freshness anchor the gating model reads.
    var stored = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.UpToDate, stored.SchemaCompatibilityStatus);
    Assert.NotNull(stored.LastSchemaCheckUtc);
    Assert.NotNull(stored.LastConnectivityCheckUtc);
    Assert.Equal(stored.AppliedMigration, stored.TargetMigration);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_database_behind_the_application_is_reported_as_pending_not_up_to_date()
  {
    // The catalog exists and is reachable but has no tenant migrations applied — the "behind" case, and
    // also the missing-history case. Both must classify as PendingMigrations; classifying an empty
    // history as UpToDate would be the single most dangerous mistake this service could make.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);

    var result = await fixture.HealthService().CheckAsync(id);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseConnectivityStatus.Healthy, result.Value.ConnectivityStatus);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.PendingMigrations, result.Value.SchemaCompatibilityStatus);
    Assert.NotEmpty(result.Value.PendingMigrations);
    Assert.Null(result.Value.AppliedMigration);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_database_ahead_of_the_application_fails_closed()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true);

    // A migration this application does not know, appended to a genuine history.
    await HealthFixture.ExecuteAsync(fixture.CatalogA,
      "INSERT INTO [tenant].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) " +
      "VALUES (N'29990101000000_FutureTenantMigration', N'8.0.0')");

    var result = await fixture.HealthService().CheckAsync(id);

    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication, result.Value.SchemaCompatibilityStatus);

    // And the orchestrator refuses to touch it: an older application never downgrades a newer database.
    var migrate = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.AheadOfApplication, migrate.Value.Kind);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_divergent_migration_history_is_a_mismatch_and_is_never_appended_to()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);

    // A history containing only a migration we do not know: the lineage diverged rather than being behind.
    await HealthFixture.ExecuteAsync(fixture.CatalogB,
      """
      IF SCHEMA_ID(N'tenant') IS NULL EXEC(N'CREATE SCHEMA [tenant]');
      CREATE TABLE [tenant].[__EFMigrationsHistory](
        [MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL);
      INSERT INTO [tenant].[__EFMigrationsHistory] VALUES (N'20250101000000_ForeignLineage', N'8.0.0');
      """);

    var result = await fixture.HealthService().CheckAsync(id);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch, result.Value.SchemaCompatibilityStatus);

    var migrate = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.MigrationHistoryMismatch, migrate.Value.Kind);

    // Nothing was applied on top of the unknown lineage.
    Assert.Equal(1, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM [tenant].[__EFMigrationsHistory]"));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task An_unreachable_database_is_recorded_as_unreachable_not_as_a_schema_problem()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true, serverKey: "UnconfiguredServer");

    var result = await fixture.HealthService().CheckAsync(id);

    Assert.Equal(TenantDatabaseConnectivityStatus.Unreachable, result.Value.ConnectivityStatus);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.Unknown, result.Value.SchemaCompatibilityStatus);

    var stored = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseConnectivityStatus.Unreachable, stored.ConnectivityStatus);
    // No credential or connection material reaches the persisted row.
    Assert.Null(stored.LastMigrationError);
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public async Task A_customer_managed_database_is_never_connected_to_and_never_reported_healthy()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(
      "CustomerERP", migrateTenantStream: false, serverKey: "CustomerServer",
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);

    var result = await fixture.HealthService().CheckAsync(id);

    // Unknown, never Healthy: claiming verified health for a database we never contacted would be the most
    // misleading thing this service could report.
    Assert.Equal(TenantDatabaseConnectivityStatus.Unknown, result.Value.ConnectivityStatus);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.Unknown, result.Value.SchemaCompatibilityStatus);

    var stored = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseConnectivityStatus.Unknown, stored.ConnectivityStatus);

    var migrate = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.NotVerifiable, migrate.Value.Kind);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_shared_database_is_checked_once_however_many_tenants_it_hosts()
  {
    // The migration unit is the PHYSICAL database. Iterating assignments would check and migrate the same
    // database once per tenant — the mistake this discovery model exists to prevent.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true);
    await fixture.AssignTenantAsync(id, "SHARED-1");
    await fixture.AssignTenantAsync(id, "SHARED-2");
    await fixture.AssignTenantAsync(id, "SHARED-3");

    var sweep = await fixture.HealthService().SweepAsync(50);

    Assert.True(sweep.IsSuccess);
    Assert.Equal(1, sweep.Value.Discovered);
    Assert.Equal(1, sweep.Value.UpToDate);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task The_orchestrator_migrates_a_pending_database_and_verifies_the_result()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);

    var outcome = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());

    Assert.True(outcome.IsSuccess);
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.Migrated, outcome.Value.Kind);

    // The tenant schema really exists now, and the tenant history advanced.
    Assert.Equal(1, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[Companies]')"));
    Assert.Equal(1, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM [tenant].[__EFMigrationsHistory]"));

    // Status is persisted, and success is recorded only after post-verification re-read the history.
    var stored = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseMigrationExecutionStatus.Succeeded, stored.MigrationExecutionStatus);
    Assert.Equal(TenantDatabaseSchemaCompatibilityStatus.UpToDate, stored.SchemaCompatibilityStatus);
    Assert.NotNull(stored.LastMigrationSuccessUtc);
    Assert.Null(stored.LastMigrationError);

    // Re-running is a no-op rather than a second migration.
    var again = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.AlreadyUpToDate, again.Value.Kind);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_second_run_cannot_migrate_a_database_whose_ownership_is_already_held()
  {
    // Lock invariant 5: failure to acquire is a clean skip-and-report, never a forced proceed. Ownership is
    // held on a separate real connection, so this exercises the actual SQL Server primitive.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);

    await using var holder = new SqlConnection(HealthFixture.ConnectionFor(fixture.CatalogB));
    await holder.OpenAsync();
    await using var ownership = await TenantDatabaseMigrationOwnership.TryAcquireAsync(holder, TimeSpan.FromSeconds(1));
    Assert.NotNull(ownership);

    var outcome = await fixture.Orchestrator().MigrateAsync(
      id, new TenantMigrationRunOptions(OwnershipTimeout: TimeSpan.FromSeconds(1)));

    Assert.Equal(TenantDatabaseMigrationOutcomeKind.SkippedOwnershipHeld, outcome.Value.Kind);

    // Nothing was applied while ownership was held elsewhere.
    Assert.Equal(0, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[Companies]')"));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task Two_concurrent_orchestrators_produce_exactly_one_migration()
  {
    // The multi-instance race, run for real: two orchestrators with independent connections against one
    // physical database. Exactly one may migrate it.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);

    var options = new TenantMigrationRunOptions(OwnershipTimeout: TimeSpan.FromSeconds(20));
    var results = await Task.WhenAll(
      fixture.Orchestrator().MigrateAsync(id, options),
      fixture.Orchestrator().MigrateAsync(id, options));

    var kinds = results.Select(result => result.Value.Kind).ToArray();
    Assert.Single(kinds, kind => kind == TenantDatabaseMigrationOutcomeKind.Migrated);

    // The loser either waited and found the work already done, or was skipped. Both are correct; what is
    // NOT acceptable is a second migration or a failure.
    Assert.Contains(kinds, kind =>
      kind is TenantDatabaseMigrationOutcomeKind.AlreadyUpToDate
        or TenantDatabaseMigrationOutcomeKind.SkippedOwnershipHeld);
    Assert.DoesNotContain(TenantDatabaseMigrationOutcomeKind.Failed, kinds);

    Assert.Equal(1, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM [tenant].[__EFMigrationsHistory]"));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_customer_dba_database_is_reported_blocked_rather_than_migrated_or_failed()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(
      fixture.CatalogB, migrateTenantStream: false,
      managementMode: TenantDatabaseMigrationManagementMode.CustomerDba);

    var outcome = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());

    Assert.Equal(TenantDatabaseMigrationOutcomeKind.BlockedPendingCustomer, outcome.Value.Kind);

    var stored = await fixture.ReadAsync(id);
    Assert.Equal(TenantDatabaseMigrationExecutionStatus.BlockedPendingCustomer, stored.MigrationExecutionStatus);

    // The platform applied no DDL to a database it may never touch.
    Assert.Equal(0, await HealthFixture.ScalarAsync(fixture.CatalogB,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[tenant].[Companies]')"));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task Approval_gated_databases_are_blocked_without_approval_and_migrate_with_it()
  {
    // Absence of approval is denial, never default-allow.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(
      fixture.CatalogB, migrateTenantStream: false,
      managementMode: TenantDatabaseMigrationManagementMode.PlatformAfterApproval);

    var denied = await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions());
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.BlockedPendingCustomer, denied.Value.Kind);

    var approved = await fixture.Orchestrator().MigrateAsync(
      id, new TenantMigrationRunOptions(ApprovalGranted: true));
    Assert.Equal(TenantDatabaseMigrationOutcomeKind.Migrated, approved.Value.Kind);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task The_request_path_is_blocked_while_a_database_is_incompatible_and_allowed_once_migrated()
  {
    // End to end: a tenant routed to a database with pending migrations cannot obtain a TenantDbContext,
    // and the denial is a controlled TenantStorage.* result rather than a raw SQL error.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);
    var tenantId = await fixture.AssignTenantAsync(id, "GATED");

    await fixture.HealthService().CheckAsync(id);

    var blocked = await fixture.ContextFactory(tenantId).CreateAsync(tenantId);
    Assert.True(blocked.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseUpgradeRequired.Code, blocked.Error.Code);

    // Migrate, then the same tenant is served.
    Assert.Equal(
      TenantDatabaseMigrationOutcomeKind.Migrated,
      (await fixture.Orchestrator().MigrateAsync(id, new TenantMigrationRunOptions())).Value.Kind);

    var allowed = await fixture.ContextFactory(tenantId).CreateAsync(tenantId);
    Assert.True(allowed.IsSuccess);
    await allowed.Value.DisposeAsync();
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task An_unverified_database_denies_request_traffic()
  {
    // Unknown denies: a freshly registered database is not servable until something has verified it, even
    // though this catalog is in fact fully migrated. Being correct is not the same as being verified.
    await using var fixture = await HealthFixture.CreateAsync();
    var id = await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true);
    var tenantId = await fixture.AssignTenantAsync(id, "UNVERIFIED");

    var result = await fixture.ContextFactory(tenantId).CreateAsync(tenantId);

    Assert.True(result.IsFailure);
    // Connectivity is unverified too, and it is evaluated first — so the reported reason is
    // "unavailable" rather than a schema verdict. That precedence is deliberate: an operator must not be
    // sent to investigate a release when nothing has yet established the database can be reached at all.
    Assert.Equal(TenantStorageErrors.TenantDatabaseUnavailable.Code, result.Error.Code);

    // Once connectivity has been established but the schema verdict is still absent, the denial moves to
    // the schema reason — proving Unknown denies on its own account and not merely via connectivity.
    await fixture.SetHealthAsync(id, database => database.RecordConnectivity(
      TenantDatabaseConnectivityStatus.Healthy, "health-tests", DateTimeOffset.UtcNow));

    var schemaResult = await fixture.ContextFactory(tenantId).CreateAsync(tenantId);
    Assert.True(schemaResult.IsFailure);
    Assert.Equal(TenantStorageErrors.SchemaHealthUnknown.Code, schemaResult.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task A_fleet_run_reports_every_category_and_is_not_failed_by_a_blocked_database()
  {
    await using var fixture = await HealthFixture.CreateAsync();
    await fixture.RegisterAsync(fixture.CatalogA, migrateTenantStream: true);
    await fixture.RegisterAsync(fixture.CatalogB, migrateTenantStream: false);
    await fixture.RegisterAsync("CustomerERP", migrateTenantStream: false, serverKey: "CustomerServer",
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);
    await fixture.RegisterAsync("SSAS_Unreachable", migrateTenantStream: false, serverKey: "UnconfiguredServer");

    var summary = await fixture.Orchestrator().RunAsync(new TenantMigrationRunOptions());

    Assert.True(summary.IsSuccess);
    Assert.Equal(4, summary.Value.Discovered);
    Assert.Equal(1, summary.Value.AlreadyUpToDate);
    Assert.Equal(1, summary.Value.Migrated);
    Assert.Equal(1, summary.Value.NotVerifiable);
    Assert.Equal(1, summary.Value.Unreachable);
    // A customer-managed or unreachable database does not fail the release.
    Assert.Equal(0, summary.Value.Failed);
  }

  // Three real catalogs: the Platform registry, and two physical tenant databases.
  private sealed class HealthFixture : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private readonly string platformCatalog;

    private HealthFixture(string platformCatalog, string catalogA, string catalogB)
    {
      this.platformCatalog = platformCatalog;
      CatalogA = catalogA;
      CatalogB = catalogB;
    }

    public string CatalogA { get; }

    public string CatalogB { get; }

    public static async Task<HealthFixture> CreateAsync()
    {
      var fixture = new HealthFixture(
        $"SSAS_ERP_HEALTH_P_{Guid.NewGuid():N}",
        $"SSAS_ERP_HEALTH_A_{Guid.NewGuid():N}",
        $"SSAS_ERP_HEALTH_B_{Guid.NewGuid():N}");
      try
      {
        await using var platform = fixture.PlatformContext();
        await platform.Database.MigrateAsync();

        // Both tenant catalogs are created EMPTY — via CREATE DATABASE, deliberately not
        // EnsureCreatedAsync, which would materialise the tenant tables with no migration history and then
        // collide with the migration under test. Only A is migrated; B is the "behind" database the
        // migration tests start from.
        foreach (var catalog in new[] { fixture.CatalogA, fixture.CatalogB })
        {
          await HealthFixture.CreateCatalogAsync(catalog);
        }

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

    public PlatformDbContext PlatformContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(platformCatalog), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    private static TenantDbContext TenantContext(string catalog)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(catalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable, TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    private static TenantDatabaseConnectionFactory ConnectionFactory()
    {
      var options = Options.Create(new TenantStorageOptions());
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions
      {
        ConnectionString = new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString
      };
      return new TenantDatabaseConnectionFactory(options);
    }

    public TenantDatabaseSchemaHealthService HealthService()
    {
      var context = PlatformContext();
      return new TenantDatabaseSchemaHealthService(
        new TenantDatabaseRegistryReadRepository(context),
        ConnectionFactory(),
        new TenantDatabaseHealthWriter(context),
        new TestClock());
    }

    // A fresh instance per call, each with its own PlatformDbContext and its own connections — which is
    // what makes the concurrency test a genuine multi-instance race rather than two calls sharing state.
    public TenantDatabaseMigrationOrchestrator Orchestrator()
    {
      var context = PlatformContext();
      return new TenantDatabaseMigrationOrchestrator(
        new TenantDatabaseRegistryReadRepository(context),
        ConnectionFactory(),
        new TenantDatabaseHealthWriter(context),
        new TestClock());
    }

    public TenantDbContextFactory ContextFactory(Guid tenantId)
    {
      var context = PlatformContext();
      return new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(context)),
        ConnectionFactory(),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(),
        new FixedTenant(tenantId),
        new TestClock());
    }

    public async Task<long> RegisterAsync(
      string databaseName,
      bool migrateTenantStream,
      string serverKey = PrimaryServerKey,
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      TenantDatabaseMigrationManagementMode managementMode =
        TenantDatabaseMigrationManagementMode.AutomaticByPlatform)
    {
      if (migrateTenantStream)
      {
        await using var tenant = TenantContext(databaseName);
        await tenant.Database.MigrateAsync();
      }

      await using var platform = PlatformContext();
      var database = TenantDatabase.Register(
        hostingMode, storageMode, serverKey, databaseName,
        TenantDatabaseProvisioningStatus.Ready, "health-tests", Now, managementMode).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    public async Task<Guid> AssignTenantAsync(long tenantDatabaseId, string code)
    {
      await using var platform = PlatformContext();
      var tenant = Tenant.Create(
        SSAS.Platform.Domain.ValueObjects.TenantCode.Create($"{code}-{Guid.NewGuid():N}"[..12]).Value,
        SSAS.Platform.Domain.ValueObjects.TenantName.Create($"Tenant {code}").Value,
        "health-tests", Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.Create(tenant.TenantId, tenantDatabaseId, 1, "health-tests", "health-tests", Now).Value);
      await platform.SaveChangesAsync();
      return tenant.TenantId;
    }

    // Sets health directly through the aggregate, for cases that need a specific dimension established
    // without running the checker that would establish all of them.
    public async Task SetHealthAsync(long tenantDatabaseId, Action<TenantDatabase> mutate)
    {
      await using var platform = PlatformContext();
      await new TenantDatabaseHealthWriter(platform).RecordHealthAsync(tenantDatabaseId, mutate);
    }

    public async Task<TenantDatabase> ReadAsync(long tenantDatabaseId)
    {
      await using var platform = PlatformContext();
      return await platform.TenantDatabases.AsNoTracking().SingleAsync(item => item.Id == tenantDatabaseId);
    }

    public static async Task CreateCatalogAsync(string catalog)
    {
      await using var connection = new SqlConnection(
        new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString);
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      // The catalog name is generated by this fixture, never caller input; quoted defensively regardless.
      command.CommandText = $"CREATE DATABASE [{catalog.Replace("]", "]]", StringComparison.Ordinal)}]";
      await command.ExecuteNonQueryAsync();
    }

    public static async Task ExecuteAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      foreach (var batch in sql.Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
      {
        await using var command = connection.CreateCommand();
        command.CommandText = batch;
        await command.ExecuteNonQueryAsync();
      }
    }

    public static async Task<int> ScalarAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { CatalogA, CatalogB, platformCatalog })
      {
        try
        {
          await using var context = TenantContext(catalog);
          await context.Database.EnsureDeletedAsync();
        }
        catch (SqlException)
        {
          // A catalog that was never created is not worth masking a real failure for.
        }
      }
    }
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "health-tests";

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

  private sealed class FixedTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    // Real UTC: the gating freshness model compares the stored check timestamp against "now", so a frozen
    // clock would make every freshly-written health record look an implausible distance in the past.
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
