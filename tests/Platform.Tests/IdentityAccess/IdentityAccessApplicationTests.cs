using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.IdentityAccess;

public sealed class IdentityAccessApplicationTests
{
  private static readonly Guid TenantId = Guid.NewGuid();
  private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Tenant_command_requires_trusted_tenant_and_actor()
  {
    var handler = new CreateCustomRoleCommandHandler(
      new FakeRoleRepository(),
      new FakeUnitOfWork(),
      new TestCurrentTenant(null),
      new TestCurrentUser("actor"),
      new TestClock());

    var result = await handler.HandleAsync(new CreateCustomRoleCommand("Administrator", null));

    Assert.True(result.IsFailure);
    Assert.Equal("Authorization.Unauthorized", result.Error.Code);
  }

  [Fact]
  public async Task Membership_creation_rejects_a_second_membership_for_the_same_identity_in_the_tenant()
  {
    var identity = Identity.Create(AuthenticationSubject.Create("oidc|subject").Value);
    SetEntityId(identity, 7);
    var userRepository = new FakeTenantUserRepository { MembershipExists = true };
    var handler = new CreateTenantUserMembershipCommandHandler(
      new FakeIdentityRepository(identity),
      userRepository,
      new FakeRoleRepository(),
      new FakeUserBranchAccessRepository(),
      new FakeTenantAdministratorAuthority(),
      new FakeTenantBranchValidator(),
      new FakeBranchTopologyGuard(),
      new FakeUnitOfWork(),
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("actor"),
      new TestClock());

    // Branch foundation B1b: a normal user names the branches they may work in. This case fails earlier, on
    // the duplicate membership, which is the point — the branch rules must not mask an existing refusal.
    var result = await handler.HandleAsync(
      new CreateTenantUserMembershipCommand(7, "user@example.com", "User", [], [Guid.NewGuid()]));

    Assert.True(result.IsFailure);
    Assert.Equal("TenantUser.MembershipExists", result.Error.Code);
    Assert.Null(userRepository.Added);
  }

  // ---- B1b. A NORMAL USER WITHOUT BRANCHES IS REFUSED BEFORE ANYTHING IS PERSISTED.
  [Fact]
  public async Task Membership_creation_refuses_a_normal_user_with_no_branches_and_persists_nothing()
  {
    var identity = Identity.Create(AuthenticationSubject.Create("oidc|nobranch").Value);
    SetEntityId(identity, 9);
    var userRepository = new FakeTenantUserRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new CreateTenantUserMembershipCommandHandler(
      new FakeIdentityRepository(identity),
      userRepository,
      new FakeRoleRepository(),
      new FakeUserBranchAccessRepository(),
      new FakeTenantAdministratorAuthority(),
      new FakeTenantBranchValidator(),
      new FakeBranchTopologyGuard(),
      unitOfWork,
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("actor"),
      new TestClock());

    var result = await handler.HandleAsync(
      new CreateTenantUserMembershipCommand(9, "user@example.com", "User", [], []));

    Assert.True(result.IsFailure);
    Assert.Equal(BranchErrors.UserMustHaveAtLeastOneBranch.Code, result.Error.Code);
    Assert.Null(userRepository.Added);
  }

  // ---- B1b. AN ADMINISTRATOR NEEDS NO BRANCHES, which is what makes the first one creatable.
  [Fact]
  public async Task Membership_creation_allows_an_administrator_with_no_branches()
  {
    var identity = Identity.Create(AuthenticationSubject.Create("oidc|admin").Value);
    SetEntityId(identity, 11);
    var userRepository = new FakeTenantUserRepository();
    var handler = new CreateTenantUserMembershipCommandHandler(
      new FakeIdentityRepository(identity),
      userRepository,
      new FakeRoleRepository(),
      new FakeUserBranchAccessRepository(),
      new FakeTenantAdministratorAuthority { ConfersAdministration = true },
      new FakeTenantBranchValidator(),
      new FakeBranchTopologyGuard(),
      new FakeUnitOfWork(),
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("actor"),
      new TestClock());

    var result = await handler.HandleAsync(
      new CreateTenantUserMembershipCommand(11, "admin@example.com", "Admin", [], []));

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
    Assert.NotNull(userRepository.Added);
  }

  [Fact]
  public async Task Permission_command_rejects_unknown_names_and_stale_versions()
  {
    var role = CreateRole(1);
    SetRowVersion(role, [1]);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new AssignPermissionToRoleCommandHandler(
      new FakeRoleRepository(role),
      new PlatformPermissionCatalog(),
      unitOfWork,
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("actor"),
      new TestClock());

    var unknown = await handler.HandleAsync(new AssignPermissionToRoleCommand(role.Id, "Platform.Unknown.Read", [1]));
    var stale = await handler.HandleAsync(new AssignPermissionToRoleCommand(role.Id, PlatformPermissionNames.ViewUsers, [2]));
    var valid = await handler.HandleAsync(new AssignPermissionToRoleCommand(role.Id, PlatformPermissionNames.ViewUsers, [1]));

    Assert.Equal("Permission.Invalid", unknown.Error.Code);
    Assert.Equal("Persistence.ConcurrencyConflict", stale.Error.Code);
    Assert.True(valid.IsSuccess);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Effective_permissions_use_only_active_memberships_roles_and_permission_assignments()
  {
    var catalog = new PlatformPermissionCatalog();
    catalog.TryGet(PlatformPermissionNames.ViewUsers, out var viewUsers);
    catalog.TryGet(PlatformPermissionNames.ViewRoles, out var viewRoles);
    var activeRole = CreateRole(1);
    var historicalRole = CreateRole(2, "Historical");
    Assert.True(activeRole.AssignPermission(viewUsers, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(historicalRole.AssignPermission(viewUsers, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(historicalRole.AssignPermission(viewRoles, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(historicalRole.RemovePermission(viewRoles.Name, "actor", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    var user = CreateUser(10);
    Assert.True(user.AssignRole(activeRole, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(user.AssignRole(historicalRole, "actor", Guid.NewGuid(), Now).IsSuccess);
    var handler = new ResolveEffectivePermissionsQueryHandler(
      new FakeTenantUserRepository(user),
      new FakeRoleRepository(activeRole, historicalRole),
      new TestCurrentTenant(TenantId),
      new TestCurrentUser("actor"));

    var activeResult = await handler.HandleAsync(new ResolveEffectivePermissionsQuery(user.Id));
    Assert.Equal([PlatformPermissionNames.ViewUsers], activeResult.Value);

    Assert.True(user.RemoveRole(historicalRole.Id, "actor", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.True(activeRole.RequestRetirement(Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    var pendingResult = await handler.HandleAsync(new ResolveEffectivePermissionsQuery(user.Id));
    Assert.Equal([PlatformPermissionNames.ViewUsers], pendingResult.Value);

    Assert.True(activeRole.Retire(false, Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
    var retiredResult = await handler.HandleAsync(new ResolveEffectivePermissionsQuery(user.Id));
    Assert.Empty(retiredResult.Value);

    Assert.True(user.Deactivate(Guid.NewGuid(), Now.AddMinutes(4)).IsSuccess);
    var inactiveResult = await handler.HandleAsync(new ResolveEffectivePermissionsQuery(user.Id));
    Assert.Empty(inactiveResult.Value);
  }

  private static Role CreateRole(long id, string name = "Administrator")
  {
    var role = Role.CreateCustom(TenantId, RoleName.Create(name).Value, null, Guid.NewGuid(), Now);
    SetEntityId(role, id);
    return role;
  }

  private static TenantUser CreateUser(long id)
  {
    var user = TenantUser.CreateActive(
      7,
      TenantId,
      EmailAddress.Create("user@example.com").Value,
      UserDisplayName.Create("User").Value,
      Guid.NewGuid(),
      Now);
    SetEntityId(user, id);
    return user;
  }

  private static void SetEntityId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField(
      "<Id>k__BackingField",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }

  private static void SetRowVersion(Role role, byte[] value)
  {
    var field = typeof(Role).GetField(
      "<RowVersion>k__BackingField",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(role, value);
  }

  private sealed class FakeIdentityRepository(params Identity[] identities) : IIdentityRepository
  {
    private readonly List<Identity> values = [.. identities];

    public Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(item => item.Id == identityId));

    public Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(item => item.Subject.Value == subject));

    public Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.Any(item => item.Subject.Value == subject));

    public Task AddAsync(Identity identity, CancellationToken cancellationToken = default)
    {
      values.Add(identity);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeTenantUserRepository(params TenantUser[] users) : ITenantUserRepository
  {
    private readonly List<TenantUser> values = [.. users];

    public bool MembershipExists { get; init; }

    public TenantUser? Added { get; private set; }

    public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(item => item.Id == tenantUserId));

    public Task<TenantUser?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(item => item.IdentityId == identityId));

    public Task<TenantUser?> GetByTrustedInvitationBindingAsync(
      Guid tenantId,
      long tenantUserId,
      CancellationToken cancellationToken = default) => Task.FromResult(
        values.SingleOrDefault(item => item.TenantId == tenantId && item.Id == tenantUserId));

    public Task<bool> EmailExistsAsync(
      string normalizedEmail,
      long? excludingTenantUserId = null,
      CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(MembershipExists);

    public Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.Any(user => user.Status == Domain.Enums.TenantUserStatus.Active && user.ActiveRoleIds.Contains(roleId)));

    public Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default)
    {
      Added = tenantUser;
      values.Add(tenantUser);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeRoleRepository(params Role[] roles) : IRoleRepository
  {
    private readonly List<Role> values = [.. roles];

    public Task<Role?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(item => item.Id == roleId));

    public Task<IReadOnlyCollection<Role>> GetByIdsAsync(
      IReadOnlyCollection<long> roleIds,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyCollection<Role>>(values.Where(item => roleIds.Contains(item.Id)).ToArray());

    public Task<bool> NameExistsAsync(
      string normalizedRoleName,
      long? excludingRoleId = null,
      CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
      values.Add(role);
      return Task.CompletedTask;
    }
  }

  // ---- Branch foundation B1b fakes. Each is deliberately permissive so these tests keep exercising the
  // membership rules they were written for; the branch rules themselves are proven against real SQL.
  private sealed class FakeUserBranchAccessRepository : IUserBranchAccessRepository
  {
    public List<Guid> Added { get; } = [];

    public Task<IReadOnlyList<Guid>> GetBranchIdsAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task AddAsync(UserBranchAccess access, CancellationToken cancellationToken = default)
    {
      Added.Add(access.BranchId);
      return Task.CompletedTask;
    }

    public Task RemoveAsync(
      Guid tenantId, long tenantUserId, IReadOnlyCollection<Guid> branchIds,
      CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FakeTenantAdministratorAuthority : ITenantAdministratorAuthority
  {
    public bool IsAdministrator { get; init; }

    public bool ConfersAdministration { get; init; }

    public Task<bool> IsTenantAdministratorAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(IsAdministrator);

    public Task<bool> RolesConferAdministrationAsync(
      Guid tenantId, IReadOnlyCollection<long> roleIds, CancellationToken cancellationToken = default) =>
      Task.FromResult(ConfersAdministration);
  }

  private sealed class FakeTenantBranchValidator : ITenantBranchValidator
  {
    public Task<Result> ValidateAssignableAsync(
      Guid tenantId, IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());
  }

  private sealed class FakeBranchTopologyGuard : IBranchTopologyGuard
  {
    public Task<IBranchTopologyLease?> AcquireAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IBranchTopologyLease?>(new Lease(tenantId));

    private sealed class Lease(Guid tenantId) : IBranchTopologyLease
    {
      public Guid TenantId => tenantId;

      public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    public bool Committed { get; private set; }

    // Supported since Branch foundation B1b: membership creation now commits the user, its roles and its
    // branch assignments as one unit, so the handler genuinely opens a transaction.
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new NoOpTransaction(() => Committed = true));

    private sealed class NoOpTransaction(Action onCommit) : ITransaction
    {
      public Task CommitAsync(CancellationToken cancellationToken = default)
      {
        onCommit();
        return Task.CompletedTask;
      }

      public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

      public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
  }

  private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
