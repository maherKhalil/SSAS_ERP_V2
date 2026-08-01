using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

public sealed class PlatformIdentityAccessPersistenceTests
{
  [Fact]
  public async Task Initial_migration_enforces_platform_iam_persistence_invariants()
  {
    var databaseName = $"SSAS_ERP_FP001_{Guid.NewGuid():N}";
    var connectionString = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      $"Server=localhost;Database={databaseName};Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
    connectionString = WithDatabase(connectionString, databaseName);
    var tenantOne = Guid.NewGuid();
    var tenantTwo = Guid.NewGuid();
    var clock = new MutableClock(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    try
    {
      long roleOneId;
      await using (var context = CreateContext(connectionString, tenantOne, clock))
      {
        AssertSqlServerModel(context);
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        var identity = Identity.Create(AuthenticationSubject.Create("oidc|global-user-1").Value);
        context.Identities.Add(identity);
        Assert.True((await SaveAsync(context)).IsSuccess);

        var role = Role.CreateCustom(
          tenantOne,
          RoleName.Create("Tenant Administrator").Value,
          "Tenant role",
          Guid.NewGuid(),
          clock.UtcNow);
        var user = TenantUser.CreateActive(
          identity.Id,
          tenantOne,
          EmailAddress.Create("User@Example.com").Value,
          UserDisplayName.Create("Tenant One User").Value,
          Guid.NewGuid(),
          clock.UtcNow);
        context.Roles.Add(role);
        context.TenantUsers.Add(user);
        Assert.True((await SaveAsync(context)).IsSuccess);
        roleOneId = role.Id;
        var userVersionBeforeAssignments = user.RowVersion.ToArray();
        var roleVersionBeforeAssignments = role.RowVersion.ToArray();

        var catalog = new PlatformPermissionCatalog();
        Assert.True(catalog.TryGet(PlatformPermissionNames.ViewUsers, out var permission));
        Assert.True(role.AssignPermission(permission, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True(user.AssignRole(role, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.False(userVersionBeforeAssignments.SequenceEqual(user.RowVersion));
        Assert.False(roleVersionBeforeAssignments.SequenceEqual(role.RowVersion));
        Assert.NotEmpty(identity.RowVersion);
        var userVersionAfterAssignment = user.RowVersion.ToArray();
        var roleVersionAfterAssignment = role.RowVersion.ToArray();

        clock.Advance();
        Assert.True(user.RemoveRole(role.Id, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.False(userVersionAfterAssignment.SequenceEqual(user.RowVersion));
        var userVersionAfterRemoval = user.RowVersion.ToArray();
        clock.Advance();
        Assert.True(user.AssignRole(role, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.False(userVersionAfterRemoval.SequenceEqual(user.RowVersion));
        Assert.False(roleVersionAfterAssignment.SequenceEqual(role.RowVersion));
        var roleVersionBeforePermissionRemoval = role.RowVersion.ToArray();

        clock.Advance();
        Assert.True(role.RemovePermission(permission.Name, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.False(roleVersionBeforePermissionRemoval.SequenceEqual(role.RowVersion));
        var roleVersionAfterRemoval = role.RowVersion.ToArray();
        clock.Advance();
        Assert.True(role.AssignPermission(permission, "integration-user", Guid.NewGuid(), clock.UtcNow).IsSuccess);
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.False(roleVersionAfterRemoval.SequenceEqual(role.RowVersion));

        Assert.Equal(2, await context.TenantUserRoleAssignments.CountAsync());
        Assert.Single(await context.TenantUserRoleAssignments.Where(item => item.RemovedUtc == null).ToArrayAsync());
        Assert.Equal(2, await context.RolePermissionAssignments.CountAsync());
        Assert.Single(await context.RolePermissionAssignments.Where(item => item.RemovedUtc == null).ToArrayAsync());
        Assert.Equal("integration-user", user.CreatedBy);
        Assert.Equal("integration-user", user.ModifiedBy);
        Assert.NotEmpty(user.RowVersion);
        Assert.NotEmpty(role.RowVersion);

        var tables = await ReadPlatformTableNamesAsync(context);
        Assert.Equal(
          [
            "AccountActionTokens",
            "AuthenticationAccounts",
            "Identities",
            "RolePermissionAssignments",
            "Roles",
            "TenantUserRoleAssignments",
            "TenantUsers",
            "Tenants"
          ],
          tables.Where(name => !name.StartsWith("__", StringComparison.Ordinal)).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Contains("__EFMigrationsHistory", tables);
        Assert.Equal(1, await ReadInt32Async(
          context,
          "SELECT COUNT(*) FROM sys.schemas WHERE name = 'platform'"));
        var filteredIndexes = await ReadFilteredIndexesAsync(context);
        Assert.Contains("[RemovedUtc] IS NULL", filteredIndexes["TenantUserRoleAssignments"], StringComparison.Ordinal);
        Assert.Contains("[RemovedUtc] IS NULL", filteredIndexes["RolePermissionAssignments"], StringComparison.Ordinal);

        var userAssignments = await context.TenantUserRoleAssignments.OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal("integration-user", userAssignments[0].AssignedBy);
        Assert.Equal("integration-user", userAssignments[0].RemovedBy);
        Assert.Equal(TimeSpan.Zero, userAssignments[0].AssignedUtc.Offset);
        Assert.Equal(TimeSpan.Zero, userAssignments[0].RemovedUtc?.Offset);
        var permissionAssignments = await context.RolePermissionAssignments.OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal("integration-user", permissionAssignments[0].AssignedBy);
        Assert.Equal("integration-user", permissionAssignments[0].RemovedBy);
        Assert.Equal(TimeSpan.Zero, permissionAssignments[0].AssignedUtc.Offset);
        Assert.Equal(TimeSpan.Zero, permissionAssignments[0].RemovedUtc?.Offset);
      }

      await using (var context = CreateContext(connectionString, tenantTwo, clock))
      {
        var identityId = await context.Identities.Select(item => item.Id).SingleAsync();
        context.Roles.Add(Role.CreateCustom(
          tenantTwo,
          RoleName.Create("Tenant Administrator").Value,
          null,
          Guid.NewGuid(),
          clock.UtcNow));
        context.TenantUsers.Add(TenantUser.CreateActive(
          identityId,
          tenantTwo,
          EmailAddress.Create("user@example.com").Value,
          UserDisplayName.Create("Tenant Two User").Value,
          Guid.NewGuid(),
          clock.UtcNow));
        Assert.True((await SaveAsync(context)).IsSuccess);
        Assert.Single(await context.Roles.ToArrayAsync());
        Assert.Single(await context.TenantUsers.ToArrayAsync());
      }

      await using (var context = CreateContext(connectionString, tenantOne, clock))
      {
        Assert.Single(await context.Roles.ToArrayAsync());
        var tenantUser = await context.TenantUsers.SingleAsync();
        Assert.Equal(2, await context.TenantUsers.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await context.Roles.IgnoreQueryFilters().CountAsync());

        Assert.Throws<InvalidOperationException>(() =>
          context.Entry(tenantUser).Property(item => item.TenantId).CurrentValue = tenantTwo);
      }

      await using (var context = CreateContext(connectionString, tenantOne, clock))
      {
        var tenantUser = await context.TenantUsers.SingleAsync();
        context.Entry(tenantUser).Property(item => item.IdentityId).CurrentValue += 100;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
      }

      await VerifyDuplicateEmailIsRejectedAsync(connectionString, tenantOne, clock);
      await VerifyOptimisticConcurrencyAsync(connectionString, tenantOne, roleOneId, clock);
    }
    finally
    {
      await using var cleanup = CreateContext(connectionString, tenantOne, clock);
      await cleanup.Database.EnsureDeletedAsync();
    }
  }

  private static async Task VerifyDuplicateEmailIsRejectedAsync(
    string connectionString,
    Guid tenantId,
    MutableClock clock)
  {
    await using var context = CreateContext(connectionString, tenantId, clock);
    var identity = Identity.Create(AuthenticationSubject.Create($"oidc|{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await SaveAsync(context)).IsSuccess);
    context.TenantUsers.Add(TenantUser.CreateActive(
      identity.Id,
      tenantId,
      EmailAddress.Create("USER@example.com").Value,
      UserDisplayName.Create("Duplicate Email").Value,
      Guid.NewGuid(),
      clock.UtcNow));

    var result = await SaveAsync(context);
    Assert.True(result.IsFailure);
    Assert.Equal("Persistence.UniqueConstraint", result.Error.Code);
  }

  private static async Task VerifyOptimisticConcurrencyAsync(
    string connectionString,
    Guid tenantId,
    long roleId,
    MutableClock clock)
  {
    await using var firstContext = CreateContext(connectionString, tenantId, clock);
    await using var secondContext = CreateContext(connectionString, tenantId, clock);
    var first = await firstContext.Roles.SingleAsync(role => role.Id == roleId);
    var second = await secondContext.Roles.SingleAsync(role => role.Id == roleId);

    Assert.True(first.Update(RoleName.Create("Administrator A").Value, null, Guid.NewGuid(), clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(firstContext)).IsSuccess);
    Assert.True(second.Update(RoleName.Create("Administrator B").Value, null, Guid.NewGuid(), clock.UtcNow).IsSuccess);
    var staleSave = await SaveAsync(secondContext);

    Assert.True(staleSave.IsFailure);
    Assert.Equal("Persistence.ConcurrencyConflict", staleSave.Error.Code);
  }

  private static PlatformDbContext CreateContext(string connectionString, Guid tenantId, MutableClock clock)
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
      .Options;
    return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId), clock);
  }

  private static void AssertSqlServerModel(PlatformDbContext context)
  {
    var identity = context.Model.FindEntityType(typeof(Identity));
    var tenantUser = context.Model.FindEntityType(typeof(TenantUser));
    var role = context.Model.FindEntityType(typeof(Role));
    var userRole = context.Model.FindEntityType(typeof(TenantUserRoleAssignment));
    var rolePermission = context.Model.FindEntityType(typeof(RolePermissionAssignment));
    Assert.NotNull(identity);
    Assert.NotNull(tenantUser);
    Assert.NotNull(role);
    Assert.NotNull(userRole);
    Assert.NotNull(rolePermission);

    Assert.Null(identity.GetQueryFilter());
    Assert.NotNull(tenantUser.GetQueryFilter());
    Assert.NotNull(role.GetQueryFilter());
    Assert.NotNull(userRole.GetQueryFilter());
    Assert.NotNull(rolePermission.GetQueryFilter());
    AssertRowVersion(identity);
    AssertRowVersion(tenantUser);
    AssertRowVersion(role);

    Assert.Contains(userRole.GetIndexes(), index =>
      index.IsUnique && index.GetFilter() == "[RemovedUtc] IS NULL" &&
      index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "TenantUserId", "RoleId"]));
    Assert.Contains(rolePermission.GetIndexes(), index =>
      index.IsUnique && index.GetFilter() == "[RemovedUtc] IS NULL" &&
      index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "RoleId", "PermissionName"]));
    Assert.Contains(userRole.GetForeignKeys(), key =>
      key.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "TenantUserId"]));
    Assert.Contains(userRole.GetForeignKeys(), key =>
      key.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "RoleId"]));
    Assert.Contains(rolePermission.GetForeignKeys(), key =>
      key.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "RoleId"]));
  }

  private static void AssertRowVersion(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType)
  {
    var rowVersion = entityType.FindProperty("RowVersion");
    Assert.NotNull(rowVersion);
    Assert.True(rowVersion.IsConcurrencyToken);
    Assert.Equal("rowversion", rowVersion.GetColumnType());
  }

  private static Task<Result<int>> SaveAsync(PlatformDbContext context) =>
    new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher()).SaveChangesAsync();

  private static async Task<IReadOnlyCollection<string>> ReadPlatformTableNamesAsync(PlatformDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'platform'";
    var names = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      names.Add(reader.GetString(0));
    }

    return names;
  }

  private static async Task<IReadOnlyDictionary<string, string>> ReadFilteredIndexesAsync(PlatformDbContext context)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT t.name, i.filter_definition FROM sys.indexes i " +
      "JOIN sys.tables t ON t.object_id = i.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = 'platform' AND i.filter_definition IS NOT NULL";
    var indexes = new Dictionary<string, string>(StringComparer.Ordinal);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
      var tableName = reader.GetString(0);
      var filter = reader.GetString(1);
      indexes[tableName] = indexes.TryGetValue(tableName, out var existing)
        ? string.Concat(existing, "; ", filter)
        : filter;
    }

    return indexes;
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

  private static string WithDatabase(string connectionString, string databaseName)
  {
    var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
    {
      InitialCatalog = databaseName
    };
    return builder.ConnectionString;
  }

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-user";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class MutableClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Advance() => UtcNow = UtcNow.AddMinutes(1);
  }
}
