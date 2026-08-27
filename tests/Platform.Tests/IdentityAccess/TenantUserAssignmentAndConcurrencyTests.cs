using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// TWO CRITERIA THAT WERE ENFORCED AND UNTESTED (T-062).
// ==================================================================================================
//
// `AC-IAM-0012` and `AC-IAM-0022` were found untraced by `trace-check.py` and confirmed by hand: each
// appeared in `acceptance-criteria.md` and on no line anywhere in FP-001 that also named a scenario.
//
// **Both invariants were already live in the code.** That is what makes them worth testing rather
// than reassuring: **an enforced guard with no test can be removed by a refactor and nothing catches
// it**, which is the argument behind `DEC-L-008` condition 4.
//
// ---- A THIRD CRITERION IS DELIBERATELY ABSENT FROM THIS FILE.
//
// `AC-IAM-0019` was reported untraced in the same breath and was NOT. It sits inside the range
// `AC-IAM-0018`-`AC-IAM-0020` at `traceability-matrix.md:16`, on a row carrying four scenarios, and
// it reads as untraced only because rule 4 did not expand ranges. **No test was written to make the
// earlier report true.**
public sealed class TenantUserAssignmentAndConcurrencyTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

  private static TenantUser ActiveUser(Guid tenantId) =>
    TenantUser.CreateActive(
      identityId: 1,
      tenantId: tenantId,
      EmailAddress.Create("user@example.com").Value,
      UserDisplayName.Create("User").Value,
      Guid.NewGuid(),
      Now);

  private static Role AssignableRole(Guid tenantId, string name) =>
    Role.CreateCustom(tenantId, RoleName.Create(name).Value, null, Guid.NewGuid(), Now);

  // `RowVersion` is written by the database, so a test that wants a CURRENT version has to set one.
  // Reflection is used deliberately and only here: without it every expected version mismatches and
  // the concurrency test would pass against a guard that rejected unconditionally — a test with no
  // negative control, which is the shape this file exists to remove.
  private static void SetRowVersion(TenantUser user, byte[] value) =>
    typeof(TenantUser)
      .GetProperty(nameof(TenantUser.RowVersion), BindingFlags.Public | BindingFlags.Instance)!
      .SetValue(user, value);

  // ================================================================================================
  // AC-IAM-0012 — a role from one tenant cannot be assigned to a user in another (TS-IAM-0043).
  // ================================================================================================
  [Fact]
  public void Cross_tenant_role_assignment_is_rejected()
  {
    var user = ActiveUser(TenantId);
    var foreignRole = AssignableRole(OtherTenantId, "Foreign");

    var result = user.AssignRole(foreignRole, "actor", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(IdentityAccessErrors.TenantMismatch, result.Error);
  }

  // The negative control. Without it the test above passes against an AssignRole that rejects
  // everything, which is exactly how a suite gets a green that means nothing.
  [Fact]
  public void Same_tenant_role_assignment_is_accepted()
  {
    var user = ActiveUser(TenantId);
    var ownRole = AssignableRole(TenantId, "Own");

    Assert.True(user.AssignRole(ownRole, "actor", Guid.NewGuid(), Now).IsSuccess);
  }

  // ================================================================================================
  // AC-IAM-0022 — a stale update is rejected WITHOUT overwriting newer data (TS-IAM-0049, TS-IAM-0050).
  //
  // TWO PROPERTIES WITH TWO OWNERS, AND TESTING THEM AS ONE IS WHAT MAKES THIS LOOK EXPENSIVE.
  //   "rejected"                   -> the handler refuses early and never reaches the unit of work
  //   "without overwriting"        -> the DATABASE refuses, via a rowversion concurrency token
  //
  // The second is not tested by a round-trip here on purpose: a round-trip would be testing EF Core's
  // rowversion implementation, which is Microsoft's. **What can regress is someone deleting
  // `.IsConcurrencyToken()` from a configuration** — and the model assertion below catches exactly
  // that, in milliseconds rather than in a 33-minute Integration leg.
  // ================================================================================================
  [Fact]
  public async Task Stale_expected_row_version_is_rejected_and_nothing_is_saved()
  {
    var user = ActiveUser(TenantId);
    SetRowVersion(user, [7, 7, 7, 7]);
    var role = AssignableRole(TenantId, "Own");

    var unitOfWork = new RecordingUnitOfWork();
    var handler = Handler(user, role, unitOfWork);

    var result = await handler.HandleAsync(
      new AssignRoleToTenantUserCommand(TenantUserId: user.Id, RoleId: role.Id, ExpectedRowVersion: [1, 2, 3, 4]));

    Assert.True(result.IsFailure);
    Assert.Equal(IdentityAccessErrors.ConcurrencyConflict, result.Error);
    Assert.Equal(0, unitOfWork.SaveCount);   // "without overwriting": the write never happened
  }

  [Fact]
  public async Task Matching_expected_row_version_passes_the_concurrency_guard()
  {
    var user = ActiveUser(TenantId);
    SetRowVersion(user, [7, 7, 7, 7]);
    var role = AssignableRole(TenantId, "Own");

    var unitOfWork = new RecordingUnitOfWork();
    var handler = Handler(user, role, unitOfWork);

    var result = await handler.HandleAsync(
      new AssignRoleToTenantUserCommand(TenantUserId: user.Id, RoleId: role.Id, ExpectedRowVersion: [7, 7, 7, 7]));

    Assert.True(result.IsSuccess);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  // ================================================================================================
  // AC-IAM-0022, the half the database owns — asserted as a property of the MODEL (TS-IAM-0050).
  // ================================================================================================
  [Fact]
  public void TenantUser_row_version_is_a_concurrency_token_in_the_model()
  {
    // Model building needs options and services, not a connection: `.Model` is built without ever
    // opening one, which is why this needs no database and no Integration leg.
    using var context = new PlatformDbContext(
      new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer("Server=model-only;Database=none")
        .Options,
      new NoUser(), new NoTenant(), new FixedClock());

    var rowVersion = context.Model
      .FindEntityType(typeof(TenantUser))!
      .FindProperty(nameof(TenantUser.RowVersion))!;

    Assert.True(rowVersion.IsConcurrencyToken);
    Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
  }

  private static AssignRoleToTenantUserCommandHandler Handler(
    TenantUser user, Role role, IPlatformUnitOfWork unitOfWork) =>
    new(new SingleUserRepository(user), new SingleRoleRepository(role), unitOfWork,
        new FixedTenant(), new FixedUser(), new FixedClock());

  private sealed class RecordingUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("not reached by these tests");
  }

  private sealed class SingleUserRepository(TenantUser user) : ITenantUserRepository
  {
    public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(user);

    public Task<TenantUser?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(user);

    public Task<TenantUser?> GetByTrustedInvitationBindingAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(user);

    public Task<bool> EmailExistsAsync(
      string normalizedEmail, long? excludingTenantUserId = null, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(true);

    public Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class SingleRoleRepository(Role role) : IRoleRepository
  {
    public Task<Role?> GetByIdAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult<Role?>(role);

    public Task<IReadOnlyCollection<Role>> GetByIdsAsync(
      IReadOnlyCollection<long> roleIds, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyCollection<Role>>([role]);

    public Task<bool> NameExistsAsync(
      string normalizedRoleName, long? excludingRoleId = null, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(Role role, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => TenantUserAssignmentAndConcurrencyTests.TenantId;
  }

  private sealed class FixedUser : ICurrentUser
  {
    public string? UserId => "actor";

    public string? UserName => "actor";

    public string? Email => "actor@example.com";

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class NoUser : ICurrentUser
  {
    public string? UserId => null;

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

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
