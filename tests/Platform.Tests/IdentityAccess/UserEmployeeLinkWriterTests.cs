using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// THE LINK'S FIRST WRITE PATH (T-092, ADR-030) — AND THE REFUSALS ARE THE INTERESTING HALF.
// ==================================================================================================
//
// Creating a row is one assertion. **What needed deciding was every case where a row must NOT be created**,
// and each of those refusals encodes a ruling that would otherwise live only in a comment:
//
//   at most one live link each way   a mistaken link would otherwise be permanent
//   identical pair succeeds          a retry after a lost response must not refuse work already done
//   a different pair refuses         an upsert would hide a reassignment inside a creation
//   `Ended` refuses                  T-090's seam would make the link inert from birth
//   `Unknown` refuses differently    the disclosure inversion, which is deliberate
public sealed class UserEmployeeLinkWriterTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid EmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid OtherEmployeeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

  [Fact]
  [Trait("Criterion", "ADR-030")]
  public async Task A_current_employee_is_linked_and_saved()
  {
    var world = new World();

    Assert.True((await world.Link().HandleAsync(Command())).IsSuccess);

    var link = Assert.Single(world.Links.Added);
    Assert.Equal(TenantId, link.TenantId);
    Assert.Equal(World.TenantUserId, link.TenantUserId);
    Assert.Equal(EmployeeId, link.EmployeeId);

    // SAVED, not merely added to a repository. Without this the test passes against a handler that builds
    // the row and never persists it.
    Assert.Equal(1, world.UnitOfWork.SaveCount);
  }

  // ---- THE SAME PAIR AGAIN IS A SUCCESS, AND NOTHING IS WRITTEN.
  //
  // A retry after a lost response must not refuse work already done. Asserting `SaveCount == 0` is what
  // separates idempotent from "wrote a second row and got away with it because the index was not hit".
  [Fact]
  public async Task Linking_the_same_pair_again_succeeds_without_writing()
  {
    var world = new World();
    world.Links.ByTenantUser = NewLink(EmployeeId);

    Assert.True((await world.Link().HandleAsync(Command())).IsSuccess);

    Assert.Empty(world.Links.Added);
    Assert.Equal(0, world.UnitOfWork.SaveCount);
  }

  // ================================================================================================
  // ONE LIVE LINK EACH WAY — BOTH DIRECTIONS, AND THEY ANSWER DIFFERENTLY.
  // ================================================================================================
  //
  // The unique indexes enforce this and remain the authority. **Checked in the handler so the caller learns
  // WHICH way the collision went**, because the two need different repairs and a unique violation cannot
  // say. A single conflict code would make an administrator guess.
  [Fact]
  [Trait("Criterion", "ADR-030")]
  public async Task Linking_a_user_that_already_has_a_different_employee_is_refused()
  {
    var world = new World();
    world.Links.ByTenantUser = NewLink(OtherEmployeeId);

    var result = await world.Link().HandleAsync(Command());

    Assert.True(result.IsFailure);
    Assert.Equal("UserEmployeeLink.TenantUserAlreadyLinked", result.Error.Code);
    Assert.Equal(0, world.UnitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Criterion", "ADR-030")]
  public async Task Linking_an_employee_that_already_has_a_different_user_is_refused()
  {
    var world = new World();
    world.Links.ByEmployee = NewLink(EmployeeId);

    var result = await world.Link().HandleAsync(Command());

    Assert.True(result.IsFailure);
    Assert.Equal("UserEmployeeLink.EmployeeAlreadyLinked", result.Error.Code);
    Assert.Equal(0, world.UnitOfWork.SaveCount);
  }

  // ================================================================================================
  // THE DISCLOSURE INVERSION — `Unknown` AND `Ended` GET DIFFERENT ANSWERS HERE.
  // ================================================================================================
  //
  // `UserEmployeeResolver` collapses them into one refusal, because its caller is an END USER and telling
  // them apart would disclose that a record exists. **This caller is a tenant administrator acting on an
  // employee they named and can already read**, so distinguishing discloses nothing they do not have — and
  // merging would leave them unable to tell a typo from a former employee.
  //
  // **Asserted as two DIFFERENT codes on purpose.** A future reader who "fixes" this to match the seam
  // will fail here and meet the reason.
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public async Task An_unknown_employee_and_a_terminated_one_are_refused_differently()
  {
    var unknown = new World { Standing = { Value = EmploymentStanding.Unknown } };
    var ended = new World { Standing = { Value = EmploymentStanding.Ended } };

    var unknownResult = await unknown.Link().HandleAsync(Command());
    var endedResult = await ended.Link().HandleAsync(Command());

    Assert.True(unknownResult.IsFailure);
    Assert.True(endedResult.IsFailure);
    Assert.NotEqual(unknownResult.Error.Code, endedResult.Error.Code);

    Assert.Equal("Common.NotFound", unknownResult.Error.Code);
    Assert.Equal("UserEmployeeLink.EmploymentEnded", endedResult.Error.Code);

    Assert.Equal(0, unknown.UnitOfWork.SaveCount);
    Assert.Equal(0, ended.UnitOfWork.SaveCount);
  }

  // ---- A DEACTIVATED USER CAN STILL BE LINKED.
  //
  // T-091 deactivates the account on termination, so refusing here would make a link unrepairable exactly
  // when someone noticed it was missing. The link is a mapping, not a grant.
  [Fact]
  public async Task A_deactivated_tenant_user_can_be_linked()
  {
    var world = new World(TenantUserStatus.Deactivated);

    Assert.True((await world.Link().HandleAsync(Command())).IsSuccess);
    Assert.Equal(1, world.UnitOfWork.SaveCount);
  }

  // ================================================================================================
  // REMOVAL — AND IT REFUSES WHEN THERE IS NOTHING TO REMOVE.
  // ================================================================================================
  //
  // The one place the two routes differ in shape. Linking is idempotent because a retry should not refuse
  // completed work; **removal is not, because "no link" and "I just removed the link" are the same wire
  // answer only if the caller never needed to know which happened** — and a typo in the tenant user id
  // would otherwise look like a completed correction.
  [Fact]
  public async Task Removing_a_link_deletes_the_row_and_saves()
  {
    var world = new World();
    world.Links.ByTenantUser = NewLink(EmployeeId);

    Assert.True((await world.Unlink().HandleAsync(new UnlinkEmployeeFromTenantUserCommand(World.TenantUserId))).IsSuccess);

    Assert.Single(world.Links.Removed);
    Assert.Equal(1, world.UnitOfWork.SaveCount);
  }

  [Fact]
  public async Task Removing_a_link_that_does_not_exist_is_refused_rather_than_ignored()
  {
    var world = new World();

    var result = await world.Unlink().HandleAsync(new UnlinkEmployeeFromTenantUserCommand(World.TenantUserId));

    Assert.True(result.IsFailure);
    Assert.Equal("UserEmployeeLink.NotFound", result.Error.Code);
    Assert.Empty(world.Links.Removed);
    Assert.Equal(0, world.UnitOfWork.SaveCount);
  }

  private static LinkEmployeeToTenantUserCommand Command() => new(World.TenantUserId, EmployeeId);

  private static UserEmployeeLink NewLink(Guid employeeId) =>
    UserEmployeeLink.Create(TenantId, World.TenantUserId, employeeId).Value;

  private sealed class World(TenantUserStatus status = TenantUserStatus.Active)
  {
    public const long TenantUserId = 42;

    public StubLinks Links { get; } = new();

    public StubStanding Standing { get; } = new();

    public RecordingUnitOfWork UnitOfWork { get; } = new();

    public LinkEmployeeToTenantUserCommandHandler Link() => new(
      Links, new SingleUser(NewTenantUser(status)), Standing, UnitOfWork,
      new FixedTenant(), new FixedUser());

    public UnlinkEmployeeFromTenantUserCommandHandler Unlink() => new(
      Links, UnitOfWork, new FixedTenant(), new FixedUser());

    private static TenantUser NewTenantUser(TenantUserStatus status)
    {
      var user = TenantUser.CreateActive(
        7, TenantId,
        EmailAddress.Create("layla@example.test").Value,
        UserDisplayName.Create("Layla Haddad").Value,
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

      if (status == TenantUserStatus.Deactivated)
      {
        user.Deactivate(Guid.NewGuid(), new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));
      }

      return user;
    }
  }

  private sealed class StubLinks : IUserEmployeeLinkRepository
  {
    public UserEmployeeLink? ByTenantUser { get; set; }

    public UserEmployeeLink? ByEmployee { get; set; }

    public List<UserEmployeeLink> Added { get; } = [];

    public List<UserEmployeeLink> Removed { get; } = [];

    public Task<UserEmployeeLink?> GetByTenantUserAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(ByTenantUser);

    public Task<UserEmployeeLink?> GetByEmployeeAsync(
      Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default) =>
      Task.FromResult(ByEmployee);

    public Task AddAsync(UserEmployeeLink link, CancellationToken cancellationToken = default)
    {
      Added.Add(link);
      return Task.CompletedTask;
    }

    public void Remove(UserEmployeeLink link) => Removed.Add(link);
  }

  private sealed class StubStanding : IEmploymentStandingDirectory
  {
    public EmploymentStanding Value { get; set; } = EmploymentStanding.Current;

    public Task<EmploymentStanding> GetStandingAsync(
      Guid employeeId, CancellationToken cancellationToken = default) => Task.FromResult(Value);
  }

  private sealed class SingleUser(TenantUser user) : ITenantUserRepository
  {
    public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(user);

    public Task<TenantUser?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(null);

    public Task<TenantUser?> GetByTrustedInvitationBindingAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(null);

    public Task<bool> EmailExistsAsync(
      string normalizedEmail, long? excludingTenantUserId = null, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task<bool> MembershipExistsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task<bool> HasActiveAssignmentToRoleAsync(long roleId, CancellationToken cancellationToken = default) =>
      Task.FromResult(false);

    public Task AddAsync(TenantUser tenantUser, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class RecordingUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<SSAS.BuildingBlocks.Application.Abstractions.Persistence.ITransaction> BeginTransactionAsync(
      CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("not reached by these tests");
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => UserEmployeeLinkWriterTests.TenantId;
  }

  private sealed class FixedUser : ICurrentUser
  {
    public string? UserId => "link-tests";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }
}
