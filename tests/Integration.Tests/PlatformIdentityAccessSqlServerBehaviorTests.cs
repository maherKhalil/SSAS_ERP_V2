using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

public sealed class PlatformIdentityAccessSqlServerBehaviorTests
{
  [Fact]
  public async Task Exact_identifiers_and_per_tenant_normalized_uniqueness_are_enforced()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantOne = Guid.NewGuid();
    var tenantTwo = Guid.NewGuid();
    long firstIdentityId;
    long secondIdentityId;

    await using (var context = database.CreateContext(tenantOne))
    {
      var firstIdentity = Identity.Create(AuthenticationSubject.Create("oidc|CaseSensitive").Value);
      var secondIdentity = Identity.Create(AuthenticationSubject.Create("oidc|casesensitive").Value);
      context.Identities.AddRange(firstIdentity, secondIdentity);
      Assert.True((await SaveAsync(context)).IsSuccess);
      firstIdentityId = firstIdentity.Id;
      secondIdentityId = secondIdentity.Id;

      var role = Role.CreateCustom(
        tenantOne,
        RoleName.Create("Manager").Value,
        null,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      var user = TenantUser.CreateActive(
        firstIdentity.Id,
        tenantOne,
        EmailAddress.Create("Case.User@Example.com").Value,
        UserDisplayName.Create("Case User").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      context.Roles.Add(role);
      context.TenantUsers.Add(user);
      Assert.True((await SaveAsync(context)).IsSuccess);

      var upperPermission = new PermissionDefinition(
        PermissionName.Create("Platform.Users.View").Value,
        PermissionScope.Tenant,
        "Uppercase identifier");
      var lowerPermission = new PermissionDefinition(
        PermissionName.Create("platform.Users.View").Value,
        PermissionScope.Tenant,
        "Lowercase identifier");
      Assert.True(role.AssignPermission(upperPermission, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True(role.AssignPermission(lowerPermission, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(context)).IsSuccess);
      Assert.Equal(2, await context.RolePermissionAssignments.CountAsync());

      var duplicateRole = Role.CreateCustom(
        tenantOne,
        RoleName.Create(" manager ").Value,
        null,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      context.Roles.Add(duplicateRole);
      var duplicateRoleResult = await SaveAsync(context);
      Assert.Equal("Persistence.UniqueConstraint", duplicateRoleResult.Error.Code);
      context.Entry(duplicateRole).State = EntityState.Detached;

      var duplicateEmail = TenantUser.CreateActive(
        secondIdentity.Id,
        tenantOne,
        EmailAddress.Create("case.user@example.com").Value,
        UserDisplayName.Create("Duplicate Email").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      context.TenantUsers.Add(duplicateEmail);
      var duplicateEmailResult = await SaveAsync(context);
      Assert.Equal("Persistence.UniqueConstraint", duplicateEmailResult.Error.Code);
    }

    await using (var context = database.CreateContext(tenantTwo))
    {
      context.Roles.Add(Role.CreateCustom(
        tenantTwo,
        RoleName.Create("manager").Value,
        null,
        Guid.NewGuid(),
        database.Clock.UtcNow));
      context.TenantUsers.Add(TenantUser.CreateActive(
        secondIdentityId,
        tenantTwo,
        EmailAddress.Create("case.user@example.com").Value,
        UserDisplayName.Create("Other Tenant User").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow));
      Assert.True((await SaveAsync(context)).IsSuccess);
    }

    await using (var context = database.CreateContext(tenantOne))
    {
      Assert.NotEqual(firstIdentityId, secondIdentityId);
      var repository = new IdentityRepository(context);
      Assert.Equal(firstIdentityId, (await repository.GetBySubjectAsync("oidc|CaseSensitive"))?.Id);
      Assert.Equal(secondIdentityId, (await repository.GetBySubjectAsync("oidc|casesensitive"))?.Id);
      Assert.True(await repository.SubjectExistsAsync("oidc|CaseSensitive"));
      Assert.False(await repository.SubjectExistsAsync("OIDC|CASESENSITIVE"));
      Assert.Equal(2, await context.RolePermissionAssignments.CountAsync());
    }
  }

  [Fact]
  public async Task Composite_tenant_foreign_keys_and_restricted_deletes_are_enforced()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantOne = Guid.NewGuid();
    var tenantTwo = Guid.NewGuid();
    long tenantUserId;
    long tenantOneRoleId;
    long tenantTwoRoleId;

    await using (var context = database.CreateContext(tenantOne))
    {
      var identity = Identity.Create(AuthenticationSubject.Create("oidc|tenant-one").Value);
      context.Identities.Add(identity);
      Assert.True((await SaveAsync(context)).IsSuccess);
      var role = Role.CreateCustom(tenantOne, RoleName.Create("Role One").Value, null, Guid.NewGuid(), database.Clock.UtcNow);
      var user = TenantUser.CreateActive(
        identity.Id,
        tenantOne,
        EmailAddress.Create("one@example.com").Value,
        UserDisplayName.Create("One").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      context.Roles.Add(role);
      context.TenantUsers.Add(user);
      Assert.True((await SaveAsync(context)).IsSuccess);
      Assert.True(user.AssignRole(role, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(context)).IsSuccess);
      tenantUserId = user.Id;
      tenantOneRoleId = role.Id;
    }

    await using (var context = database.CreateContext(tenantTwo))
    {
      var role = Role.CreateCustom(tenantTwo, RoleName.Create("Role Two").Value, null, Guid.NewGuid(), database.Clock.UtcNow);
      context.Roles.Add(role);
      Assert.True((await SaveAsync(context)).IsSuccess);
      tenantTwoRoleId = role.Id;
    }

    await using (var context = database.CreateContext(tenantOne))
    {
      var exception = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
        "INSERT INTO [platform].[TenantUserRoleAssignments] " +
        "([TenantId], [TenantUserId], [RoleId], [AssignedUtc], [AssignedBy]) " +
        "VALUES ({0}, {1}, {2}, {3}, {4})",
        tenantOne,
        tenantUserId,
        tenantTwoRoleId,
        database.Clock.UtcNow,
        "integration-user"));
      Assert.Equal(547, exception.Number);
    }

    await using (var context = database.CreateContext(tenantOne))
    {
      var role = await context.Roles.SingleAsync(item => item.Id == tenantOneRoleId);
      context.Roles.Remove(role);
      var deleteResult = await SaveAsync(context);
      Assert.True(deleteResult.IsFailure);
      Assert.Equal("Persistence.WriteFailure", deleteResult.Error.Code);
    }

    await using (var context = database.CreateContext(tenantOne))
    {
      Assert.NotNull(await context.Roles.SingleOrDefaultAsync(item => item.Id == tenantOneRoleId));
      Assert.Single(await context.TenantUserRoleAssignments.ToArrayAsync());
    }
  }

  [Fact]
  public async Task Concurrent_assignment_cannot_commit_after_role_retirement()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    long userId;
    long roleId;

    await using (var context = database.CreateContext(tenantId))
    {
      var identity = Identity.Create(AuthenticationSubject.Create("oidc|concurrency").Value);
      context.Identities.Add(identity);
      Assert.True((await SaveAsync(context)).IsSuccess);
      var role = Role.CreateCustom(tenantId, RoleName.Create("Concurrent Role").Value, null, Guid.NewGuid(), database.Clock.UtcNow);
      var user = TenantUser.CreateActive(
        identity.Id,
        tenantId,
        EmailAddress.Create("concurrency@example.com").Value,
        UserDisplayName.Create("Concurrent User").Value,
        Guid.NewGuid(),
        database.Clock.UtcNow);
      context.Roles.Add(role);
      context.TenantUsers.Add(user);
      Assert.True((await SaveAsync(context)).IsSuccess);
      userId = user.Id;
      roleId = role.Id;
    }

    await using var staleAssignmentContext = database.CreateContext(tenantId);
    var staleUser = await staleAssignmentContext.TenantUsers
      .Include(item => item.RoleAssignments)
      .SingleAsync(item => item.Id == userId);
    var staleRole = await staleAssignmentContext.Roles.SingleAsync(item => item.Id == roleId);

    await using (var retirementContext = database.CreateContext(tenantId))
    {
      var role = await retirementContext.Roles.SingleAsync(item => item.Id == roleId);
      Assert.True(role.RequestRetirement(Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(retirementContext)).IsSuccess);
      Assert.True(role.Retire(false, Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
      Assert.True((await SaveAsync(retirementContext)).IsSuccess);
    }

    Assert.True(staleUser.AssignRole(staleRole, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    var staleAssignmentResult = await SaveAsync(staleAssignmentContext);
    Assert.True(staleAssignmentResult.IsFailure);
    Assert.Equal("Persistence.ConcurrencyConflict", staleAssignmentResult.Error.Code);

    await using var verificationContext = database.CreateContext(tenantId);
    Assert.Equal(RoleStatus.Retired, (await verificationContext.Roles.SingleAsync(item => item.Id == roleId)).Status);
    Assert.Empty(await verificationContext.TenantUserRoleAssignments.ToArrayAsync());
  }

  [Fact]
  public async Task Deactivated_users_and_removed_assignments_do_not_block_retirement()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    await using var context = database.CreateContext(tenantId);
    var identity = Identity.Create(AuthenticationSubject.Create("oidc|retirement").Value);
    context.Identities.Add(identity);
    Assert.True((await SaveAsync(context)).IsSuccess);
    var role = Role.CreateCustom(tenantId, RoleName.Create("Retiring Role").Value, null, Guid.NewGuid(), database.Clock.UtcNow);
    var user = TenantUser.CreateActive(
      identity.Id,
      tenantId,
      EmailAddress.Create("retirement@example.com").Value,
      UserDisplayName.Create("Retirement User").Value,
      Guid.NewGuid(),
      database.Clock.UtcNow);
    context.Roles.Add(role);
    context.TenantUsers.Add(user);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.True(user.AssignRole(role, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    var repository = new TenantUserRepository(context);
    Assert.True(await repository.HasActiveAssignmentToRoleAsync(role.Id));

    Assert.True(user.Deactivate(Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.False(await repository.HasActiveAssignmentToRoleAsync(role.Id));

    Assert.True(user.Reactivate(Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.True(user.RemoveRole(role.Id, "integration-user", Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.False(await repository.HasActiveAssignmentToRoleAsync(role.Id));

    Assert.True(role.RequestRetirement(Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.True(role.Retire(await repository.HasActiveAssignmentToRoleAsync(role.Id), Guid.NewGuid(), database.Clock.UtcNow).IsSuccess);
    Assert.True((await SaveAsync(context)).IsSuccess);
    Assert.Equal(RoleStatus.Retired, role.Status);
    Assert.Single(await context.TenantUserRoleAssignments.ToArrayAsync());
  }

  private static Task<Result<int>> SaveAsync(PlatformDbContext context) =>
    new PlatformUnitOfWork(context, new NoOpDomainEventDispatcher()).SaveChangesAsync();

  private sealed class SqlTestDatabase(string connectionString) : IAsyncDisposable
  {
    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public static async Task<SqlTestDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP001_REVIEW_{Guid.NewGuid():N}";
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new SqlTestDatabase(builder.ConnectionString);
      try
      {
        await using var context = database.CreateContext(Guid.NewGuid());
        await context.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext(Guid tenantId)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId), Clock);
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext(Guid.NewGuid());
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
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
