using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

public sealed class PlatformTenantLifecycleSqlServerTests
{
  private const string PreviousMigration = "20260731200122_AddAuthenticationCredentialLifecycle";

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0304")]
  [Trait("Scenario", "TS-TEN-0020")]
  public async Task Complete_migration_chain_enforces_tenant_sql_server_invariants()
  {
    await using var database = await TenantSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    var entity = context.Model.FindEntityType(typeof(Tenant));
    Assert.NotNull(entity);
    Assert.Null(entity.GetQueryFilter());
    Assert.Null(entity.FindProperty(nameof(Tenant.TenantId)));
    Assert.Equal("TenantId", entity.FindProperty(nameof(Tenant.Id))?.GetColumnName());
    Assert.True(entity.FindProperty(nameof(Tenant.RowVersion))?.IsConcurrencyToken);
    Assert.Equal("rowversion", entity.FindProperty(nameof(Tenant.RowVersion))?.GetColumnType());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(Tenant).GetInterfaces());
    Assert.Equal(
      [
        "TenantId", "TenantCode", "NormalizedTenantCode", "TenantName", "Status", "CreatedUtc", "CreatedBy",
        "ModifiedUtc", "ModifiedBy", "StatusChangedUtc", "StatusChangedBy", "StatusChangeReasonCode", "RowVersion"
      ],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'Tenants' ORDER BY ORDINAL_POSITION"));
    Assert.Equal("uniqueidentifier", await ReadStringAsync(
      context,
      "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'Tenants' AND COLUMN_NAME = 'TenantId'"));
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COLUMNPROPERTY(OBJECT_ID(N'[platform].[Tenants]'), 'TenantId', 'IsIdentity')"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[Tenants]') AND name = N'UX_Tenants_NormalizedTenantCode' AND is_unique = 1"));

    var first = CreateTenant("  Acme  ", "Shared Name");
    var second = CreateTenant("BETA", "Shared Name");
    context.Tenants.AddRange(first, second);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.NotEmpty(first.RowVersion);
    Assert.Equal(2, await context.Tenants.CountAsync());
    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'Tenants' AND COLUMN_NAME = 'NormalizedTenantCode'"));

    context.Tenants.Add(CreateTenant("acme", "Another Name"));
    var duplicate = await SaveAsync(context);
    Assert.Equal("Persistence.UniqueConstraint", duplicate.Error.Code);
    context.ChangeTracker.Clear();

    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[Tenants] SET [Status] = N'Deleted' WHERE [TenantId] = {0}",
      first.TenantId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[Tenants] SET [StatusChangeReasonCode] = N'FreeForm' WHERE [TenantId] = {0}",
      first.TenantId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[Tenants] SET [TenantCode] = N'   ' WHERE [TenantId] = {0}",
      first.TenantId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[Tenants] SET [TenantName] = N'   ' WHERE [TenantId] = {0}",
      first.TenantId));
    await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "UPDATE [platform].[Tenants] SET [StatusChangedUtc] = DATEADD(day, -1, [CreatedUtc]) WHERE [TenantId] = {0}",
      first.TenantId));
    var deleteFailure = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
      "DELETE FROM [platform].[Tenants] WHERE [TenantId] = {0}",
      first.TenantId));
    Assert.Equal(51000, deleteFailure.Number);
    Assert.Equal(2, await context.Tenants.AsNoTracking().CountAsync());

    var persisted = await context.Tenants.SingleAsync(tenant => tenant.Id == first.TenantId);
    context.Tenants.Remove(persisted);
    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    context.Entry(persisted).State = EntityState.Unchanged;

    Assert.Equal(["AuthenticationSessions", "Companies_MigratedToTenant", "TenantDatabaseAssignments", "TenantEntitlementGrants", "TenantLocalizationOverrides", "TenantLocalizationSettings", "TenantSubscriptions"], await ReadStringsAsync(
      context,
      "SELECT parent.name FROM sys.foreign_keys fk JOIN sys.tables referenced ON referenced.object_id = fk.referenced_object_id JOIN sys.schemas s ON s.schema_id = referenced.schema_id JOIN sys.tables parent ON parent.object_id = fk.parent_object_id WHERE s.name = 'platform' AND referenced.name = 'Tenants' ORDER BY parent.name"));
    var deferredTables = await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME IN ('AccessTokens', 'RefreshTokens', 'Subscriptions', 'Billing')");
    Assert.Equal(0, deferredTables);
  }

  [Theory]
  [InlineData("ACME")]
  [InlineData("acme")]
  [InlineData("  acme  ")]
  [Trait("Acceptance", "AC-TEN-0002")]
  [Trait("Scenario", "TS-TEN-0023")]
  public async Task Concurrent_normalized_code_creation_commits_exactly_one_and_maps_the_loser(string competingCode)
  {
    await using var database = await TenantSqlDatabase.CreateAsync();
    await using var firstContext = database.CreateContext();
    await using var secondContext = database.CreateContext();
    var gate = new AsyncGate(2);
    var firstDispatcher = new RecordingDomainEventDispatcher();
    var secondDispatcher = new RecordingDomainEventDispatcher();
    // The REAL trial issuer over the real repository (`DEC-L-034`, T-041): the tenant and its 14-day
    // subscription share one unit of work, so this test now also covers the losing side rolling BOTH back.
    var firstHandler = new CreateTenantCommandHandler(
      new GatedTenantRepository(new TenantRepository(firstContext), gate),
      new TrialSubscriptionIssuer(new TenantSubscriptionRepository(firstContext), new TestClock()),
      new PlatformUnitOfWork(firstContext, firstDispatcher),
      new TestCurrentUser(),
      new TestClock());
    var secondHandler = new CreateTenantCommandHandler(
      new GatedTenantRepository(new TenantRepository(secondContext), gate),
      new TrialSubscriptionIssuer(new TenantSubscriptionRepository(secondContext), new TestClock()),
      new PlatformUnitOfWork(secondContext, secondDispatcher),
      new TestCurrentUser(),
      new TestClock());

    var results = await Task.WhenAll(
      firstHandler.HandleAsync(new CreateTenantCommand("Acme", "Shared Name")),
      secondHandler.HandleAsync(new CreateTenantCommand(competingCode, "Shared Name")));

    var successIndex = Array.FindIndex(results, result => result.IsSuccess);
    var failureIndex = Array.FindIndex(results, result => result.IsFailure);
    Assert.NotEqual(-1, successIndex);
    Assert.NotEqual(-1, failureIndex);
    Assert.Equal("Tenant.CodeExists", results[failureIndex].Error.Code);
    Assert.Single(successIndex == 0 ? firstDispatcher.Events : secondDispatcher.Events);
    Assert.Empty(failureIndex == 0 ? firstDispatcher.Events : secondDispatcher.Events);
    await using var verification = database.CreateContext();
    Assert.Equal(1, await verification.Tenants.AsNoTracking().CountAsync());
    Assert.Equal("ACME", await verification.Tenants.AsNoTracking()
      .Select(tenant => tenant.NormalizedTenantCode)
      .SingleAsync());

    // ---- AND THE TRIAL WENT WITH THE WINNER, ONCE, WHILE THE LOSER LEFT NOTHING BEHIND.
    //
    // The stronger claim under contention: one tenant, one subscription. A trial issued outside the
    // tenant's transaction would have left the loser's record stranded here with no tenant to belong to.
    Assert.Equal(1, await verification.TenantSubscriptions.AsNoTracking().CountAsync());
    Assert.Equal(
      TrialSubscription.PlanId,
      await verification.TenantSubscriptions.AsNoTracking()
        .Select(subscription => subscription.SubscriptionPlanId)
        .SingleAsync());
  }

  [Fact]
  [Trait("Security", "SEC-TEN-0206")]
  [Trait("Scenario", "TS-TEN-0026")]
  public async Task Batch_physical_delete_fails_explicitly_and_retains_every_tenant()
  {
    await using var database = await TenantSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();
    context.Tenants.AddRange(
      CreateTenant("ONE", "Tenant One"),
      CreateTenant("TWO", "Tenant Two"),
      CreateTenant("THREE", "Tenant Three"));
    Assert.True((await SaveAsync(context)).IsSuccess);

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      context.Database.ExecuteSqlRawAsync("DELETE FROM [platform].[Tenants]"));

    Assert.Equal(51000, failure.Number);
    Assert.DoesNotContain("token", failure.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(3, await context.Tenants.AsNoTracking().CountAsync());
  }

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0304")]
  [Trait("Scenario", "TS-TEN-0020")]
  public async Task Tenant_migration_rolls_back_and_reapplies_with_its_trigger()
  {
    await using var database = TenantSqlDatabase.CreateUnmigrated();
    await using var context = database.CreateContext();
    var migrator = context.Database.GetService<IMigrator>();
    await migrator.MigrateAsync();
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[platform].[TR_Tenants_PreventDelete]')"));

    await migrator.MigrateAsync(PreviousMigration);
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'[platform].[Tenants]')"));
    Assert.Equal(0, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[platform].[TR_Tenants_PreventDelete]')"));

    await migrator.MigrateAsync();
    Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.triggers WHERE object_id = OBJECT_ID(N'[platform].[TR_Tenants_PreventDelete]')"));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0017")]
  [Trait("Scenario", "TS-TEN-0021")]
  [Trait("Scenario", "TS-TEN-0022")]
  [Trait("Decision", "DEC-TEN-0013")]
  [Trait("Decision", "DEC-TEN-0014")]
  public async Task Upgrade_from_authentication_milestone_preserves_legacy_ids_without_backfill_or_fk_retrofit()
  {
    await using var database = TenantSqlDatabase.CreateUnmigrated();
    await using var context = database.CreateContext();
    var migrator = context.Database.GetService<IMigrator>();
    await migrator.MigrateAsync(PreviousMigration);
    var legacyTenantId = Guid.NewGuid();
    await context.Database.ExecuteSqlRawAsync(
      "INSERT INTO [platform].[Roles] ([TenantId], [Name], [NormalizedRoleName], [RoleType], [Status], [CreatedUtc], [ModifiedUtc]) " +
      "VALUES ({0}, N'Legacy Role', N'LEGACY ROLE', N'Custom', N'Active', {1}, {1})",
      legacyTenantId,
      TenantSqlDatabase.Now);

    await migrator.MigrateAsync();

    Assert.Empty(await context.Tenants.AsNoTracking().ToArrayAsync());
    Assert.Equal(legacyTenantId, await context.Roles.IgnoreQueryFilters().Select(role => role.TenantId).SingleAsync());
    Assert.Equal(["AuthenticationSessions", "Companies_MigratedToTenant", "TenantDatabaseAssignments", "TenantEntitlementGrants", "TenantLocalizationOverrides", "TenantLocalizationSettings", "TenantSubscriptions"], await ReadStringsAsync(
      context,
      "SELECT parent.name FROM sys.foreign_keys fk JOIN sys.tables referenced ON referenced.object_id = fk.referenced_object_id JOIN sys.schemas s ON s.schema_id = referenced.schema_id JOIN sys.tables parent ON parent.object_id = fk.parent_object_id WHERE s.name = 'platform' AND referenced.name = 'Tenants' ORDER BY parent.name"));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0014")]
  [Trait("Scenario", "TS-TEN-0025")]
  public async Task Tenant_rowversion_rejects_stale_lifecycle_write()
  {
    await using var database = await TenantSqlDatabase.CreateAsync();
    Guid tenantId;
    await using (var setup = database.CreateContext())
    {
      var tenant = CreateTenant("CONCURRENT", "Concurrent Tenant");
      setup.Tenants.Add(tenant);
      Assert.True((await SaveAsync(setup)).IsSuccess);
      tenantId = tenant.TenantId;
    }

    await using var firstContext = database.CreateContext();
    await using var staleContext = database.CreateContext();
    var first = await firstContext.Tenants.SingleAsync(tenant => tenant.Id == tenantId);
    var stale = await staleContext.Tenants.SingleAsync(tenant => tenant.Id == tenantId);
    Assert.True(first.Activate("actor-1", Guid.NewGuid(), TenantSqlDatabase.Now.AddMinutes(1)).IsSuccess);
    Assert.True((await SaveAsync(firstContext)).IsSuccess);
    Assert.True(stale.Archive(TenantStatusChangeReason.Administrative, "actor-2", Guid.NewGuid(), TenantSqlDatabase.Now.AddMinutes(1)).IsSuccess);

    var dispatcher = new RecordingDomainEventDispatcher();
    var staleResult = await SaveAsync(staleContext, dispatcher);

    Assert.Equal("Persistence.ConcurrencyConflict", staleResult.Error.Code);
    Assert.Empty(dispatcher.Events);
  }

  private static Tenant CreateTenant(string code, string name) => Tenant.Create(
    TenantCode.Create(code).Value,
    TenantName.Create(name).Value,
    "integration-actor",
    Guid.NewGuid(),
    TenantSqlDatabase.Now).Value;

  private static Task<Result<int>> SaveAsync(
    PlatformDbContext context,
    IDomainEventDispatcher? dispatcher = null) =>
    new PlatformUnitOfWork(context, dispatcher ?? new NoOpDomainEventDispatcher()).SaveChangesAsync();

  private static async Task<string> ReadStringAsync(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
  }

  private static async Task<int> ReadInt32Async(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private static async Task<string[]> ReadStringsAsync(PlatformDbContext context, string commandText)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    await using var reader = await command.ExecuteReaderAsync();
    var values = new List<string>();
    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return [.. values];
  }

  private sealed class TenantSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 1, 11, 0, 0, TimeSpan.Zero);

    public static TenantSqlDatabase CreateUnmigrated()
    {
      var databaseName = $"SSAS_ERP_FP003_{Guid.NewGuid():N}";
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      return new TenantSqlDatabase(builder.ConnectionString);
    }

    public static async Task<TenantSqlDatabase> CreateAsync()
    {
      var database = CreateUnmigrated();
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

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
  {
    public List<DomainEvent> Events { get; } = [];

    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
      Events.AddRange(domainEvents);
      return Task.CompletedTask;
    }
  }

  private sealed class AsyncGate(int participantCount)
  {
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;

    public Task SignalAndWaitAsync()
    {
      if (Interlocked.Increment(ref arrivals) == participantCount)
      {
        ready.SetResult();
      }

      return ready.Task;
    }
  }

  private sealed class GatedTenantRepository(ITenantRepository inner, AsyncGate gate) : ITenantRepository
  {
    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      inner.GetByIdAsync(tenantId, cancellationToken);

    public Task<Tenant?> GetByNormalizedCodeAsync(
      string normalizedTenantCode,
      CancellationToken cancellationToken = default) =>
      inner.GetByNormalizedCodeAsync(normalizedTenantCode, cancellationToken);

    public async Task<bool> NormalizedCodeExistsAsync(
      string normalizedTenantCode,
      CancellationToken cancellationToken = default)
    {
      var exists = await inner.NormalizedCodeExistsAsync(normalizedTenantCode, cancellationToken);
      await gate.SignalAndWaitAsync();
      return exists;
    }

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
      inner.AddAsync(tenant, cancellationToken);
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-actor";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => Guid.NewGuid();
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => TenantSqlDatabase.Now;
  }
}
