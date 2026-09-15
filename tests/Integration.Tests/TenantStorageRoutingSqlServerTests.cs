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
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// TS-1C routing against real SQL (ADR-017). Resolution reads genuinely persisted registry state, and the
// connection factory is proven to reach the database the route names — and only that one.
public sealed class TenantStorageRoutingSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_tenant_resolves_to_its_registered_shared_database()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-ROUTE");
    var shared = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, database.DatabaseName);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, shared.Id, routingVersion: 3);

    var result = await Resolver(context).ResolveAsync(tenantId);

    Assert.True(result.IsSuccess);
    Assert.Equal(tenantId, result.Value.TenantId);
    Assert.Equal(shared.Id, result.Value.TenantDatabaseId);
    Assert.Equal(RoutingSqlDatabase.PrimaryServerKey, result.Value.ServerKey);
    Assert.Equal(database.DatabaseName, result.Value.DatabaseName);
    Assert.Equal(TenantDatabaseHostingMode.PlatformManaged, result.Value.HostingMode);
    Assert.Equal(3, result.Value.RoutingVersion);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task The_resolved_route_opens_a_connection_to_that_database()
  {
    // End-to-end proof of the slice: persisted registry -> route -> trusted ServerKey config -> a real
    // connection that lands in the expected catalog.
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-OPEN");
    var shared = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, database.DatabaseName);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, shared.Id);

    var route = (await Resolver(context).ResolveAsync(tenantId)).Value;
    var connectionResult = RoutingSqlDatabase.Factory().Create(route);

    Assert.True(connectionResult.IsSuccess);
    await using var connection = connectionResult.Value;
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";
    Assert.Equal(database.DatabaseName, Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Two_tenants_route_to_two_different_databases_and_never_cross()
  {
    // Cross-database safety: two registry rows naming two real catalogs on the same instance. Each tenant
    // reaches only its own, and routing follows trusted registry state rather than any caller choice.
    await using var primary = await RoutingSqlDatabase.CreateAsync();
    await using var secondary = await RoutingSqlDatabase.CreateAsync();
    await using var context = primary.CreateContext();

    var tenantA = await RoutingSqlDatabase.SeedTenantAsync(context, "T-CROSS-A");
    var tenantB = await RoutingSqlDatabase.SeedTenantAsync(context, "T-CROSS-B");
    var databaseA = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, primary.DatabaseName);
    var databaseB = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, secondary.DatabaseName);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantA, databaseA.Id);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantB, databaseB.Id);

    var resolver = Resolver(context);
    var routeA = (await resolver.ResolveAsync(tenantA)).Value;
    var routeB = (await resolver.ResolveAsync(tenantB)).Value;

    Assert.Equal(primary.DatabaseName, routeA.DatabaseName);
    Assert.Equal(secondary.DatabaseName, routeB.DatabaseName);

    var factory = RoutingSqlDatabase.Factory();
    await using var connectionA = factory.Create(routeA).Value;
    await using var connectionB = factory.Create(routeB).Value;
    await connectionA.OpenAsync();
    await connectionB.OpenAsync();

    Assert.Equal(primary.DatabaseName, await CurrentDatabaseAsync(connectionA));
    Assert.Equal(secondary.DatabaseName, await CurrentDatabaseAsync(connectionB));
    Assert.NotEqual(await CurrentDatabaseAsync(connectionA), await CurrentDatabaseAsync(connectionB));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task An_unknown_server_key_fails_and_opens_no_connection()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-UNKNOWN-KEY");
    var shared = await RoutingSqlDatabase.SeedDatabaseAsync(context, "UnconfiguredServer", database.DatabaseName);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, shared.Id);

    var route = (await Resolver(context).ResolveAsync(tenantId)).Value;
    var connectionResult = RoutingSqlDatabase.Factory().Create(route);

    // No fallback to the Platform connection, and nothing is opened.
    Assert.True(connectionResult.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, connectionResult.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_tenant_with_only_an_ended_assignment_fails_before_any_connection()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-ENDED");
    var shared = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, database.DatabaseName);
    var assignment = await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, shared.Id);
    Assert.True(assignment.End("test", RoutingSqlDatabase.Now.AddMinutes(1)).IsSuccess);
    await context.SaveChangesAsync();

    var result = await Resolver(context).ResolveAsync(tenantId);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ActiveAssignmentMissing.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_database_that_is_not_ready_fails_before_any_connection()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-NOTREADY");
    var registered = await RoutingSqlDatabase.SeedDatabaseAsync(
      context, RoutingSqlDatabase.PrimaryServerKey, database.DatabaseName,
      provisioningStatus: TenantDatabaseProvisioningStatus.Provisioning);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, registered.Id);

    var result = await Resolver(context).ResolveAsync(tenantId);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseNotReady.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public async Task A_customer_managed_registration_is_rejected_at_resolution()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-CUSTOMER");
    var customer = await RoutingSqlDatabase.SeedDatabaseAsync(
      context, "CustomerServer", "CustomerERP",
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);
    await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, customer.Id);

    var result = await Resolver(context).ResolveAsync(tenantId);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Resolution_reflects_registry_changes_because_nothing_is_cached()
  {
    await using var database = await RoutingSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    var tenantId = await RoutingSqlDatabase.SeedTenantAsync(context, "T-NOCACHE");
    var shared = await RoutingSqlDatabase.SeedDatabaseAsync(context, RoutingSqlDatabase.PrimaryServerKey, database.DatabaseName);
    var assignment = await RoutingSqlDatabase.SeedAssignmentAsync(context, tenantId, shared.Id);

    var resolver = Resolver(context);
    Assert.True((await resolver.ResolveAsync(tenantId)).IsSuccess);

    // End the assignment; the very next resolution must observe it rather than a memoised route.
    Assert.True(assignment.End("test", RoutingSqlDatabase.Now.AddMinutes(1)).IsSuccess);
    await context.SaveChangesAsync();

    var second = await resolver.ResolveAsync(tenantId);
    Assert.True(second.IsFailure);
    Assert.Equal(TenantStorageErrors.ActiveAssignmentMissing.Code, second.Error.Code);
  }

  private static TenantDatabaseResolver Resolver(PlatformDbContext context) =>
    new(new TenantDatabaseRegistryReadRepository(context));

  private static async Task<string?> CurrentDatabaseAsync(SqlConnection connection)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";
    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private sealed class RoutingSqlDatabase(string connectionString, string databaseName) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 13, 11, 0, 0, TimeSpan.Zero);

    public const string PrimaryServerKey = "PrimarySqlServer";

    public string DatabaseName => databaseName;

    public static async Task<RoutingSqlDatabase> CreateAsync()
    {
      var name = $"SSAS_ERP_TS1C_{Guid.NewGuid():N}";
      var builder = new SqlConnectionStringBuilder(Configured()) { InitialCatalog = name };
      var database = new RoutingSqlDatabase(builder.ConnectionString, name);
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

    private static string Configured() =>
      IntegrationSqlEnvironment.BaseConnectionString;

    public PlatformDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(), new TestClock());
    }

    // Trusted server configuration: the base connection string without a catalog, so the route's
    // DatabaseName is what selects the database.
    public static TenantDatabaseConnectionFactory Factory()
    {
      var options = Options.Create(new TenantStorageOptions());
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions
      {
        ConnectionString = new SqlConnectionStringBuilder(Configured()) { InitialCatalog = "master" }.ConnectionString
      };
      return new TenantDatabaseConnectionFactory(options);
    }

    public static async Task<Guid> SeedTenantAsync(PlatformDbContext context, string code)
    {
      var tenant = Tenant.Create(
        TenantCode.Create($"{code}-{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create($"Tenant {code}").Value, "seed", Guid.NewGuid(), Now).Value;
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      return tenant.Id;
    }

    public static async Task<TenantDatabase> SeedDatabaseAsync(
      PlatformDbContext context,
      string serverKey,
      string name,
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      TenantDatabaseProvisioningStatus provisioningStatus = TenantDatabaseProvisioningStatus.Ready)
    {
      var database = TenantDatabase.Register(
        hostingMode, storageMode, serverKey, name, provisioningStatus, "seed", Now).Value;
      context.TenantDatabases.Add(database);
      await context.SaveChangesAsync();
      return database;
    }

    public static async Task<TenantDatabaseAssignment> SeedAssignmentAsync(
      PlatformDbContext context, Guid tenantId, long tenantDatabaseId, long routingVersion = 1)
    {
      var assignment = TenantDatabaseAssignment.Create(
        tenantId, tenantDatabaseId, routingVersion, "test", "seed", Now).Value;
      context.TenantDatabaseAssignments.Add(assignment);
      await context.SaveChangesAsync();
      return assignment;
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => RoutingSqlDatabase.Now;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "tenant-routing-tests";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }
}
