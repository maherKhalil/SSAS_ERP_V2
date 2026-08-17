using System.Globalization;
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
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// CONVERGENCE WITHOUT A TRANSPORT, against real SQL (ADR-020 "Resolver cache", TS-Storage Phase E2).
//
// The claim E2 rests on is that a process which is told NOTHING about a cutover still stops using the old
// route. Proving that needs two things a unit test cannot supply: a genuinely shared authoritative registry
// (a real Platform database, written by a connection the resolver does not own) and the real narrow version
// query running against the real index. Both are here.
//
// The instances below deliberately hold SEPARATE PlatformDbContexts and SEPARATE caches. That is the
// property under test — one process cannot reach into another's memory, and nothing in this file simulates
// a message ever being delivered.
public sealed class TenantRoutingConvergenceSqlServerTests(Xunit.Abstractions.ITestOutputHelper output)
{
  // ---- §9. The scenario ADR-020 describes, end to end.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task An_instance_that_receives_no_invalidation_converges_on_the_new_route()
  {
    await using var fixture = await ConvergenceFixture.CreateAsync();
    var tenantId = await fixture.RegisterSharedTenantAsync("CONVERGE");

    // Instance A resolves and caches Shared / version 1.
    await using var instanceA = fixture.NewInstance();
    var before = await instanceA.Resolver.ResolveAsync(tenantId);
    Assert.True(before.IsSuccess);
    Assert.Equal(fixture.SharedCatalog, before.Value.DatabaseName);
    Assert.Equal(TenantDatabaseStorageMode.Shared, before.Value.StorageMode);
    Assert.Equal(1, before.Value.RoutingVersion);
    Assert.Equal(1, instanceA.Cache.Count);

    // The cutover commits through a COMPLETELY INDEPENDENT Platform context — the shape a flip executed by
    // another node would take. Instance A is not notified, and its cache is deliberately left populated.
    var flipped = await fixture.FlipToDedicatedAsync(tenantId);
    Assert.Equal(2, flipped);
    Assert.Equal(1, instanceA.Cache.Count);
    Assert.True(instanceA.Cache.TryGet(tenantId, out var stale));
    Assert.Equal(fixture.SharedCatalog, stale.Route.DatabaseName);

    // ...and the very next resolution converges, because the version check is on the path.
    var after = await instanceA.Resolver.ResolveAsync(tenantId);

    Assert.True(after.IsSuccess);
    Assert.Equal(fixture.DedicatedCatalog, after.Value.DatabaseName);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, after.Value.StorageMode);
    Assert.Equal(2, after.Value.RoutingVersion);

    // The stale entry was replaced rather than merely bypassed.
    Assert.True(instanceA.Cache.TryGet(tenantId, out var refreshed));
    Assert.Equal(fixture.DedicatedCatalog, refreshed.Route.DatabaseName);
    Assert.Equal(2, refreshed.RoutingVersion);
  }

  // The same convergence, but observed where it actually matters: which physical catalog a routed
  // TenantDbContext lands in. A route object that changed while the connection did not would be worthless.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_routed_tenant_context_reaches_the_new_catalog_after_a_cutover_it_was_not_told_about()
  {
    await using var fixture = await ConvergenceFixture.CreateAsync();
    var tenantId = await fixture.RegisterSharedTenantAsync("CTXCONV");

    await using var instanceA = fixture.NewInstance();
    var factory = ConvergenceFixture.FactoryFor(instanceA, tenantId);

    await using (var context = (await factory.CreateAsync(tenantId)).Value)
    {
      Assert.Equal(fixture.SharedCatalog, await CurrentCatalogAsync(context));
    }

    Assert.Equal(1, instanceA.Cache.Count);
    await fixture.FlipToDedicatedAsync(tenantId);

    // No invalidation. A new context on the SAME instance nonetheless opens against the dedicated catalog.
    await using (var context = (await factory.CreateAsync(tenantId)).Value)
    {
      Assert.Equal(fixture.DedicatedCatalog, await CurrentCatalogAsync(context));
    }
  }

  // A cached route is not consulted when the authoritative version cannot be read — proven against a real
  // unreachable Platform catalog rather than a stub that returns a failure on request.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_real_platform_outage_refuses_to_route_rather_than_serving_the_cached_route()
  {
    await using var fixture = await ConvergenceFixture.CreateAsync();
    var tenantId = await fixture.RegisterSharedTenantAsync("OUTAGE");

    await using var instanceA = fixture.NewInstance();
    Assert.True((await instanceA.Resolver.ResolveAsync(tenantId)).IsSuccess);
    Assert.Equal(1, instanceA.Cache.Count);

    // The same cache and the same inner resolver, but a version reader whose Platform catalog does not
    // exist. This is the "Platform DB unavailable" case, and the cached Shared route is sitting right there.
    var unavailable = fixture.NewInstanceWithUnreachableVersionReader(instanceA.Cache);
    await using var outage = unavailable;

    var result = await outage.Resolver.ResolveAsync(tenantId);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RoutingVersionUnavailable.Code, result.Error.Code);

    // The entry survives the outage — a transient failure is not evidence that routing moved.
    Assert.Equal(1, instanceA.Cache.Count);
  }

  // ---- §10. The version read is a seek on the index the assignment invariant already maintains, so E2 adds
  // no index and no write cost. Asserted from the real plan, not from the query's shape.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task The_version_read_seeks_the_existing_active_assignment_index()
  {
    await using var fixture = await ConvergenceFixture.CreateAsync();
    var tenantId = await fixture.RegisterSharedTenantAsync("PLAN");

    // AT REALISTIC CARDINALITY, because a plan taken from a one-row table proves nothing: SQL Server will
    // trivially scan a single page rather than seek, and the resulting plan says more about the fixture than
    // about the design. A few thousand routed tenants is the estate this query has to hold up in.
    await fixture.SeedRoutedTenantsAsync(2_000);
    await fixture.RefreshStatisticsAndPlanCacheAsync();

    await using var instance = fixture.NewInstance();
    for (var attempt = 0; attempt < 5; attempt++)
    {
      Assert.True((await instance.Versions.ReadCurrentRoutingVersionAsync(tenantId)).IsSuccess);
    }

    var measured = await fixture.CaptureVersionQueryPlanAsync();

    Assert.NotNull(measured);
    output.WriteLine(
      $"Version read over {measured!.TableCardinality} assignment rows: " +
      $"{measured.LogicalReadsPerExecution} logical reads/execution across {measured.ExecutionCount} executions, " +
      $"{measured.MicrosecondsPerExecution}us/execution, subtree cost {measured.SubtreeCost}.");
    Assert.True(
      measured.PlanXml.Contains("UX_TenantDatabaseAssignments_ActiveTenant", StringComparison.Ordinal),
      $"Version-read plan did not seek the existing active-assignment index.\n{measured.PlanXml}");
    Assert.True(
      measured.PlanXml.Contains("PhysicalOp=\"Index Seek\"", StringComparison.Ordinal),
      $"Version-read plan was not a seek.\n{measured.PlanXml}");

    // NO SCAN. Asserted on PhysicalOp rather than on the word "Scan": showplan wraps a seek in an
    // <IndexScan> element too, so a substring check would pass for the plan this is meant to catch.
    foreach (var scan in new[] { "Clustered Index Scan", "Index Scan", "Table Scan" })
    {
      Assert.DoesNotContain($"PhysicalOp=\"{scan}\"", measured.PlanXml, StringComparison.Ordinal);
    }

    // ...and no join to the registry. The wide read is exactly what the cache exists to avoid, so a version
    // check that dragged it back in would cost what it was meant to save.
    Assert.DoesNotContain("TenantDatabases", measured.PlanXml, StringComparison.Ordinal);

    // Reported rather than asserted on a threshold: absolute timings vary by machine, but the shape of the
    // work does not. A seek returning at most one row is a handful of logical reads.
    Assert.True(
      measured.LogicalReadsPerExecution <= 20,
      $"Version read averaged {measured.LogicalReadsPerExecution} logical reads per execution, " +
      "which is not the seek this design assumes.");
  }

  private static async Task<string?> CurrentCatalogAsync(TenantDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT DB_NAME()";
    return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
  }

  private sealed record MeasuredPlan(
    string PlanXml,
    long LogicalReadsPerExecution,
    long MicrosecondsPerExecution,
    long ExecutionCount)
  {
    // Read back out of the plan rather than counted separately, so the reported cardinality is the one the
    // optimizer actually compiled against.
    public string TableCardinality => Attribute("TableCardinality=\"");

    public string SubtreeCost => Attribute("StatementSubTreeCost=\"");

    private string Attribute(string marker)
    {
      var start = PlanXml.IndexOf(marker, StringComparison.Ordinal);
      if (start < 0)
      {
        return "unknown";
      }

      start += marker.Length;
      var end = PlanXml.IndexOf('"', start);
      return end < 0 ? "unknown" : PlanXml[start..end];
    }
  }

  // One real Platform catalog holding the registry, plus two real tenant catalogs so "the route changed"
  // can be checked by asking SQL Server which database a connection is actually in.
  private sealed class ConvergenceFixture : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 16, 11, 0, 0, TimeSpan.Zero);

    private const string PrimaryServerKey = "PrimarySqlServer";

    private readonly string platformCatalog;

    private ConvergenceFixture(string platformCatalog, string sharedCatalog, string dedicatedCatalog)
    {
      this.platformCatalog = platformCatalog;
      SharedCatalog = sharedCatalog;
      DedicatedCatalog = dedicatedCatalog;
    }

    public string SharedCatalog { get; }

    public string DedicatedCatalog { get; }

    public static async Task<ConvergenceFixture> CreateAsync()
    {
      var fixture = new ConvergenceFixture(
        $"SSAS_ERP_E2_PLATFORM_{Guid.NewGuid():N}",
        $"SSAS_ERP_E2_SHARED_{Guid.NewGuid():N}",
        $"SSAS_ERP_E2_DEDICATED_{Guid.NewGuid():N}");

      try
      {
        await using (var platform = fixture.PlatformContext())
        {
          await platform.Database.MigrateAsync();
        }

        foreach (var catalog in new[] { fixture.SharedCatalog, fixture.DedicatedCatalog })
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

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string CatalogConnection(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    public PlatformDbContext PlatformContext() => PlatformContextFor(platformCatalog);

    private static PlatformDbContext PlatformContextFor(string catalog)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(CatalogConnection(catalog), sql => sql.MigrationsHistoryTable(
          "__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    // A separate "process": its own Platform context, its own registry read path, its own cache.
    public ResolverInstance NewInstance()
    {
      var context = PlatformContext();
      var cache = new TenantRoutingMemoryCache();
      var versions = new TenantRoutingVersionReader(context);
      return new ResolverInstance(context, cache, versions, Compose(context, versions, cache));
    }

    // The same cache and the same authoritative inner resolver, but a version reader pointed at a catalog
    // that does not exist. Nothing is stubbed: the query really fails.
    public ResolverInstance NewInstanceWithUnreachableVersionReader(TenantRoutingMemoryCache cache)
    {
      var context = PlatformContext();
      var unreachable = PlatformContextFor($"SSAS_ERP_E2_ABSENT_{Guid.NewGuid():N}");
      var versions = new TenantRoutingVersionReader(unreachable);
      return new ResolverInstance(context, cache, versions, Compose(context, versions, cache), unreachable);
    }

    private static VersionAwareTenantDatabaseResolver Compose(
      PlatformDbContext context, TenantRoutingVersionReader versions, TenantRoutingMemoryCache cache) =>
      new(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(context)),
        versions,
        cache,
        new TenantRoutingCacheOptions { Lifetime = TimeSpan.FromMinutes(10) },
        new TestClock());

    // The real routed-context path, over the instance's version-aware resolver.
    public static TenantDbContextFactory FactoryFor(ResolverInstance instance, Guid tenantId) =>
      new(
        instance.Resolver,
        ConnectionFactory(),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(),
        new FixedTenant(tenantId),
        new TestClock(),
        UnfencedTenantWrites.Instance);

    private static TenantDatabaseConnectionFactory ConnectionFactory()
    {
      var options = Options.Create(new TenantStorageOptions());
      options.Value.Servers[PrimaryServerKey] = new TenantStorageServerOptions
      {
        ConnectionString = CatalogConnection("master")
      };
      return new TenantDatabaseConnectionFactory(options);
    }

    public async Task<Guid> RegisterSharedTenantAsync(string code)
    {
      await using var platform = PlatformContext();
      var tenant = Tenant.Create(
        TenantCode.Create($"{code}-{Guid.NewGuid():N}"[..12]).Value,
        TenantName.Create($"Tenant {code}").Value, "e2-tests", Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      var shared = Register(TenantDatabaseStorageMode.Shared, SharedCatalog);
      platform.TenantDatabases.Add(shared);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.Create(tenant.Id, shared.Id, 1, "e2-tests", "e2-tests", Now).Value);
      await platform.SaveChangesAsync();
      return tenant.Id;
    }

    // The registry shape a routing flip produces: end the active assignment, insert the next one at
    // RoutingVersion + 1. Deliberately hand-written rather than calling a flip service — E2 does not
    // implement the flip, and this test is about what a resolver does once one has happened.
    public async Task<long> FlipToDedicatedAsync(Guid tenantId)
    {
      await using var platform = PlatformContext();

      var dedicated = await platform.TenantDatabases
        .SingleOrDefaultAsync(database => database.DatabaseName == DedicatedCatalog);
      if (dedicated is null)
      {
        dedicated = Register(TenantDatabaseStorageMode.Dedicated, DedicatedCatalog);
        platform.TenantDatabases.Add(dedicated);
        await platform.SaveChangesAsync();
      }

      var current = await platform.TenantDatabaseAssignments
        .SingleAsync(assignment => assignment.TenantId == tenantId && assignment.EndedUtc == null);
      Assert.True(current.End("e2-tests", Now.AddMinutes(1)).IsSuccess);

      var next = current.RoutingVersion + 1;
      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.Create(
          tenantId, dedicated.Id, next, "e2-tests", "e2-tests", Now.AddMinutes(1)).Value);
      await platform.SaveChangesAsync();
      return next;
    }

    // ADR-018 gating denies traffic to an unverified database, so a database that is expected to serve must
    // carry a health observation. Gating itself is proven elsewhere.
    private static TenantDatabase Register(TenantDatabaseStorageMode storageMode, string catalog)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, storageMode, PrimaryServerKey, catalog,
        TenantDatabaseProvisioningStatus.Ready, "e2-tests", Now).Value;
      database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, "e2-tests", Now);
      database.RecordSchemaHealth(
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, null, null, "e2-tests", Now);
      return database;
    }

    // Background population: tenants that are routed somewhere, so the assignment table has the shape a real
    // estate gives it. All of them point at the same registered database — the row count and the index
    // selectivity are what the optimizer is deciding against, not the placement variety.
    public async Task SeedRoutedTenantsAsync(int count)
    {
      await using var platform = PlatformContext();
      platform.ChangeTracker.AutoDetectChangesEnabled = false;

      var databaseId = await platform.TenantDatabases
        .Where(database => database.DatabaseName == SharedCatalog)
        .Select(database => database.Id)
        .SingleAsync();

      for (var index = 0; index < count; index++)
      {
        var tenant = Tenant.Create(
          TenantCode.Create($"BULK{index:D8}").Value,
          TenantName.Create($"Bulk Tenant {index}").Value, "e2-tests", Guid.NewGuid(), Now).Value;
        platform.Tenants.Add(tenant);
        platform.TenantDatabaseAssignments.Add(
          TenantDatabaseAssignment.Create(tenant.Id, databaseId, 1, "e2-tests", "e2-tests", Now).Value);
      }

      await platform.SaveChangesAsync();
    }

    // A fair measurement needs current statistics and a fresh compilation. Both are scoped to this test's
    // own database — nothing here touches the server-wide plan cache.
    public async Task RefreshStatisticsAndPlanCacheAsync()
    {
      await using var connection = new SqlConnection(CatalogConnection(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = """
        UPDATE STATISTICS [platform].[TenantDatabaseAssignments] WITH FULLSCAN;
        ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
        """;
      await command.ExecuteNonQueryAsync();
    }

    // The plan for the NARROW version query, taken from the server's own statistics. Identified by the
    // columns it projects and by the absence of the registry join, so it cannot be confused with the wide
    // route read. CHARINDEX rather than LIKE: bracketed identifiers are LIKE wildcards.
    public async Task<MeasuredPlan?> CaptureVersionQueryPlanAsync()
    {
      await using var connection = new SqlConnection(CatalogConnection(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = """
        SELECT TOP (1)
          CAST(qp.query_plan AS nvarchar(max)) AS PlanXml,
          qs.total_logical_reads AS LogicalReads,
          qs.total_elapsed_time AS ElapsedMicroseconds,
          qs.execution_count AS Executions
        FROM sys.dm_exec_query_stats AS qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
        CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) AS qp
        WHERE CHARINDEX(N'[TenantDatabaseAssignments]', st.text) > 0
          AND CHARINDEX(N'[RoutingVersion]', st.text) > 0
          AND CHARINDEX(N'[TenantDatabases]', st.text) = 0
        ORDER BY qs.last_execution_time DESC;
        """;

      await using var reader = await command.ExecuteReaderAsync();
      if (!await reader.ReadAsync())
      {
        return null;
      }

      var executions = reader.GetInt64(3);
      var divisor = executions == 0 ? 1 : executions;
      return new MeasuredPlan(
        reader.GetString(0),
        reader.GetInt64(1) / divisor,
        reader.GetInt64(2) / divisor,
        executions);
    }

    private static TenantDbContext TenantContext(string catalog)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(CatalogConnection(catalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(options, new TestUser(), new NoTenant(), new TestClock());
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { SharedCatalog, DedicatedCatalog, platformCatalog })
      {
        try
        {
          await using var context = TenantContext(catalog);
          await context.Database.EnsureDeletedAsync();
        }
        catch (SqlException)
        {
          // A catalog that was never created is not a failure worth masking the real one for.
        }
      }
    }
  }

  private sealed class ResolverInstance(
    PlatformDbContext context,
    TenantRoutingMemoryCache cache,
    TenantRoutingVersionReader versions,
    VersionAwareTenantDatabaseResolver resolver,
    PlatformDbContext? secondaryContext = null) : IAsyncDisposable
  {
    public TenantRoutingMemoryCache Cache => cache;

    public TenantRoutingVersionReader Versions => versions;

    public VersionAwareTenantDatabaseResolver Resolver => resolver;

    public async ValueTask DisposeAsync()
    {
      await context.DisposeAsync();
      if (secondaryContext is not null)
      {
        await secondaryContext.DisposeAsync();
      }
    }
  }

  private sealed class TestUser : ICurrentUser
  {
    public string? UserId => "e2-tests";
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

  private sealed class FixedTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => ConvergenceFixture.Now;
  }
}
