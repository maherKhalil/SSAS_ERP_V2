using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// TS-1A/TS-1B tenant-storage registry SQL verification (ADR-017). Proves the schema-enforced invariants
// against real SQL Server, because the whole point of this slice is that the DATABASE — not application
// logic — guarantees at most one active assignment per tenant and rejects the invalid hosting/storage
// combination.
public sealed class TenantStorageRegistrySqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Migration_creates_registry_tables_with_expected_shape()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());

    // Platform operational metadata: no tenant global filter, so routing/bootstrap can read it without an
    // ambient tenant, and neither type is ITenantOwnedEntity.
    var databaseEntity = context.Model.FindEntityType(typeof(TenantDatabase));
    var assignmentEntity = context.Model.FindEntityType(typeof(TenantDatabaseAssignment));
    Assert.NotNull(databaseEntity);
    Assert.NotNull(assignmentEntity);
    Assert.Null(databaseEntity!.GetQueryFilter());
    Assert.Null(assignmentEntity!.GetQueryFilter());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabase).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(TenantDatabaseAssignment).GetInterfaces());

    Assert.True(databaseEntity.FindProperty(nameof(TenantDatabase.RowVersion))?.IsConcurrencyToken);
    Assert.True(assignmentEntity.FindProperty(nameof(TenantDatabaseAssignment.RowVersion))?.IsConcurrencyToken);

    Assert.Equal(
      ["TenantDatabaseId", "HostingMode", "StorageMode", "ServerKey", "DatabaseName", "ProvisioningStatus", "RowVersion", "CreatedUtc", "ModifiedUtc", "CreatedBy", "ModifiedBy"],
      await ReadStringsAsync(context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantDatabases' ORDER BY ORDINAL_POSITION"));
    Assert.Equal(
      ["TenantDatabaseAssignmentId", "TenantId", "TenantDatabaseId", "RoutingVersion", "AssignedUtc", "EndedUtc", "Reason", "RowVersion", "CreatedUtc", "ModifiedUtc", "CreatedBy", "ModifiedBy"],
      await ReadStringsAsync(context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantDatabaseAssignments' ORDER BY ORDINAL_POSITION"));

    // No endpoint, credential-reference or connection-string column exists in this slice (ADR-021 defers
    // customer-managed connectivity entirely).
    Assert.Equal(0, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'TenantDatabases' " +
      "AND COLUMN_NAME IN ('Endpoint', 'Host', 'Port', 'CredentialSecretReference', 'ConnectionString', 'Password', 'AuthenticationMode')"));

    Assert.Equal(1, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[TenantDatabaseAssignments]') AND name = N'UX_TenantDatabaseAssignments_ActiveTenant' AND is_unique = 1 AND has_filter = 1"));
    Assert.Equal(1, await ReadInt32Async(context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[TenantDatabases]') AND name = N'UX_TenantDatabases_ServerKey_DatabaseName' AND is_unique = 1"));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task One_active_assignment_per_tenant_is_enforced_while_history_is_retained()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-ACTIVE");
    var shared = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, "PrimarySqlServer", "SSAS_Shared_01");

    var first = TenantDatabaseAssignment.CreateInitial(tenantId, shared.Id, "first", "test", TenantStorageSqlDatabase.Now).Value;
    context.TenantDatabaseAssignments.Add(first);
    await context.SaveChangesAsync();

    // A second ACTIVE assignment for the same tenant must be impossible at the database level.
    var duplicate = TenantDatabaseAssignment.CreateInitial(tenantId, shared.Id, "duplicate", "test", TenantStorageSqlDatabase.Now).Value;
    context.TenantDatabaseAssignments.Add(duplicate);
    await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    context.Entry(duplicate).State = EntityState.Detached;

    // Ending the first assignment frees the active slot; the ended row REMAINS as history.
    Assert.True(first.End("test", TenantStorageSqlDatabase.Now.AddMinutes(5)).IsSuccess);
    await context.SaveChangesAsync();

    var second = TenantDatabaseAssignment.Create(tenantId, shared.Id, 2, "second", "test", TenantStorageSqlDatabase.Now.AddMinutes(5)).Value;
    context.TenantDatabaseAssignments.Add(second);
    await context.SaveChangesAsync();

    Assert.Equal(2, await ReadInt32Async(context,
      $"SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{tenantId}'"));
    Assert.Equal(1, await ReadInt32Async(context,
      $"SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{tenantId}' AND [EndedUtc] IS NULL"));

    // Many ENDED assignments for one tenant are permitted — the filter is what makes that possible.
    var third = TenantDatabaseAssignment.Create(tenantId, shared.Id, 3, "third", "test", TenantStorageSqlDatabase.Now.AddMinutes(10)).Value;
    Assert.True(third.End("test", TenantStorageSqlDatabase.Now.AddMinutes(11)).IsSuccess);
    context.TenantDatabaseAssignments.Add(third);
    await context.SaveChangesAsync();
    Assert.Equal(2, await ReadInt32Async(context,
      $"SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{tenantId}' AND [EndedUtc] IS NOT NULL"));
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("PlatformManaged", "Shared", true)]
  [InlineData("PlatformManaged", "Dedicated", true)]
  [InlineData("CustomerManaged", "Dedicated", true)]
  [InlineData("CustomerManaged", "Shared", false)]
  public async Task Hosting_and_storage_matrix_is_enforced_by_the_database(string hostingMode, string storageMode, bool accepted)
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    // Raw SQL deliberately bypasses the domain guard, so this proves the DATABASE constraint rather than
    // the application validation that shadows it.
    var sql =
      "INSERT INTO [platform].[TenantDatabases] ([HostingMode], [StorageMode], [ServerKey], [DatabaseName], [ProvisioningStatus], [CreatedUtc], [ModifiedUtc]) " +
      $"VALUES (N'{hostingMode}', N'{storageMode}', N'Key-{Guid.NewGuid():N}', N'Db-{Guid.NewGuid():N}', N'Ready', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())";

    if (accepted)
    {
      Assert.Equal(1, await context.Database.ExecuteSqlRawAsync(sql));
    }
    else
    {
      var failure = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(sql));
      Assert.Contains("CK_TenantDatabases_CustomerManagedIsDedicated", failure.Message, StringComparison.Ordinal);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Assignment_check_constraints_reject_invalid_routing_version_and_dates()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-CHECKS");
    var shared = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, "PrimarySqlServer", "SSAS_Shared_Checks");

    var zeroVersion = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "INSERT INTO [platform].[TenantDatabaseAssignments] ([TenantId], [TenantDatabaseId], [RoutingVersion], [AssignedUtc], [CreatedUtc], [ModifiedUtc]) " +
      $"VALUES ('{tenantId}', {shared.Id}, 0, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())"));
    Assert.Contains("CK_TenantDatabaseAssignments_RoutingVersion", zeroVersion.Message, StringComparison.Ordinal);

    var endedBeforeAssigned = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "INSERT INTO [platform].[TenantDatabaseAssignments] ([TenantId], [TenantDatabaseId], [RoutingVersion], [AssignedUtc], [EndedUtc], [CreatedUtc], [ModifiedUtc]) " +
      $"VALUES ('{tenantId}', {shared.Id}, 1, SYSDATETIMEOFFSET(), DATEADD(day, -1, SYSDATETIMEOFFSET()), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())"));
    Assert.Contains("CK_TenantDatabaseAssignments_EndedUtc", endedBeforeAssigned.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task RowVersion_is_generated_and_changes_on_update()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var shared = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, "PrimarySqlServer", "SSAS_Shared_RowVersion");
    var tenantId = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-ROWVER");

    var assignment = TenantDatabaseAssignment.CreateInitial(tenantId, shared.Id, "initial", "test", TenantStorageSqlDatabase.Now).Value;
    context.TenantDatabaseAssignments.Add(assignment);
    await context.SaveChangesAsync();

    Assert.NotEmpty(shared.RowVersion);
    var before = assignment.RowVersion.ToArray();
    Assert.NotEmpty(before);

    Assert.True(assignment.End("test", TenantStorageSqlDatabase.Now.AddMinutes(1)).IsSuccess);
    await context.SaveChangesAsync();
    Assert.NotEqual(before, assignment.RowVersion);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Registry_rows_cannot_be_physically_deleted()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var shared = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, "PrimarySqlServer", "SSAS_Shared_Retention");

    context.TenantDatabases.Remove(shared);
    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
  }

  // ---- TS-1B bootstrap ----

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Bootstrap_registers_the_shared_database_and_assigns_every_tenant()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantIds = new List<Guid>();
    for (var index = 0; index < 3; index++)
    {
      tenantIds.Add(await TenantStorageSqlDatabase.SeedTenantAsync(context, $"T-BOOT-{index}"));
    }

    var outcome = await TenantStorageSqlDatabase.CreateBootstrap(context).RunAsync();

    Assert.True(outcome.TenantDatabaseCreated);
    Assert.Equal(3, outcome.AssignmentsCreated);
    Assert.Equal(0, outcome.TenantsAlreadyAssigned);

    var registered = await context.TenantDatabases.AsNoTracking().SingleAsync();
    Assert.Equal(TenantDatabaseHostingMode.PlatformManaged, registered.HostingMode);
    Assert.Equal(TenantDatabaseStorageMode.Shared, registered.StorageMode);
    Assert.Equal(TenantStorageSqlDatabase.DefaultServerKey, registered.ServerKey);
    Assert.Equal(database.DatabaseName, registered.DatabaseName);
    Assert.Equal(TenantDatabaseProvisioningStatus.Ready, registered.ProvisioningStatus);

    var assignments = await context.TenantDatabaseAssignments.AsNoTracking().ToListAsync();
    Assert.Equal(3, assignments.Count);
    Assert.All(assignments, assignment =>
    {
      Assert.Equal(registered.Id, assignment.TenantDatabaseId);
      Assert.Equal(TenantDatabaseAssignment.InitialRoutingVersion, assignment.RoutingVersion);
      Assert.Null(assignment.EndedUtc);
    });
    Assert.Equal(tenantIds.OrderBy(id => id), assignments.Select(a => a.TenantId).OrderBy(id => id));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Bootstrap_is_idempotent_across_runs()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-IDEM-1");
    await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-IDEM-2");

    var first = await TenantStorageSqlDatabase.CreateBootstrap(context).RunAsync();
    var snapshot = await context.TenantDatabaseAssignments.AsNoTracking()
      .OrderBy(a => a.TenantId)
      .Select(a => new { a.Id, a.TenantId, a.RoutingVersion, a.AssignedUtc })
      .ToListAsync();

    await using var secondContext = database.CreateContext();
    var second = await TenantStorageSqlDatabase.CreateBootstrap(secondContext).RunAsync();

    Assert.False(second.TenantDatabaseCreated);
    Assert.Equal(0, second.AssignmentsCreated);
    Assert.Equal(2, second.TenantsAlreadyAssigned);
    Assert.False(second.ChangedAnything);
    Assert.Equal(first.TenantDatabaseId, second.TenantDatabaseId);

    // No duplicate rows, and no churn that would look like re-assignment.
    Assert.Equal(1, await ReadInt32Async(secondContext, "SELECT COUNT(*) FROM [platform].[TenantDatabases]"));
    var after = await secondContext.TenantDatabaseAssignments.AsNoTracking()
      .OrderBy(a => a.TenantId)
      .Select(a => new { a.Id, a.TenantId, a.RoutingVersion, a.AssignedUtc })
      .ToListAsync();
    Assert.Equal(snapshot, after);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Bootstrap_completes_a_partially_initialised_registry()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var assigned = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-PARTIAL-ASSIGNED");
    var unassigned = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-PARTIAL-UNASSIGNED");

    // Pre-existing registry: the shared database row and one tenant already assigned.
    var existing = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, TenantStorageSqlDatabase.DefaultServerKey, database.DatabaseName);
    context.TenantDatabaseAssignments.Add(
      TenantDatabaseAssignment.CreateInitial(assigned, existing.Id, "pre-existing", "test", TenantStorageSqlDatabase.Now).Value);
    await context.SaveChangesAsync();

    var outcome = await TenantStorageSqlDatabase.CreateBootstrap(context).RunAsync();

    Assert.False(outcome.TenantDatabaseCreated);
    Assert.Equal(existing.Id, outcome.TenantDatabaseId);
    Assert.Equal(1, outcome.AssignmentsCreated);
    Assert.Equal(1, outcome.TenantsAlreadyAssigned);
    Assert.Equal(1, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[TenantDatabases]"));
    Assert.Equal(1, await ReadInt32Async(context,
      $"SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] WHERE [TenantId] = '{unassigned}' AND [EndedUtc] IS NULL"));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Bootstrap_fails_closed_when_a_tenant_is_already_routed_elsewhere()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await TenantStorageSqlDatabase.SeedTenantAsync(context, "T-ELSEWHERE");

    // The tenant is already routed to a DIFFERENT database — a real routing decision (for example an
    // earlier promotion). Bootstrap must refuse rather than silently re-point it.
    await TenantStorageSqlDatabase.SeedDatabaseAsync(context, TenantStorageSqlDatabase.DefaultServerKey, database.DatabaseName);
    var elsewhere = await TenantStorageSqlDatabase.SeedDatabaseAsync(context, "OtherSqlServer", "SSAS_Dedicated_01", TenantDatabaseStorageMode.Dedicated);
    context.TenantDatabaseAssignments.Add(
      TenantDatabaseAssignment.CreateInitial(tenantId, elsewhere.Id, "promoted", "test", TenantStorageSqlDatabase.Now).Value);
    await context.SaveChangesAsync();

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => TenantStorageSqlDatabase.CreateBootstrap(context).RunAsync());
    Assert.Contains("already has an active assignment", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Bootstrap_fails_closed_when_the_existing_registration_is_classified_differently()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    // Same physical identity, recorded as dedicated: the registry and the deployment disagree.
    await TenantStorageSqlDatabase.SeedDatabaseAsync(
      context, TenantStorageSqlDatabase.DefaultServerKey, database.DatabaseName, TenantDatabaseStorageMode.Dedicated);

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => TenantStorageSqlDatabase.CreateBootstrap(context).RunAsync());
    Assert.Contains("platform-managed shared database", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Concurrent_bootstrap_converges_on_one_database_and_one_assignment_per_tenant()
  {
    await using var database = await TenantStorageSqlDatabase.CreateAsync();
    await using var seedContext = database.CreateContext();
    for (var index = 0; index < 5; index++)
    {
      await TenantStorageSqlDatabase.SeedTenantAsync(seedContext, $"T-RACE-{index}");
    }

    // Two hosts starting together: correctness must come from the unique indexes, not from ordering.
    await using var firstContext = database.CreateContext();
    await using var secondContext = database.CreateContext();
    await Task.WhenAll(
      TenantStorageSqlDatabase.CreateBootstrap(firstContext).RunAsync(),
      TenantStorageSqlDatabase.CreateBootstrap(secondContext).RunAsync());

    await using var verifyContext = database.CreateContext();
    Assert.Equal(1, await ReadInt32Async(verifyContext, "SELECT COUNT(*) FROM [platform].[TenantDatabases]"));
    Assert.Equal(5, await ReadInt32Async(verifyContext,
      "SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments] WHERE [EndedUtc] IS NULL"));
    Assert.Equal(5, await ReadInt32Async(verifyContext, "SELECT COUNT(*) FROM [platform].[TenantDatabaseAssignments]"));
  }

  private static async Task<string[]> ReadStringsAsync(PlatformDbContext context, string sql)
  {
    await using var connection = new SqlConnection(context.Database.GetConnectionString());
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var values = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return [.. values];
  }

  private static async Task<int> ReadInt32Async(PlatformDbContext context, string sql)
  {
    await using var connection = new SqlConnection(context.Database.GetConnectionString());
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private sealed class TenantStorageSqlDatabase(string connectionString, string databaseName) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 13, 11, 0, 0, TimeSpan.Zero);

    public const string DefaultServerKey = "PrimarySqlServer";

    public string DatabaseName => databaseName;

    public static async Task<TenantStorageSqlDatabase> CreateAsync()
    {
      var name = $"SSAS_ERP_TS1_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = name };
      var database = new TenantStorageSqlDatabase(builder.ConnectionString, name);
      try
      {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(), new TestClock());
    }

    public static TenantStorageBootstrapService CreateBootstrap(PlatformDbContext context) =>
      new(context, new TestClock(), Options.Create(new TenantStorageOptions { DefaultServerKey = DefaultServerKey }));

    public static async Task<Guid> SeedTenantAsync(PlatformDbContext context, string code)
    {
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Tenant {code}").Value, "seed", Guid.NewGuid(), Now).Value;
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      return tenant.Id;
    }

    public static async Task<TenantDatabase> SeedDatabaseAsync(
      PlatformDbContext context,
      string serverKey,
      string name,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, storageMode, serverKey, name,
        TenantDatabaseProvisioningStatus.Ready, "seed", Now).Value;
      context.TenantDatabases.Add(database);
      await context.SaveChangesAsync();
      return database;
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => TenantStorageSqlDatabase.Now;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "tenant-storage-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  // No ambient tenant: the registry is Platform operational metadata and must be readable without one.
  private sealed class TestCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }
}
