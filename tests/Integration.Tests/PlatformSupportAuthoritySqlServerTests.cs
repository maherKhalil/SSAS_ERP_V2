using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

// Phase 2 platform-support authority SQL verification (ADR-015 / DEC-TEN-0018).
public sealed class PlatformSupportAuthoritySqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Migration_creates_platform_support_authority_tables_with_expected_shape()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    await using var context = database.CreateContext();

    Assert.Empty(await context.Database.GetPendingMigrationsAsync());

    var principalEntity = context.Model.FindEntityType(typeof(PlatformSupportPrincipal));
    Assert.NotNull(principalEntity);
    Assert.Null(principalEntity!.GetQueryFilter());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformSupportPrincipal).GetInterfaces());
    Assert.True(principalEntity.FindProperty(nameof(PlatformSupportPrincipal.RowVersion))?.IsConcurrencyToken);

    Assert.Equal(
      ["PlatformSupportPrincipalId", "IdentityId", "RowVersion", "CreatedUtc", "ModifiedUtc", "CreatedBy", "ModifiedBy"],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformSupportPrincipals' ORDER BY ORDINAL_POSITION"));
    Assert.Equal(
      ["PlatformPermissionAssignmentId", "PlatformSupportPrincipalId", "PermissionName", "AssignedUtc", "AssignedBy", "RemovedUtc", "RemovedBy"],
      await ReadStringsAsync(
        context,
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformPermissionAssignments' ORDER BY ORDINAL_POSITION"));

    Assert.Equal("Latin1_General_100_BIN2", await ReadStringAsync(
      context,
      "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'platform' AND TABLE_NAME = 'PlatformPermissionAssignments' AND COLUMN_NAME = 'PermissionName'"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND name = N'UX_PlatformSupportPrincipals_IdentityId' AND is_unique = 1"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'[platform].[PlatformPermissionAssignments]') AND name = N'UX_PlatformPermissionAssignments_Principal_Permission' AND is_unique = 1 AND has_filter = 1"));
    Assert.Equal(1, await ReadInt32Async(
      context,
      "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'[platform].[PlatformSupportPrincipals]') AND referenced_object_id = OBJECT_ID(N'[platform].[Identities]')"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Authority_grant_and_revoke_round_trip_through_the_real_provider_and_leave_tenant_tables_untouched()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      var result = await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId));
      Assert.True(result.IsSuccess);
      principalId = result.Value;
    }

    await using (var context = database.CreateContext())
    {
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewTenants))).IsSuccess);

      // Duplicate active grant is a conflict (enforced by the unique filtered index → mapped error).
      var duplicate = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants));
      Assert.True(duplicate.IsFailure);
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      var permissions = await read.GetActivePermissionsAsync(principalId);
      Assert.Equal([PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewTenants], permissions);
    }

    await using (var context = database.CreateContext())
    {
      var revoke = new RevokePlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await revoke.HandleAsync(new RevokePlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      Assert.Equal([PlatformPermissionNames.ViewTenants], await read.GetActivePermissionsAsync(principalId));
      // The revoked grant is retained (history), not physically removed.
      Assert.Equal(2, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
      // Tenant authority tables are entirely unaffected by platform-support authority operations.
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[Roles]"));
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[RolePermissionAssignments]"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Tenant_scoped_permission_grant_is_rejected_before_persistence()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    await using (var context = database.CreateContext())
    {
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      var tenant = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewCompanies));
      Assert.True(tenant.IsFailure);
      var unknown = await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, "Platform.Unknown.Thing"));
      Assert.True(unknown.IsFailure);
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(0, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Corrupt_non_platform_support_assignment_is_excluded_from_authority_reads()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ViewTenants))).IsSuccess);
    }

    // Force-seed a corrupt Tenant-scoped permission directly into SQL, bypassing the write-side guard.
    await using (var context = database.CreateContext())
    {
      await context.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ViewCompanies}, {PlatformSupportSqlDatabase.Now}, {"corruption-test"})");
    }

    await using (var context = database.CreateContext())
    {
      var read = new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog());
      var permissions = await read.GetActivePermissionsAsync(principalId);
      Assert.DoesNotContain(PlatformPermissionNames.ViewCompanies, permissions);
      Assert.Contains(PlatformPermissionNames.ViewTenants, permissions);
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Duplicate_active_assignment_violates_database_uniqueness()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    await using (var context = database.CreateContext())
    {
      await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ManageTenants}, {PlatformSupportSqlDatabase.Now}, {"race"})"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Physical_deletion_of_a_platform_support_principal_is_rejected()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    // Childless principal: no assignment rows exist, so FK Restrict cannot cause the failure — the
    // DbContext physical-delete guard must, before any SQL DELETE is issued (InvalidOperationException,
    // not SqlException).
    await using (var context = database.CreateContext())
    {
      var principal = await context.PlatformSupportPrincipals.SingleAsync(item => item.Id == principalId);
      context.PlatformSupportPrincipals.Remove(principal);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0018")]
  public async Task Physical_deletion_of_a_platform_permission_assignment_is_rejected()
  {
    await using var database = await PlatformSupportSqlDatabase.CreateAsync();
    var identityId = await SeedIdentityAsync(database);

    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), Uow(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;

      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(), Uow(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, PlatformPermissionNames.ManageTenants))).IsSuccess);
    }

    // Active assignment: physical delete must be rejected by the guard. Revocation stays valid because
    // it is an UPDATE (RemovedUtc/RemovedBy), not a DELETE.
    await using (var context = database.CreateContext())
    {
      var assignment = await context.PlatformPermissionAssignments.SingleAsync();
      context.PlatformPermissionAssignments.Remove(assignment);
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    await using (var context = database.CreateContext())
    {
      Assert.Equal(1, await ReadInt32Async(
        context,
        $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE PlatformSupportPrincipalId = {principalId}"));
    }
  }

  private static TestPlatformUnitOfWork Uow(PlatformDbContext context) => new(context);

  private static async Task<long> SeedIdentityAsync(PlatformSupportSqlDatabase database)
  {
    await using var context = database.CreateContext();
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return identity.Id;
  }

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

  // Thin PlatformUnitOfWork wrapper so tests can reuse the production unit of work without a DI container.
  private sealed class TestPlatformUnitOfWork(PlatformDbContext context)
    : SSAS.Platform.Application.Abstractions.Persistence.IPlatformUnitOfWork
  {
    private readonly PlatformUnitOfWork inner = new(context, new NoOpDomainEventDispatcher());

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      inner.SaveChangesAsync(cancellationToken);

    public Task<SSAS.BuildingBlocks.Application.Abstractions.Persistence.ITransaction> BeginTransactionAsync(
      CancellationToken cancellationToken = default) =>
      inner.BeginTransactionAsync(cancellationToken);
  }

  private sealed class PlatformSupportSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);

    public static async Task<PlatformSupportSqlDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP003_PSA_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new PlatformSupportSqlDatabase(builder.ConnectionString);
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

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-actor";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
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
    public DateTimeOffset UtcNow => PlatformSupportSqlDatabase.Now;
  }
}
