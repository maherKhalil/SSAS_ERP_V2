using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// PLATFORM'S HALF OF REQ-SS-0007 (T-091) — AND THE CASES THAT MUST *NOT* FAIL.
// ==================================================================================================
//
// The interesting assertions here are the successes. **A deactivator that refused whenever it found nothing
// to do would make termination fail for almost every employee** — most have no account at all, and today
// EVERY employee does, because nothing in production writes a `UserEmployeeLink` yet.
//
// Since the caller holds an open tenant transaction across this call, a spurious failure here does not
// merely log: it rolls back a termination that should have succeeded.
public sealed class TenantUserDeactivatorTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid EmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

  // ---- THE CASE THE TASK EXISTS FOR.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task A_linked_active_user_is_deactivated_and_saved()
  {
    await using var scope = await DeactivatorScope.CreateAsync(TenantUserStatus.Active, linked: true);

    var result = await scope.Deactivator.DeactivateForEmployeeAsync(EmployeeId);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantUserStatus.Deactivated, scope.TenantUser.Status);

    // SAVED, not merely mutated in memory. Without this the test passes against a deactivator that changed
    // the aggregate and never persisted it — and the account would stay open.
    Assert.Equal(1, scope.UnitOfWork.SaveCount);
  }

  // ---- AN EMPLOYEE WITH NO ACCOUNT IS A SUCCESS.
  //
  // **Today this is every employee.** A failure here would have made the guard a bug for every real caller
  // on the day it shipped — and, because the caller holds an open transaction, it would have made
  // termination impossible rather than merely noisy.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task An_employee_with_no_linked_user_succeeds_without_writing()
  {
    await using var scope = await DeactivatorScope.CreateAsync(TenantUserStatus.Active, linked: false);

    var result = await scope.Deactivator.DeactivateForEmployeeAsync(EmployeeId);

    Assert.True(result.IsSuccess);
    Assert.Equal(0, scope.UnitOfWork.SaveCount);
    Assert.Equal(TenantUserStatus.Active, scope.TenantUser.Status);
  }

  // ---- ALREADY DEACTIVATED IS A SUCCESS, WHICH IS WHAT MAKES THE OPERATOR'S RETRY SAFE.
  //
  // `TenantUser.Deactivate` refuses a non-Active status. If that surfaced as a failure, a retry after the
  // one reachable half-state would refuse on the half that already completed — and the state this task
  // exists to make repairable would be unrepairable by the obvious repair.
  [Theory]
  [Trait("Criterion", "REQ-SS-0007")]
  [InlineData(TenantUserStatus.Deactivated)]
  [InlineData(TenantUserStatus.Pending)]
  public async Task A_user_that_is_not_active_succeeds_without_writing(TenantUserStatus status)
  {
    await using var scope = await DeactivatorScope.CreateAsync(status, linked: true);

    var result = await scope.Deactivator.DeactivateForEmployeeAsync(EmployeeId);

    Assert.True(result.IsSuccess);
    Assert.Equal(0, scope.UnitOfWork.SaveCount);
  }

  // ---- A FAILED SAVE IS A FAILURE, AND THE CALLER DEPENDS ON THAT.
  //
  // The whole ordering ruling rests on this returning a failure: the handler rolls the termination back on
  // it. A deactivator that swallowed a write failure would leave the caller committing a termination whose
  // account closure never landed — the half-state the ordering was chosen to avoid.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task A_failed_save_is_reported_to_the_caller()
  {
    await using var scope = await DeactivatorScope.CreateAsync(TenantUserStatus.Active, linked: true);
    scope.UnitOfWork.Failure = new Error("IdentityAccess.WriteFailure", "the platform database is down");

    var result = await scope.Deactivator.DeactivateForEmployeeAsync(EmployeeId);

    Assert.True(result.IsFailure);
    Assert.Equal("IdentityAccess.WriteFailure", result.Error.Code);
  }

  // ---- NO TRUSTED TENANT IS A REFUSAL, NOT A QUIET SUCCESS.
  //
  // Deliberately unlike `UserEmployeeResolver`, where absence of a tenant is an ordinary answer. This is a
  // WRITE path: answering `Success` would report a guard as satisfied when it never ran.
  [Fact]
  public async Task A_request_with_no_trusted_tenant_is_refused()
  {
    await using var scope = await DeactivatorScope.CreateAsync(
      TenantUserStatus.Active, linked: true, withTenant: false);

    Assert.True((await scope.Deactivator.DeactivateForEmployeeAsync(EmployeeId)).IsFailure);
  }

  private sealed class DeactivatorScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;
    private readonly PlatformDbContext context;

    private DeactivatorScope(
      SqliteConnection connection,
      PlatformDbContext context,
      TenantUserDeactivator deactivator,
      TenantUser tenantUser,
      RecordingUnitOfWork unitOfWork)
    {
      this.connection = connection;
      this.context = context;
      Deactivator = deactivator;
      TenantUser = tenantUser;
      UnitOfWork = unitOfWork;
    }

    public TenantUserDeactivator Deactivator { get; }

    public TenantUser TenantUser { get; }

    public RecordingUnitOfWork UnitOfWork { get; }

    public static async Task<DeactivatorScope> CreateAsync(
      TenantUserStatus status, bool linked, bool withTenant = true)
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      var context = NewContext(connection);

      // Only the link table. `EnsureCreated` would translate every Platform configuration into SQLite, a
      // different provider from the one they were written for, and a mismatch would be reported here as a
      // deactivation failure. SQLite has no schemas, so the table is unqualified.
      await context.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE UserEmployeeLink (
          UserEmployeeLinkId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          TenantId TEXT NOT NULL,
          TenantUserId INTEGER NOT NULL,
          EmployeeId TEXT NOT NULL,
          CreatedUtc TEXT NOT NULL,
          CreatedBy TEXT NULL,
          ModifiedUtc TEXT NULL,
          ModifiedBy TEXT NULL,
          RowVersion BLOB NULL);
        """);

      var tenantUser = NewTenantUser(status);

      if (linked)
      {
        // PARAMETERISED, not interpolated. `ExecuteSqlRawAsync` with an interpolated string raises EF1002,
        // and the gate's first condition is zero warnings — a suppression here would trade a real rule for
        // a test fixture's convenience.
        await context.Database.ExecuteSqlRawAsync(
          """
          INSERT INTO UserEmployeeLink (TenantId, TenantUserId, EmployeeId, CreatedUtc, CreatedBy)
          VALUES ({0}, {1}, {2}, '2026-08-28T12:00:00Z', 'seed');
          """,
          TenantId.ToString(),
          tenantUser.Id,
          EmployeeId.ToString());
      }

      var unitOfWork = new RecordingUnitOfWork();

      return new DeactivatorScope(
        connection,
        context,
        new TenantUserDeactivator(
          context,
          new SingleUserRepository(tenantUser),
          unitOfWork,
          new FixedTenant(withTenant ? TenantId : null),
          new FixedClock()),
        tenantUser,
        unitOfWork);
    }

    public async ValueTask DisposeAsync()
    {
      await context.DisposeAsync();
      await connection.DisposeAsync();
    }

    private static PlatformDbContext NewContext(SqliteConnection connection) =>
      new(
        new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options,
        new NoUser(),
        new FixedTenant(TenantId),
        new FixedClock());
  }

  // Built through the real invitation/activation path so the status is reached the way production reaches
  // it, rather than by writing a field the aggregate protects.
  private static TenantUser NewTenantUser(TenantUserStatus status)
  {
    var email = EmailAddress.Create("layla@example.test").Value;
    var displayName = UserDisplayName.Create("Layla Haddad").Value;
    var invitedUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    var user = status is TenantUserStatus.Pending
      ? TenantUser.CreatePending(7, TenantId, email, displayName, Guid.NewGuid(), invitedUtc)
      : TenantUser.CreateActive(7, TenantId, email, displayName, Guid.NewGuid(), invitedUtc);

    if (status is TenantUserStatus.Deactivated)
    {
      user.Deactivate(Guid.NewGuid(), new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero));
    }

    return user;
  }

  private sealed class SingleUserRepository(TenantUser user) : ITenantUserRepository
  {
    public Task<TenantUser?> GetByIdAsync(long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult<TenantUser?>(tenantUserId == user.Id ? user : null);

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

  public sealed class RecordingUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Error? Failure { get; set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      if (Failure is { } error)
      {
        return Task.FromResult(Result.Failure<int>(error));
      }

      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("not reached by these tests");
  }

  private sealed class FixedTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class NoUser : ICurrentUser
  {
    public string? UserId => "deactivator-tests";

    public string? UserName => null;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
  }
}
