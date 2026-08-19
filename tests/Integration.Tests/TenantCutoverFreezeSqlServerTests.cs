using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE TENANT WRITE FREEZE, against real SQL (ADR-020 "Write consistency", TS-Storage Phase E1).
//
// The invariant is not "a flag says frozen". It is:
//
//   once FreezeAsync reports success, every application write already in flight for that tenant has
//   completed, and no new application write for that tenant can commit.
//
// That cannot be demonstrated with mocks, because the part that can go wrong is a RACE — a writer that
// checked "not frozen", was descheduled, and commits after the freeze returned. Every test here therefore
// uses independent connections and real transactions, and the drain tests hold a genuine open transaction
// while the freeze runs.
[Collection(TenantBackupSerialSuites.Name)]
public sealed class TenantCutoverFreezeSqlServerTests
{
  // A: writes are ordinary when no cutover holds the tenant.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_tenant_write_succeeds_when_no_cutover_holds_the_tenant()
  {
    await using var fixture = await CutoverFixture.CreateAsync();

    await using var context = fixture.TenantContext(fixture.TenantA);
    context.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "OPEN"));

    Assert.Equal(1, await context.SaveChangesAsync());
  }

  // B + C: the freeze refuses the frozen tenant and ONLY the frozen tenant. A shared database hosts
  // co-tenants, and one tenant's promotion is not an outage for them.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_frozen_tenant_is_refused_while_a_co_tenant_on_the_same_database_still_writes()
  {
    await using var fixture = await CutoverFixture.CreateAsync();
    var operationId = await fixture.BeginAndFreezeAsync(fixture.TenantA);
    Assert.True(operationId > 0);

    await using var frozen = fixture.TenantContext(fixture.TenantA);
    frozen.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "FROZEN"));
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => frozen.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);

    await using var other = fixture.TenantContext(fixture.TenantB);
    other.Companies.Add(CutoverFixture.NewCompany(fixture.TenantB, "COTENANT"));
    Assert.Equal(1, await other.SaveChangesAsync());

    // Reads are untouched: the freeze is a write fence, not an outage.
    Assert.Equal(0, await frozen.Companies.CountAsync());
  }

  // D + E: THE DRAIN, and the race it exists to close.
  //
  // Writer A opens a real transaction and holds it. The freeze must not complete while that transaction is
  // open; it must complete once it commits; and the writer that arrives afterwards must be refused.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_freeze_waits_for_an_open_write_then_fences_every_later_writer()
  {
    await using var fixture = await CutoverFixture.CreateAsync();
    var operationId = await fixture.BeginAsync(fixture.TenantA);

    // ---- WRITER A: a genuine open write transaction holding the tenant's shared fence.
    await using var writerA = fixture.TenantContext(fixture.TenantA);
    await using var transactionA = await writerA.Database.BeginTransactionAsync();
    writerA.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "INFLIGHT"));
    await writerA.SaveChangesAsync();

    // ---- FREEZE B: starts while A is still open, and must not be able to finish.
    var freeze = Task.Run(() => fixture.FreezeService().FreezeAsync(operationId));
    var blocked = await Task.WhenAny(freeze, Task.Delay(TimeSpan.FromSeconds(3)));
    Assert.NotSame(freeze, blocked);
    Assert.False(freeze.IsCompleted, "the freeze completed while an application write was still open");

    // The durable row agrees: nothing is frozen yet.
    Assert.Equal(TenantCutoverOperationStatus.Preparing, (await fixture.ReadAsync(operationId)).Status);

    // ---- A COMMITS. The drain can now finish.
    await transactionA.CommitAsync();

    var frozen = await freeze;
    Assert.True(frozen.IsSuccess);
    Assert.Equal(TenantCutoverOperationStatus.Frozen, (await fixture.ReadAsync(operationId)).Status);
    Assert.Equal(1, await fixture.CompanyCountAsync(fixture.TenantA));

    // ---- WRITER C: arrives after the freeze returned, and cannot commit.
    await using var writerC = fixture.TenantContext(fixture.TenantA);
    writerC.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "AFTER"));
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => writerC.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);
    Assert.Equal(1, await fixture.CompanyCountAsync(fixture.TenantA));

    // ---- WRITER D: another tenant, same physical database, unaffected.
    await using var writerD = fixture.TenantContext(fixture.TenantB);
    writerD.Companies.Add(CutoverFixture.NewCompany(fixture.TenantB, "OTHER"));
    Assert.Equal(1, await writerD.SaveChangesAsync());
  }

  // F: a writer that will not drain inside the budget FAILS the freeze rather than downgrading it.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_writer_that_does_not_drain_fails_the_freeze_and_leaves_writes_open()
  {
    await using var fixture = await CutoverFixture.CreateAsync(drainTimeout: TimeSpan.FromMilliseconds(750));
    var operationId = await fixture.BeginAsync(fixture.TenantA);

    await using var writer = fixture.TenantContext(fixture.TenantA);
    await using var held = await writer.Database.BeginTransactionAsync();
    writer.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "STUBBORN"));
    await writer.SaveChangesAsync();

    var frozen = await fixture.FreezeService().FreezeAsync(operationId);

    Assert.True(frozen.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverFreezeTimedOut.Code, frozen.Error.Code);

    var operation = await fixture.ReadAsync(operationId);
    // TERMINAL, AND NOT FROZEN. A partial freeze claim is the one outcome that would make the copy unsafe.
    Assert.Equal(TenantCutoverOperationStatus.Abandoned, operation.Status);
    Assert.Null(operation.FrozenUtc);
    Assert.Equal(TenantStorageErrors.CutoverFreezeTimedOut.Code, operation.FailureSummary);

    await held.CommitAsync();

    // Writes were never fenced, so the tenant carries on.
    await using var after = fixture.TenantContext(fixture.TenantA);
    after.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "CARRYON"));
    Assert.Equal(1, await after.SaveChangesAsync());
  }

  // G: release restores writes after a handled pre-flip failure, and is safe to repeat.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Releasing_a_freeze_restores_writes_and_repeats_safely()
  {
    await using var fixture = await CutoverFixture.CreateAsync();
    var operationId = await fixture.BeginAndFreezeAsync(fixture.TenantA);

    Assert.True((await fixture.FreezeService().ReleaseFreezeAsync(operationId, "copy failed")).IsSuccess);
    Assert.True((await fixture.FreezeService().ReleaseFreezeAsync(operationId, "copy failed")).IsSuccess);

    var operation = await fixture.ReadAsync(operationId);
    Assert.Equal(TenantCutoverOperationStatus.Abandoned, operation.Status);
    Assert.NotNull(operation.FreezeReleasedUtc);

    await using var writer = fixture.TenantContext(fixture.TenantA);
    writer.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "RESUMED"));
    Assert.Equal(1, await writer.SaveChangesAsync());
  }

  // H: THE PROCESS-LOSS INVARIANT. The drain lock died with the session that took it; the freeze did not.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Disposing_every_freeze_session_does_not_re_enable_writes()
  {
    await using var fixture = await CutoverFixture.CreateAsync();

    long operationId;
    {
      // A scope standing in for the cutover worker: everything it opened is disposed before the assertions,
      // so any session-scoped lock it held is gone.
      var service = fixture.FreezeService();
      operationId = await fixture.BeginAsync(fixture.TenantA);
      Assert.True((await service.FreezeAsync(operationId)).IsSuccess);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();

    // No application lock survives, and it does not matter: the durable row is the fence.
    Assert.Equal(0, await fixture.TenantApplicationLockCountAsync(fixture.TenantA));

    await using var writer = fixture.TenantContext(fixture.TenantA);
    writer.Companies.Add(CutoverFixture.NewCompany(fixture.TenantA, "AFTERCRASH"));
    var refused = await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => writer.SaveChangesAsync());
    Assert.Equal(TenantStorageErrors.TenantWritesFrozen.Code, refused.Error.Code);
  }

  // I + K: two instances racing to promote the same tenant. The database admits one.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Only_one_cutover_can_be_active_for_a_tenant()
  {
    await using var fixture = await CutoverFixture.CreateAsync();

    var attempts = await Task.WhenAll(
      Task.Run(() => fixture.Store().BeginAsync(fixture.BeginRequest(fixture.TenantA))),
      Task.Run(() => fixture.Store().BeginAsync(fixture.BeginRequest(fixture.TenantA))));

    Assert.Equal(1, attempts.Count(attempt => attempt.IsSuccess));
    var rejected = Assert.Single(attempts.Where(attempt => attempt.IsFailure));
    Assert.Equal(TenantStorageErrors.CutoverAlreadyActive.Code, rejected.Error.Code);

    Assert.Equal(1, await fixture.ActiveOperationCountAsync(fixture.TenantA));

    // ...and a sequential second attempt is refused for the same reason, which a claim on an existing row
    // could not have caught.
    var sequential = await fixture.Store().BeginAsync(fixture.BeginRequest(fixture.TenantA));
    Assert.True(sequential.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverAlreadyActive.Code, sequential.Error.Code);
  }

  // J: independent tenants promote independently — no fleet lock anywhere in this path.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Two_tenants_can_hold_cutovers_at_the_same_time()
  {
    await using var fixture = await CutoverFixture.CreateAsync();

    var first = await fixture.BeginAndFreezeAsync(fixture.TenantA);
    var second = await fixture.BeginAndFreezeAsync(fixture.TenantB);

    Assert.NotEqual(first, second);
    Assert.Equal(TenantCutoverOperationStatus.Frozen, (await fixture.ReadAsync(first)).Status);
    Assert.Equal(TenantCutoverOperationStatus.Frozen, (await fixture.ReadAsync(second)).Status);
  }

  // L: V1 promotes onto platform-managed dedicated storage and nothing else.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_customer_managed_or_shared_target_cannot_begin_a_cutover()
  {
    await using var fixture = await CutoverFixture.CreateAsync();

    var customerManaged = await fixture.Store().BeginAsync(new TenantCutoverBeginRequest(
      fixture.TenantA, fixture.SharedDatabaseId, fixture.CustomerManagedDatabaseId, "test"));
    Assert.True(customerManaged.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverTargetNotEligible.Code, customerManaged.Error.Code);

    var sharedTarget = await fixture.Store().BeginAsync(new TenantCutoverBeginRequest(
      fixture.TenantA, fixture.SharedDatabaseId, fixture.SecondSharedDatabaseId, "test"));
    Assert.True(sharedTarget.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverTargetNotEligible.Code, sharedTarget.Error.Code);

    // A source the tenant does not actually route to is refused as well.
    var wrongSource = await fixture.Store().BeginAsync(new TenantCutoverBeginRequest(
      fixture.TenantA, fixture.SecondSharedDatabaseId, fixture.DedicatedDatabaseId, "test"));
    Assert.True(wrongSource.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverSourceNotEligible.Code, wrongSource.Error.Code);

    Assert.Equal(0, await fixture.ActiveOperationCountAsync(fixture.TenantA));
  }

  private sealed class CutoverFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "cutover-freeze-tests";

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private string platformCatalog = string.Empty;
    private string sharedCatalog = string.Empty;

    public Guid TenantA { get; private set; }

    public Guid TenantB { get; private set; }

    public long SharedDatabaseId { get; private set; }

    public long SecondSharedDatabaseId { get; private set; }

    public long DedicatedDatabaseId { get; private set; }

    public long CustomerManagedDatabaseId { get; private set; }

    public static async Task<CutoverFixture> CreateAsync(TimeSpan? drainTimeout = null)
    {
      var fixture = new CutoverFixture();
      try
      {
        await fixture.InitialiseAsync(drainTimeout);
        return fixture;
      }
      catch
      {
        await fixture.DisposeAsync();
        throw;
      }
    }

    private async Task InitialiseAsync(TimeSpan? drainTimeout)
    {
      platformCatalog = $"SSAS_E1_Platform_{token}";
      sharedCatalog = $"SSAS_E1_Shared_{token}";
      freeze.DrainTimeout = drainTimeout ?? TimeSpan.FromSeconds(30);
      freeze.WriteAdmissionTimeout = TimeSpan.FromSeconds(2);

      await ExecuteSqlAsync("master", $"CREATE DATABASE [{platformCatalog}]");
      await ExecuteSqlAsync("master", $"CREATE DATABASE [{sharedCatalog}]");

      await using (var connection = new SqlConnection(ConnectionFor(sharedCatalog)))
      await using (var tenant = TenantDbContextFor(connection, null))
      {
        await tenant.Database.MigrateAsync();
      }

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      SharedDatabaseId = await RegisterAsync(platform, TenantDatabaseHostingMode.PlatformManaged,
        TenantDatabaseStorageMode.Shared, sharedCatalog);
      SecondSharedDatabaseId = await RegisterAsync(platform, TenantDatabaseHostingMode.PlatformManaged,
        TenantDatabaseStorageMode.Shared, $"SSAS_E1_SharedTwo_{token}");
      DedicatedDatabaseId = await RegisterAsync(platform, TenantDatabaseHostingMode.PlatformManaged,
        TenantDatabaseStorageMode.Dedicated, $"SSAS_E1_Dedicated_{token}");
      CustomerManagedDatabaseId = await RegisterAsync(platform, TenantDatabaseHostingMode.CustomerManaged,
        TenantDatabaseStorageMode.Dedicated, $"SSAS_E1_Customer_{token}");

      TenantA = await SeedTenantAsync(platform, "E1AAA");
      TenantB = await SeedTenantAsync(platform, "E1BBB");
    }

    private static async Task<long> RegisterAsync(
      PlatformDbContext platform,
      TenantDatabaseHostingMode hostingMode,
      TenantDatabaseStorageMode storageMode,
      string databaseName)
    {
      var database = TenantDatabase.Register(
        hostingMode, storageMode, ServerKey, databaseName,
        TenantDatabaseProvisioningStatus.Ready, Actor, Now).Value;
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    private async Task<Guid> SeedTenantAsync(PlatformDbContext platform, string code)
    {
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Phase E1 {code}").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      var assignment = TenantDatabaseAssignment
        .CreateInitial(tenant.Id, SharedDatabaseId, "phase-e1", Actor, Now).Value;
      platform.TenantDatabaseAssignments.Add(assignment);
      await platform.SaveChangesAsync();
      return tenant.Id;
    }

    // ---- THE REAL WRITE BOUNDARY, with the real fence over a real connection.
    public TenantDbContext TenantContext(Guid tenantId) =>
      TenantDbContextFor(
        new SqlConnection(ConnectionFor(sharedCatalog)),
        new TenantCutoverWriteFence(Store(), Options.Create(freeze)),
        tenantId);

    private static TenantDbContext TenantDbContextFor(
      SqlConnection connection,
      ITenantWriteFence? fence,
      Guid? tenantId = null)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connection, contextOwnsConnection: true, sql => sql.MigrationsHistoryTable(
          "__EFMigrationsHistory", "tenant"))
        .Options;

      return new TenantDbContext(
        options, new TestUser(), new TestTenant(tenantId), new TestClock(Now), fence);
    }

    public static Company NewCompany(Guid tenantId, string code) =>
      Company.Create(
        tenantId,
        CompanyCode.Create(code).Value,
        CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("USD").Value,
        Actor,
        Guid.NewGuid(),
        Now).Value;

    public TenantCutoverOperationStore Store() => new(PlatformContext(), new TestClock(Now));

    public TenantCutoverFreezeService FreezeService()
    {
      var platform = PlatformContext();
      return new TenantCutoverFreezeService(
        new TenantCutoverOperationStore(platform, new TestClock(Now)),
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        Options.Create(freeze));
    }

    public TenantCutoverBeginRequest BeginRequest(Guid tenantId) =>
      new(tenantId, SharedDatabaseId, DedicatedDatabaseId, Actor);

    public async Task<long> BeginAsync(Guid tenantId)
    {
      var begun = await Store().BeginAsync(BeginRequest(tenantId));
      Assert.True(begun.IsSuccess);
      return begun.Value;
    }

    public async Task<long> BeginAndFreezeAsync(Guid tenantId)
    {
      var operationId = await BeginAsync(tenantId);
      Assert.True((await FreezeService().FreezeAsync(operationId)).IsSuccess);
      return operationId;
    }

    public async Task<TenantCutoverOperationRecord> ReadAsync(long operationId)
    {
      var operation = await Store().FindAsync(operationId);
      Assert.NotNull(operation);
      return operation!;
    }

    public async Task<int> CompanyCountAsync(Guid tenantId)
    {
      await using var context = TenantDbContextFor(
        new SqlConnection(ConnectionFor(sharedCatalog)), null, tenantId);
      return await context.Companies.CountAsync();
    }

    public Task<int> ActiveOperationCountAsync(Guid tenantId) => ScalarAsync(
      "SELECT COUNT(*) FROM [platform].[TenantCutoverOperations] " +
      $"WHERE [TenantId] = '{tenantId:D}' AND [Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped')");

    // Application locks currently held on the SHARED database for this tenant's cutover resource.
    public async Task<int> TenantApplicationLockCountAsync(Guid tenantId)
    {
      await using var connection = new SqlConnection(NonPooled(ConnectionFor(sharedCatalog)));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE [resource_type] = N'APPLICATION' " +
        "AND [resource_description] LIKE @resource + N'%'";
      command.Parameters.AddWithValue("@resource", TenantCutoverLockResource.ForTenant(tenantId));
      return Convert.ToInt32(await command.ExecuteScalarAsync(),
        System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<int> ScalarAsync(string sql)
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
      return new PlatformDbContext(options, new TestUser(), new TestTenant(null), new TestClock(Now));
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { sharedCatalog, platformCatalog }
        .Where(value => !string.IsNullOrWhiteSpace(value)))
      {
        try
        {
          await ExecuteSqlAsync("master",
            $"IF DB_ID(N'{catalog}') IS NOT NULL BEGIN " +
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{catalog}]; END");
        }
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(catalog, error);
        }
      }
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog, Pooling = false }
        .ConnectionString;

    private static string NonPooled(string connectionString) =>
      new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

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
    public string? UserId => "cutover-freeze-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }
}
