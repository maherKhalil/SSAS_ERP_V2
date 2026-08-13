using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 2 platform-support authority (ADR-015 / DEC-TEN-0018): domain + application invariants.
public sealed class PlatformSupportAuthorityTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

  private static PermissionDefinition Permission(string name)
  {
    Assert.True(new PlatformPermissionCatalog().TryGet(name, out var definition));
    return definition;
  }

  // ---- Domain ----

  [Fact]
  public void Register_requires_a_valid_identity()
  {
    Assert.True(PlatformSupportPrincipal.Register(0).IsFailure);
    Assert.True(PlatformSupportPrincipal.Register(-1).IsFailure);
    Assert.True(PlatformSupportPrincipal.Register(42).IsSuccess);
  }

  [Fact]
  public void Principal_is_global_and_not_tenant_or_company_owned()
  {
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformSupportPrincipal).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformPermissionAssignment).GetInterfaces());
    Assert.Null(typeof(PlatformSupportPrincipal).GetProperty("TenantId"));
    Assert.Null(typeof(PlatformPermissionAssignment).GetProperty("TenantId"));
  }

  [Theory]
  [InlineData(PlatformPermissionNames.ViewTenants)]
  [InlineData(PlatformPermissionNames.ManageTenants)]
  [InlineData(PlatformPermissionNames.TenantLifecycle)]
  public void Known_platform_support_permission_can_be_granted(string name)
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.GrantPermission(Permission(name), "actor", Now);

    Assert.True(result.IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == name);
  }

  [Fact]
  public void Tenant_scoped_permission_cannot_be_granted()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.GrantPermission(Permission(PlatformPermissionNames.ViewCompanies), "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.TenantPermissionRejected, result.Error);
    Assert.Empty(principal.ActivePermissions);
  }

  [Fact]
  public void Duplicate_active_grant_is_a_conflict()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);

    var duplicate = principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now.AddMinutes(1));

    Assert.True(duplicate.IsFailure);
    Assert.Equal(PlatformSupportErrors.DuplicatePermissionAssignment, duplicate.Error);
    Assert.Single(principal.ActivePermissions);
  }

  [Fact]
  public void Revoke_removes_authority_and_re_grant_is_allowed_and_keeps_history()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    var manage = Permission(PlatformPermissionNames.ManageTenants);
    Assert.True(principal.GrantPermission(manage, "actor", Now).IsSuccess);

    Assert.True(principal.RevokePermission(manage.Name, "actor", Now.AddMinutes(1)).IsSuccess);
    Assert.Empty(principal.ActivePermissions);

    Assert.True(principal.GrantPermission(manage, "actor", Now.AddMinutes(2)).IsSuccess);
    Assert.Single(principal.ActivePermissions);
    Assert.Equal(2, principal.PermissionAssignments.Count);
  }

  [Fact]
  public void Revoking_an_unassigned_permission_fails()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.RevokePermission(Permission(PlatformPermissionNames.ViewTenants).Name, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PermissionAssignmentNotFound, result.Error);
  }

  // ---- Lifecycle (ADR-016 / DEC-TEN-0020) ----

  [Fact]
  public void Register_starts_active_with_no_status_transition_metadata()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    Assert.Equal(PlatformSupportPrincipalStatus.Active, principal.Status);
    Assert.Null(principal.StatusChangedUtc);
    Assert.Null(principal.StatusChangedBy);
  }

  [Fact]
  public void Disable_then_reenable_transitions_and_stamps_metadata_without_touching_assignments()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);

    Assert.True(principal.Disable("disabler", Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(PlatformSupportPrincipalStatus.Disabled, principal.Status);
    Assert.Equal(Now.AddMinutes(1), principal.StatusChangedUtc);
    Assert.Equal("disabler", principal.StatusChangedBy);
    Assert.Single(principal.ActivePermissions);

    Assert.True(principal.Reenable("enabler", Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(PlatformSupportPrincipalStatus.Active, principal.Status);
    Assert.Equal(Now.AddMinutes(2), principal.StatusChangedUtc);
    Assert.Equal("enabler", principal.StatusChangedBy);
    Assert.Single(principal.ActivePermissions);
  }

  [Fact]
  public void Disable_when_already_disabled_is_an_invalid_transition()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.Disable("actor", Now).IsSuccess);

    var again = principal.Disable("actor", Now.AddMinutes(1));

    Assert.True(again.IsFailure);
    Assert.Equal(PlatformSupportErrors.InvalidStatusTransition, again.Error);
    Assert.Equal(Now, principal.StatusChangedUtc);
  }

  [Fact]
  public void Reenable_when_already_active_is_an_invalid_transition()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.Reenable("actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.InvalidStatusTransition, result.Error);
    Assert.Null(principal.StatusChangedUtc);
  }

  [Fact]
  public void Grant_is_rejected_while_disabled()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.Disable("actor", Now).IsSuccess);

    var result = principal.GrantPermission(Permission(PlatformPermissionNames.ViewTenants), "actor", Now.AddMinutes(1));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalDisabled, result.Error);
    Assert.Empty(principal.ActivePermissions);
  }

  [Fact]
  public void Revoke_is_allowed_while_disabled()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    var manage = Permission(PlatformPermissionNames.ManageTenants);
    Assert.True(principal.GrantPermission(manage, "actor", Now).IsSuccess);
    Assert.True(principal.Disable("actor", Now.AddMinutes(1)).IsSuccess);

    Assert.True(principal.RevokePermission(manage.Name, "actor", Now.AddMinutes(2)).IsSuccess);

    Assert.Empty(principal.ActivePermissions);
    Assert.Equal(PlatformSupportPrincipalStatus.Disabled, principal.Status);
  }

  [Fact]
  public async Task Disable_handler_gates_on_actor_missing_principal_and_stale_version()
  {
    var missing = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());
    Assert.Equal(
      PlatformSupportErrors.PrincipalNotFound,
      (await missing.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [1]))).Error);

    var unauthorized = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(PlatformSupportPrincipal.Register(7).Value), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser(null), new StubClock());
    Assert.True((await unauthorized.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [1]))).IsFailure);

    // A fresh principal has an empty RowVersion, so any expected version is a concurrency conflict.
    var stale = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(PlatformSupportPrincipal.Register(7).Value), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());
    Assert.Equal(
      IdentityAccessErrors.ConcurrencyConflict,
      (await stale.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [9]))).Error);
  }

  // ---- Defense-in-depth filter ----

  [Fact]
  public void Authority_filter_keeps_platform_support_and_drops_tenant_and_unknown()
  {
    var catalog = new PlatformPermissionCatalog();

    var filtered = PlatformSupportPermissionFilter.FilterToPlatformSupportScope(
      new[] { PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewCompanies, "Platform.Unknown.Thing" },
      catalog);

    Assert.Equal(new[] { PlatformPermissionNames.ManageTenants }, filtered);
  }

  // ---- Application handlers ----

  [Fact]
  public async Task Register_handler_requires_trusted_platform_actor()
  {
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(), new FakeUnitOfWork(), new StubCurrentUser(null));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsFailure);
  }

  [Fact]
  public async Task Register_handler_rejects_a_duplicate_principal_for_the_identity()
  {
    var repository = new FakePrincipalRepository { ExistsForIdentity = true };
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(
      repository, new FakeUnitOfWork(), new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalAlreadyExists, result.Error);
    Assert.Null(repository.Added);
  }

  [Fact]
  public async Task Register_handler_persists_a_new_principal()
  {
    var repository = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(repository, unitOfWork, new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsSuccess);
    Assert.NotNull(repository.Added);
    Assert.Equal(9, repository.Added!.IdentityId);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Grant_handler_rejects_unknown_and_tenant_permissions_and_accepts_platform_support()
  {
    var principal = PlatformSupportPrincipal.Register(9).Value;
    var repository = new FakePrincipalRepository(principal);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new GrantPlatformPermissionCommandHandler(
      repository, new PlatformPermissionCatalog(), unitOfWork, new StubCurrentUser("actor"), new StubClock());

    var unknown = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, "Platform.Unknown.Thing"));
    var tenant = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, PlatformPermissionNames.ViewCompanies));
    var valid = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, PlatformPermissionNames.ManageTenants));

    Assert.Equal(PlatformSupportErrors.UnknownPermission, unknown.Error);
    Assert.Equal(PlatformSupportErrors.TenantPermissionRejected, tenant.Error);
    Assert.True(valid.IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == PlatformPermissionNames.ManageTenants);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Grant_handler_reports_missing_principal()
  {
    var handler = new GrantPlatformPermissionCommandHandler(
      new FakePrincipalRepository(), new PlatformPermissionCatalog(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());

    var result = await handler.HandleAsync(new GrantPlatformPermissionCommand(123, PlatformPermissionNames.ManageTenants));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalNotFound, result.Error);
  }

  [Fact]
  public async Task Revoke_handler_removes_an_existing_platform_permission()
  {
    var principal = PlatformSupportPrincipal.Register(9).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);
    var repository = new FakePrincipalRepository(principal);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new RevokePlatformPermissionCommandHandler(repository, unitOfWork, new StubCurrentUser("actor"), new StubClock());

    var result = await handler.HandleAsync(new RevokePlatformPermissionCommand(principal.Id, PlatformPermissionNames.ManageTenants));

    Assert.True(result.IsSuccess);
    Assert.Empty(principal.ActivePermissions);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  private sealed class FakePrincipalRepository(PlatformSupportPrincipal? principal = null) : IPlatformSupportPrincipalRepository
  {
    public bool ExistsForIdentity { get; init; }
    public PlatformSupportPrincipal? Added { get; private set; }

    public Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    // In-memory fake: same result as the unlocked read. Real lock serialization is proven only by the SQL Server
    // concurrency tests (PlatformAuthenticationSessionFlowSqlServerTests), never by this fake.
    public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(ExistsForIdentity);

    public Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
    {
      Added = principal;
      return Task.CompletedTask;
    }
  }

  private sealed class FakePlatformSessionRepository : IPlatformAuthenticationSessionRepository
  {
    public Task<PlatformRefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(Guid refreshTokenPublicId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession?> GetByRefreshTokenForUpdateAsync(long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession?> GetByIdForUpdateAsync(long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(long identityId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    // The Disable-handler tests all fail before reaching proactive revocation, so no active sessions are needed.
    public Task<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>> ListActiveByPrincipalForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>>([]);

    public Task AddAsync(SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession session, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    // Disable now opens its transaction before the lock-protected principal read (global principal → session
    // order), so even the early gate paths pass through here.
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class StubCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
