using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE load-bearing proof of this slice (ADR-017): TenantDbContext actually routes through TS-1C, against
// real SQL and real physical databases.
//
// Everything else in the slice is arrangement; this is the property that matters. Tenant A's writes must
// land in tenant A's database and be absent from tenant B's, and that is asserted by querying each
// physical catalog directly rather than by trusting the routing metadata that produced the connection.
public sealed class TenantDbContextRoutingSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Each_tenant_writes_and_reads_only_its_own_physical_database()
  {
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantA = await fixture.RegisterTenantAsync("ROUTE-A", fixture.DatabaseA);
    var tenantB = await fixture.RegisterTenantAsync("ROUTE-B", fixture.DatabaseB);

    // Tenant A writes.
    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      Assert.Equal(fixture.DatabaseA, await CurrentCatalogAsync(context));
      context.Companies.Add(Company(tenantA, "ACME-A", "Acme A"));
      await context.SaveChangesAsync();
    }

    // The row exists in catalog A and NOT in catalog B — read straight from each physical database, with
    // no query filter and no routing involved, so this cannot be satisfied by filtering alone.
    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));
    Assert.Equal(0, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseB));

    // Tenant B writes.
    await using (var context = await fixture.CreateContextAsync(tenantB))
    {
      Assert.Equal(fixture.DatabaseB, await CurrentCatalogAsync(context));
      context.Companies.Add(Company(tenantB, "ACME-B", "Acme B"));
      await context.SaveChangesAsync();
    }

    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));
    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseB));

    // Each tenant reads only its own row back through its own route.
    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      var company = await context.Companies.SingleAsync();
      Assert.Equal("ACME-A", company.CompanyCode.Value);
      Assert.Equal(tenantA, company.TenantId);
    }

    await using (var context = await fixture.CreateContextAsync(tenantB))
    {
      var company = await context.Companies.SingleAsync();
      Assert.Equal("ACME-B", company.CompanyCode.Value);
      Assert.Equal(tenantB, company.TenantId);
    }
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Two_tenants_sharing_one_physical_database_stay_logically_isolated()
  {
    // Shared placement is where logical isolation is the ONLY isolation, so the filter and the write guard
    // are load-bearing rather than defence in depth.
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantA = await fixture.RegisterTenantAsync("SHARED-A", fixture.DatabaseA);
    var tenantB = await fixture.RegisterTenantAsync("SHARED-B", fixture.DatabaseA);

    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      context.Companies.Add(Company(tenantA, "SHARED-A", "Shared A"));
      await context.SaveChangesAsync();
    }

    await using (var context = await fixture.CreateContextAsync(tenantB))
    {
      context.Companies.Add(Company(tenantB, "SHARED-B", "Shared B"));
      await context.SaveChangesAsync();
    }

    // Both rows are physically in the same catalog...
    Assert.Equal(2, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));

    // ...but neither tenant can see the other's.
    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      Assert.Equal("SHARED-A", (await context.Companies.SingleAsync()).CompanyCode.Value);
      Assert.Equal(2, await context.Companies.IgnoreQueryFilters().CountAsync());
    }

    // The write guard rejects creating a row owned by another tenant, loudly rather than silently.
    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      context.Companies.Add(Company(tenantB, "CROSS", "Cross Tenant"));
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    // And rejects re-owning an existing row.
    await using (var context = await fixture.CreateContextAsync(tenantA))
    {
      var company = await context.Companies.SingleAsync();
      context.Entry(company).Property(item => item.TenantId).CurrentValue = tenantB;
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    Assert.Equal(2, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_dedicated_placement_uses_the_identical_context_and_model()
  {
    // Dedicated provisioning does not exist yet; what is proven here is that the CONTEXT is
    // topology-invariant, so adding provisioning later needs no model or context change.
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync(
      "DEDICATED", fixture.DatabaseB, storageMode: TenantDatabaseStorageMode.Dedicated);

    await using var context = await fixture.CreateContextAsync(tenantId);
    Assert.Equal(fixture.DatabaseB, await CurrentCatalogAsync(context));

    context.Companies.Add(Company(tenantId, "DED", "Dedicated Company"));
    await context.SaveChangesAsync();

    // TenantId is retained and the filter still applies, even though this database holds one tenant.
    var company = await context.Companies.SingleAsync();
    Assert.Equal(tenantId, company.TenantId);
    Assert.NotNull(context.Model.FindEntityType(typeof(Company))!.GetQueryFilter());
    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseB));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_new_context_observes_a_changed_assignment_because_nothing_is_cached()
  {
    // Routing is resolved per context creation. Re-pointing the assignment must change where the NEXT
    // context writes, without any cache invalidation step existing.
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync("REROUTE", fixture.DatabaseA);

    await using (var context = await fixture.CreateContextAsync(tenantId))
    {
      Assert.Equal(fixture.DatabaseA, await CurrentCatalogAsync(context));
      context.Companies.Add(Company(tenantId, "BEFORE", "Before Move"));
      await context.SaveChangesAsync();
    }

    var routingVersion = await fixture.ReassignAsync(tenantId, fixture.DatabaseB);
    Assert.Equal(2, routingVersion);

    await using (var context = await fixture.CreateContextAsync(tenantId))
    {
      Assert.Equal(fixture.DatabaseB, await CurrentCatalogAsync(context));
      context.Companies.Add(Company(tenantId, "AFTER", "After Move"));
      await context.SaveChangesAsync();
      Assert.Equal("AFTER", (await context.Companies.SingleAsync()).CompanyCode.Value);
    }

    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));
    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseB));

    // The new RoutingVersion reaches the route unchanged.
    var route = await fixture.ResolveAsync(tenantId);
    Assert.True(route.IsSuccess);
    Assert.Equal(2, route.Value.RoutingVersion);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_live_context_keeps_its_database_when_routing_changes_underneath_it()
  {
    // Context lifetime is fixed once created. Retargeting a live unit of work would move an open
    // transaction to a different database mid-flight.
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync("LIVE", fixture.DatabaseA);

    await using var context = await fixture.CreateContextAsync(tenantId);
    Assert.Equal(fixture.DatabaseA, await CurrentCatalogAsync(context));

    await fixture.ReassignAsync(tenantId, fixture.DatabaseB);

    context.Companies.Add(Company(tenantId, "LIVE", "Live Context"));
    await context.SaveChangesAsync();

    Assert.Equal(fixture.DatabaseA, await CurrentCatalogAsync(context));
    Assert.Equal(1, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseA));
    Assert.Equal(0, await RoutingFixture.RawCompanyCountAsync(fixture.DatabaseB));
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_tenant_without_an_active_assignment_cannot_obtain_a_context()
  {
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.SeedTenantAsync("NOROUTE");

    var created = await fixture.Factory().CreateAsync(tenantId);

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.ActiveAssignmentMissing.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task An_unknown_server_key_cannot_obtain_a_context_and_never_falls_back()
  {
    // The critical no-fallback case, now that a context — and therefore real reads and writes — depends on
    // it. Failing here must never degrade into the Platform connection.
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync("BADKEY", fixture.DatabaseA, serverKey: "UnconfiguredServer");

    var created = await fixture.Factory().CreateAsync(tenantId);

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_database_that_is_not_ready_cannot_obtain_a_context()
  {
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync(
      "NOTREADY", fixture.DatabaseA, provisioningStatus: TenantDatabaseProvisioningStatus.Provisioning);

    var created = await fixture.Factory().CreateAsync(tenantId);

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseNotReady.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public async Task A_customer_managed_placement_cannot_obtain_a_context()
  {
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync(
      "CUSTOMER", "CustomerERP",
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated,
      serverKey: "CustomerServer");

    var created = await fixture.Factory().CreateAsync(tenantId);

    Assert.True(created.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, created.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Repositories_fail_loudly_rather_than_returning_empty_when_routing_fails()
  {
    // The misrouting asymmetry made explicit: a read that returned null here would be indistinguishable
    // from "this tenant has no companies".
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.SeedTenantAsync("LOUD");
    var provider = fixture.Provider(tenantId);

    await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => new CompanyRepository(provider).GetByIdAsync(Guid.NewGuid()));
    await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => new CompanyReadService(provider).ListAsync(null, 1, 20));

    // And with no trusted tenant at all.
    var tenantless = fixture.Provider(null);
    var failure = await tenantless.ResolveAsync();
    Assert.True(failure.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantContextMissing.Code, failure.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task The_migration_runner_reports_the_tenant_streams_applied_state_for_one_tenant()
  {
    await using var fixture = await RoutingFixture.CreateAsync();
    var tenantId = await fixture.RegisterTenantAsync("MIGRATE", fixture.DatabaseA);
    var runner = new TenantMigrationRunner(fixture.Factory());

    var pending = await runner.GetPendingMigrationsAsync(tenantId);
    Assert.True(pending.IsSuccess);
    Assert.Empty(pending.Value);

    var applied = await runner.GetAppliedMigrationsAsync(tenantId);
    Assert.True(applied.IsSuccess);
    Assert.Contains(applied.Value, id => id.EndsWith("AddTenantCompanyOrganization", StringComparison.Ordinal));

    // Routing applies to tooling too: an unroutable tenant gets no migration access.
    var unroutable = await runner.GetPendingMigrationsAsync(await fixture.SeedTenantAsync("UNROUTED"));
    Assert.True(unroutable.IsFailure);
    Assert.Equal(TenantStorageErrors.ActiveAssignmentMissing.Code, unroutable.Error.Code);
  }

  private static Company Company(Guid tenantId, string code, string name) =>
    SSAS.Platform.Domain.Companies.Company.Create(
      tenantId,
      CompanyCode.Create(code).Value,
      CompanyName.Create(name).Value,
      BaseCurrencyCode.Create("USD").Value,
      "routing-tests",
      Guid.NewGuid(),
      RoutingFixture.Now).Value;

  private static async Task<string?> CurrentCatalogAsync(TenantDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";
    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  // Three real catalogs: one Platform database holding the registry, and two physical tenant databases.
  // That separation is what makes the cross-database assertions meaningful — the registry cannot be
  // confused with the data it routes to.
  private sealed class RoutingFixture : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 14, 11, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private readonly string platformCatalog;

    private RoutingFixture(string platformCatalog, string databaseA, string databaseB)
    {
      this.platformCatalog = platformCatalog;
      DatabaseA = databaseA;
      DatabaseB = databaseB;
    }

    public string DatabaseA { get; }

    public string DatabaseB { get; }

    public static async Task<RoutingFixture> CreateAsync()
    {
      var fixture = new RoutingFixture(
        $"SSAS_ERP_ROUTE_PLATFORM_{Guid.NewGuid():N}",
        $"SSAS_ERP_ROUTE_A_{Guid.NewGuid():N}",
        $"SSAS_ERP_ROUTE_B_{Guid.NewGuid():N}");

      try
      {
        await using (var platform = fixture.PlatformContext())
        {
          await platform.Database.MigrateAsync();
        }

        foreach (var catalog in new[] { fixture.DatabaseA, fixture.DatabaseB })
        {
          await using var tenant = TenantContext(catalog);
          await tenant.Database.MigrateAsync();
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

    public PlatformDbContext PlatformContext(Guid? currentTenantId = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(CatalogConnection(platformCatalog), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new FixedTenant(currentTenantId), new TestClock());
    }

    // The trusted server configuration used by the connection factory. The base string names `master`, so
    // only the route's DatabaseName decides which catalog is reached.
    public static TenantDatabaseConnectionFactory ConnectionFactory()
    {
      var options = Options.Create(new TenantStorageOptions());
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions
      {
        ConnectionString = CatalogConnection("master")
      };
      return new TenantDatabaseConnectionFactory(options);
    }

    public TenantDbContextFactory Factory(Guid? currentTenantId = null) =>
      new(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(PlatformContext())),
        ConnectionFactory(),
        new TestUser(),
        new FixedTenant(currentTenantId),
        new TestClock());

    public TenantDbContextProvider Provider(Guid? currentTenantId) =>
      new(Factory(currentTenantId), new FixedTenant(currentTenantId));

    public Task<TenantDbContext> CreateContextAsync(Guid tenantId) =>
      Provider(tenantId).GetRequiredAsync();

    public async Task<SSAS.BuildingBlocks.Domain.Result<TenantDatabaseRoute>> ResolveAsync(Guid tenantId)
    {
      await using var platform = PlatformContext();
      return await new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)).ResolveAsync(tenantId);
    }

    public async Task<Guid> SeedTenantAsync(string code)
    {
      await using var platform = PlatformContext();
      var tenant = Tenant.Create(
        TenantCode.Create($"{code}-{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create($"Tenant {code}").Value, "routing-tests", Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();
      return tenant.TenantId;
    }

    public async Task<Guid> RegisterTenantAsync(
      string code,
      string databaseName,
      string serverKey = PrimaryServerKey,
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      TenantDatabaseProvisioningStatus provisioningStatus = TenantDatabaseProvisioningStatus.Ready)
    {
      var tenantId = await SeedTenantAsync(code);
      await using var platform = PlatformContext();
      var existing = await platform.TenantDatabases
        .SingleOrDefaultAsync(database => database.ServerKey == serverKey && database.DatabaseName == databaseName);
      if (existing is null)
      {
        existing = TenantDatabase.Register(
          hostingMode, storageMode, serverKey, databaseName, provisioningStatus, "routing-tests", Now).Value;
        platform.TenantDatabases.Add(existing);
        await platform.SaveChangesAsync();
      }

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.Create(tenantId, existing.Id, 1, "routing-tests", "routing-tests", Now).Value);
      await platform.SaveChangesAsync();
      return tenantId;
    }

    // Ends the active assignment and creates a new one at RoutingVersion + 1 — the shape a real cutover
    // would take, without implementing the cutover service this slice excludes.
    public async Task<long> ReassignAsync(Guid tenantId, string databaseName)
    {
      await using var platform = PlatformContext();
      var current = await platform.TenantDatabaseAssignments
        .SingleAsync(assignment => assignment.TenantId == tenantId && assignment.EndedUtc == null);
      Assert.True(current.End("routing-tests", Now.AddMinutes(1)).IsSuccess);

      var target = await platform.TenantDatabases
        .SingleOrDefaultAsync(database => database.DatabaseName == databaseName);
      if (target is null)
      {
        // The destination catalog exists physically but has no registry row until something routes to it.
        target = TenantDatabase.Register(
          TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared,
          PrimaryServerKey, databaseName, TenantDatabaseProvisioningStatus.Ready, "routing-tests", Now).Value;
        platform.TenantDatabases.Add(target);
        await platform.SaveChangesAsync();
      }

      var next = current.RoutingVersion + 1;
      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.Create(tenantId, target.Id, next, "routing-tests", "routing-tests", Now.AddMinutes(1)).Value);
      await platform.SaveChangesAsync();
      return next;
    }

    // Reads a physical catalog directly, bypassing EF, routing and the query filter entirely. This is what
    // makes the isolation assertions evidence rather than restatement.
    public static async Task<int> RawCompanyCountAsync(string catalog)
    {
      await using var connection = new SqlConnection(CatalogConnection(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT COUNT(*) FROM [tenant].[Companies]";
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { DatabaseA, DatabaseB, platformCatalog })
      {
        try
        {
          await using var context = RoutingFixture.TenantContext(catalog);
          await context.Database.EnsureDeletedAsync();
        }
        catch (SqlException)
        {
          // A catalog that was never created is not a failure worth masking the real one for.
        }
      }
    }

    private static TenantDbContext TenantContext(string catalog)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(CatalogConnection(catalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(options, new TestUser(), new FixedTenant(null), new TestClock());
    }

    private static string CatalogConnection(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "routing-tests";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class FixedTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => RoutingFixture.Now;
  }
}
