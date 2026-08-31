using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE IDENTITY-TO-EMPLOYEE MAPPING, AGAINST A REAL DATABASE (ADR-030, FP-015).
// ==================================================================================================
//
// The cardinality rule and the tenant-isolation guarantee are enforced by the SCHEMA, not by a handler —
// two unique indexes and a composite foreign key. **Those are claims about SQL Server's behaviour**, so
// asserting them against an EF model would assert the intent rather than the thing.
//
// ---- EVERY REFUSAL HERE HAS A NEGATIVE CONTROL, AND WITHOUT ONE IT WOULD PROVE NOTHING.
//
// "A second link for this user is refused" passes just as well against a table that refuses EVERY second
// insert. So each refusal is paired with the neighbouring case that must SUCCEED — a different user, a
// different employee — and it is the pair that identifies which columns the constraint is actually on.
public sealed class UserEmployeeLinkSqlServerTests
{
  // ---- THE READ THE WHOLE MAPPING EXISTS FOR: given this tenant user, which employee?
  [Fact]
  public async Task A_link_resolves_an_employee_from_a_tenant_user()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    var employeeId = Guid.NewGuid();

    await using (var context = database.CreateContext(tenantId))
    {
      var userId = await SeedUserAsync(context, database, tenantId, "resolve@example.com", "Resolve");

      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, userId, employeeId).Value);
      Assert.True((await SaveAsync(context)).IsSuccess);

      var resolved = await context.UserEmployeeLinks
        .Where(link => link.TenantId == tenantId && link.TenantUserId == userId)
        .Select(link => link.EmployeeId)
        .SingleAsync();

      Assert.Equal(employeeId, resolved);

      // The audit stamp is applied by the context, as it is for both neighbours — asserted so a future
      // change that stops stamping this table is not silent.
      var stored = await context.UserEmployeeLinks.SingleAsync();
      Assert.Equal("integration-user", stored.CreatedBy);
      Assert.Equal(database.Clock.UtcNow, stored.CreatedUtc);
    }
  }

  // ---- ONE EMPLOYEE PER USER. `UX_UserEmployeeLink_TenantId_TenantUserId`.
  //
  // The control is the second half: a DIFFERENT user linking to a different employee must SUCCEED. Without
  // it this test would pass against a unique index on `TenantId` alone, which would allow one link per
  // tenant and would be catastrophically wrong.
  [Fact]
  public async Task A_second_link_for_the_same_tenant_user_is_refused_and_a_different_user_is_accepted()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();

    await using (var context = database.CreateContext(tenantId))
    {
      var firstUser = await SeedUserAsync(context, database, tenantId, "first@example.com", "First");
      var secondUser = await SeedUserAsync(context, database, tenantId, "second@example.com", "Second");

      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, firstUser, Guid.NewGuid()).Value);
      Assert.True((await SaveAsync(context)).IsSuccess);

      // REFUSED: the same user, a different employee.
      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, firstUser, Guid.NewGuid()).Value);
      Assert.True((await SaveAsync(context)).IsFailure);
    }

    await using (var context = database.CreateContext(tenantId))
    {
      // THE CONTROL: a different user in the same tenant is accepted, so the constraint is on the user and
      // not on the tenant.
      var otherUser = await context.TenantUsers
        .Where(user => user.Email == EmailAddress.Create("second@example.com").Value)
        .Select(user => user.Id)
        .SingleAsync();

      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, otherUser, Guid.NewGuid()).Value);
      Assert.True((await SaveAsync(context)).IsSuccess);
    }
  }

  // ---- ONE USER PER EMPLOYEE. `UX_UserEmployeeLink_TenantId_EmployeeId`.
  //
  // The other direction, and it is NOT implied by the first: without this index two different users could
  // each claim the same employee, and self-service would answer the same payslips to both.
  [Fact]
  public async Task A_second_link_for_the_same_employee_is_refused_and_a_different_employee_is_accepted()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantId = Guid.NewGuid();
    var employeeId = Guid.NewGuid();

    await using (var context = database.CreateContext(tenantId))
    {
      var firstUser = await SeedUserAsync(context, database, tenantId, "claimant@example.com", "Claimant");
      var secondUser = await SeedUserAsync(context, database, tenantId, "rival@example.com", "Rival");

      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, firstUser, employeeId).Value);
      Assert.True((await SaveAsync(context)).IsSuccess);

      // REFUSED: a different user, the same employee.
      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, secondUser, employeeId).Value);
      Assert.True((await SaveAsync(context)).IsFailure);
    }

    await using (var context = database.CreateContext(tenantId))
    {
      // THE CONTROL: the same second user with a DIFFERENT employee is accepted, so the refusal above was
      // the employee index rather than anything about that user.
      var rival = await context.TenantUsers
        .Where(user => user.Email == EmailAddress.Create("rival@example.com").Value)
        .Select(user => user.Id)
        .SingleAsync();

      context.UserEmployeeLinks.Add(UserEmployeeLink.Create(tenantId, rival, Guid.NewGuid()).Value);
      Assert.True((await SaveAsync(context)).IsSuccess);
    }
  }

  // ================================================================================================
  // A LINK NAMING ANOTHER TENANT'S USER CANNOT BE STORED — AND THAT IS THE SCHEMA, NOT A HANDLER.
  // ================================================================================================
  //
  // The foreign key's principal key is the COMPOSITE `(TenantId, TenantUserId)`, so a row claiming a user
  // id that exists under a different tenant matches no principal and the insert fails. Tenant isolation on
  // this table is therefore a database guarantee rather than something every write path must remember.
  //
  // The control is the same user id succeeding under its OWN tenant: that is what shows the refusal was
  // the tenant mismatch and not an invalid user id.
  [Fact]
  public async Task A_link_naming_another_tenants_user_cannot_be_stored()
  {
    await using var database = await SqlTestDatabase.CreateAsync();
    var tenantOne = Guid.NewGuid();
    var tenantTwo = Guid.NewGuid();
    long userInTenantOne;

    await using (var context = database.CreateContext(tenantOne))
    {
      userInTenantOne = await SeedUserAsync(context, database, tenantOne, "owner@example.com", "Owner");
    }

    await using (var context = database.CreateContext(tenantTwo))
    {
      // REFUSED: tenantTwo claiming a user that belongs to tenantOne. The user id is real; the pair is not.
      context.UserEmployeeLinks.Add(
        UserEmployeeLink.Create(tenantTwo, userInTenantOne, Guid.NewGuid()).Value);

      Assert.True((await SaveAsync(context)).IsFailure);
    }

    await using (var context = database.CreateContext(tenantOne))
    {
      // THE CONTROL: the very same user id, under its own tenant, is accepted.
      context.UserEmployeeLinks.Add(
        UserEmployeeLink.Create(tenantOne, userInTenantOne, Guid.NewGuid()).Value);

      Assert.True((await SaveAsync(context)).IsSuccess);
    }
  }

  private static async Task<long> SeedUserAsync(
    PlatformDbContext context, SqlTestDatabase database, Guid tenantId, string email, string name)
  {
    var identity = Identity.Create(AuthenticationSubject.Create($"oidc|{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    Assert.True((await SaveAsync(context)).IsSuccess);

    var user = TenantUser.CreateActive(
      identity.Id,
      tenantId,
      EmailAddress.Create(email).Value,
      UserDisplayName.Create(name).Value,
      Guid.NewGuid(),
      database.Clock.UtcNow);

    context.TenantUsers.Add(user);
    Assert.True((await SaveAsync(context)).IsSuccess);

    return user.Id;
  }

  private static Task<Result<int>> SaveAsync(PlatformDbContext context) =>
    TestUnitOfWork.Platform(context, new NoOpDomainEventDispatcher()).SaveChangesAsync();

  private sealed class SqlTestDatabase(string connectionString) : IAsyncDisposable
  {
    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

    public static async Task<SqlTestDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP015_LINK_{Guid.NewGuid():N}";
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
    public Task DispatchAsync(
      IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-user";
    public string? UserName => null;
    public string? Email => null;
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
    public DateTimeOffset UtcNow { get; set; } = utcNow;
  }
}
