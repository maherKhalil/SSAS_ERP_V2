using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.PlatformSupport;

namespace SSAS.Integration.Tests;

// Phase 3B platform-support genesis/recovery bootstrap SQL verification (ADR-016 / DEC-TEN-0019/0021).
// Every test drives the real orchestrator through the real SQL Server provider.
public sealed class PlatformSupportBootstrapSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task Genesis_establishes_exactly_one_usable_principal_carrying_the_administer_grant()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var subject = await SeedEligibleIdentityAsync(database);

    var outcome = await RunBootstrapAsync(database, Options([subject]));

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    await using var verify = database.CreateContext();
    var principal = await verify.PlatformSupportPrincipals
      .Include(item => item.PermissionAssignments)
      .AsNoTracking()
      .SingleAsync();
    Assert.Equal(SSAS.Platform.Domain.Enums.PlatformSupportPrincipalStatus.Active, principal.Status);
    Assert.Contains(
      principal.PermissionAssignments.Where(assignment => assignment.IsActive),
      assignment => assignment.PermissionName.Value == PlatformPermissionNames.AdministerPlatformSupport);
    Assert.All(
      principal.PermissionAssignments,
      assignment => Assert.Equal($"platform-bootstrap:{subject}", assignment.AssignedBy));
    Assert.True(await ReadUsableAuthorityAsync(database));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task Bootstrap_is_inert_when_usable_authority_already_exists()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var existing = await SeedEligibleIdentityAsync(database);
    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, await RunBootstrapAsync(database, Options([existing])));

    // A second, also-eligible candidate is configured; because usable authority now exists, nothing happens.
    var second = await SeedEligibleIdentityAsync(database);
    var outcome = await RunBootstrapAsync(database, Options([existing, second]));

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, outcome);
    Assert.Equal(1, await ReadInt32Async(database, "SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals]"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task A_granted_principal_whose_account_is_ineligible_is_not_usable_authority()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    // Seed a principal WITH the administer grant but an ineligible (PendingSetup) account.
    var subject = await SeedIdentityAsync(database, eligible: false);
    var identityId = await IdentityIdAsync(database, subject);
    await RegisterAndGrantAsync(database, identityId, PlatformPermissionNames.AdministerPlatformSupport);

    // Authority is not usable: the anchoring account cannot authenticate.
    Assert.False(await ReadUsableAuthorityAsync(database));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Recovery_skips_a_disabled_principal_and_seeds_a_new_eligible_candidate()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var disabled = await SeedEligibleIdentityAsync(database);
    var disabledIdentityId = await IdentityIdAsync(database, disabled);
    var principalId = await RegisterAndGrantAsync(database, disabledIdentityId, PlatformPermissionNames.AdministerPlatformSupport);
    await DisableAsync(database, principalId);
    var recovery = await SeedEligibleIdentityAsync(database);

    var outcome = await RunBootstrapAsync(database, Options([disabled, recovery]));

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    // Two principals now exist; the disabled one is untouched (no implicit re-enable) and recovery got a new one.
    Assert.Equal(2, await ReadInt32Async(database, "SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals]"));
    Assert.Equal("Disabled", await ReadStringAsync(
      database,
      $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
    var recoveryIdentityId = await IdentityIdAsync(database, recovery);
    Assert.Equal(1, await ReadInt32Async(
      database,
      $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE [IdentityId] = {recoveryIdentityId} AND [Status] = N'Active'"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0020")]
  public async Task Only_a_disabled_candidate_fails_closed_without_reactivation()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var disabled = await SeedEligibleIdentityAsync(database);
    var identityId = await IdentityIdAsync(database, disabled);
    var principalId = await RegisterAndGrantAsync(database, identityId, PlatformPermissionNames.AdministerPlatformSupport);
    await DisableAsync(database, principalId);

    var outcome = await RunBootstrapAsync(database, Options([disabled]));

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
    // The disabled principal is neither reactivated nor duplicated.
    Assert.Equal(1, await ReadInt32Async(database, "SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals]"));
    Assert.Equal("Disabled", await ReadStringAsync(
      database,
      $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
    Assert.False(await ReadUsableAuthorityAsync(database));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task A_fully_revoked_authority_recovers_through_a_new_principal_not_reactivation()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var revoked = await SeedEligibleIdentityAsync(database);
    var identityId = await IdentityIdAsync(database, revoked);
    var principalId = await RegisterAndGrantAsync(database, identityId, PlatformPermissionNames.AdministerPlatformSupport);
    await RevokeAsync(database, principalId, PlatformPermissionNames.AdministerPlatformSupport);
    Assert.False(await ReadUsableAuthorityAsync(database)); // Active principal, but no active PlatformSupport assignment.
    var recovery = await SeedEligibleIdentityAsync(database);

    var outcome = await RunBootstrapAsync(database, Options([revoked, recovery]));

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    var recoveryIdentityId = await IdentityIdAsync(database, recovery);
    Assert.Equal(1, await ReadInt32Async(
      database,
      $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE [IdentityId] = {recoveryIdentityId}"));
    Assert.True(await ReadUsableAuthorityAsync(database));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task A_corrupt_tenant_scoped_assignment_does_not_lock_out_recovery()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var corrupt = await SeedEligibleIdentityAsync(database);
    var identityId = await IdentityIdAsync(database, corrupt);
    var principalId = await RegisterAndGrantAsync(database, identityId, PlatformPermissionNames.AdministerPlatformSupport);
    await RevokeAsync(database, principalId, PlatformPermissionNames.AdministerPlatformSupport);

    // Force-seed a corrupt Tenant-scoped assignment bypassing the write-side guard; it must not count as authority.
    await using (var seed = database.CreateContext())
    {
      await seed.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT INTO [platform].[PlatformPermissionAssignments] ([PlatformSupportPrincipalId], [PermissionName], [AssignedUtc], [AssignedBy]) VALUES ({principalId}, {PlatformPermissionNames.ViewCompanies}, {BootstrapSqlDatabase.Now}, {"corruption-test"})");
    }

    Assert.False(await ReadUsableAuthorityAsync(database));
    var recovery = await SeedEligibleIdentityAsync(database);

    var outcome = await RunBootstrapAsync(database, Options([corrupt, recovery]));

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.True(await ReadUsableAuthorityAsync(database));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0019")]
  public async Task Concurrent_bootstrap_converges_on_exactly_one_principal()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var subject = await SeedEligibleIdentityAsync(database);
    var options = Options([subject]);

    // Multiple hosts race on the same deterministically selected subject.
    var outcomes = await Task.WhenAll(
      Enumerable.Range(0, 4).Select(_ => RunBootstrapAsync(database, options)));

    Assert.Equal(1, outcomes.Count(outcome => outcome == PlatformSupportBootstrapOutcome.GenesisEstablished));
    Assert.DoesNotContain(outcomes, outcome => outcome == PlatformSupportBootstrapOutcome.NoCandidatesConfigured);
    // Exactly one principal exists: the IdentityId uniqueness rejected every double-genesis attempt.
    Assert.Equal(1, await ReadInt32Async(database, "SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals]"));
    Assert.Equal(1, await ReadInt32Async(
      database,
      $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE [PermissionName] = N'{PlatformPermissionNames.AdministerPlatformSupport}' AND [RemovedUtc] IS NULL"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0021")]
  public async Task Established_genesis_never_leaves_a_principal_without_its_administer_grant()
  {
    await using var database = await BootstrapSqlDatabase.CreateAsync();
    var subject = await SeedEligibleIdentityAsync(database);

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, await RunBootstrapAsync(database, Options([subject])));

    // Atomic genesis invariant: no principal row can exist without an active Platform.Support.Administer grant.
    Assert.Equal(0, await ReadInt32Async(
      database,
      $@"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] p
         WHERE NOT EXISTS (
           SELECT 1 FROM [platform].[PlatformPermissionAssignments] a
           WHERE a.[PlatformSupportPrincipalId] = p.[PlatformSupportPrincipalId]
             AND a.[PermissionName] = N'{PlatformPermissionNames.AdministerPlatformSupport}'
             AND a.[RemovedUtc] IS NULL)"));
  }

  // ---- Orchestrator + read-service construction over the real provider ----

  private static IOptions<PlatformSupportBootstrapOptions> Options(string[] subjects) =>
    Microsoft.Extensions.Options.Options.Create(new PlatformSupportBootstrapOptions
    {
      Subjects = subjects,
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport]
    });

  private static async Task<PlatformSupportBootstrapOutcome> RunBootstrapAsync(
    BootstrapSqlDatabase database,
    IOptions<PlatformSupportBootstrapOptions> options)
  {
    await using var context = database.CreateContext();
    var catalog = new PlatformPermissionCatalog();
    var accounts = new AuthenticationAccountRepository(context);
    var readService = new PlatformSupportAuthorityStateReadService(context, accounts, catalog);
    var service = new PlatformSupportBootstrapService(
      options,
      new IdentityRepository(context),
      accounts,
      new PlatformSupportPrincipalRepository(context),
      readService,
      catalog,
      new TestPlatformUnitOfWork(context),
      new TestClock());
    return await service.RunAsync();
  }

  private static async Task<bool> ReadUsableAuthorityAsync(BootstrapSqlDatabase database)
  {
    await using var context = database.CreateContext();
    var accounts = new AuthenticationAccountRepository(context);
    var readService = new PlatformSupportAuthorityStateReadService(context, accounts, new PlatformPermissionCatalog());
    return await readService.HasUsablePlatformAuthorityAsync();
  }

  // ---- Seeding ----

  private static async Task<string> SeedEligibleIdentityAsync(BootstrapSqlDatabase database) =>
    await SeedIdentityAsync(database, eligible: true);

  private static async Task<string> SeedIdentityAsync(BootstrapSqlDatabase database, bool eligible)
  {
    var subject = $"local:{Guid.NewGuid():N}";
    await using var context = database.CreateContext();
    var identity = Identity.Create(AuthenticationSubject.Create(subject).Value);
    context.Identities.Add(identity);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);

    var account = AuthenticationAccount.CreatePending(
      identity.Id,
      LoginEmail.Create($"{subject.Replace(":", "-", StringComparison.Ordinal)}@example.com").Value);
    if (eligible)
    {
      Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), BootstrapSqlDatabase.Now).IsSuccess);
    }

    context.AuthenticationAccounts.Add(account);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return subject;
  }

  private static async Task<long> IdentityIdAsync(BootstrapSqlDatabase database, string subject)
  {
    await using var context = database.CreateContext();
    var identity = await new IdentityRepository(context).GetBySubjectAsync(subject);
    Assert.NotNull(identity);
    return identity!.Id;
  }

  private static async Task<long> RegisterAndGrantAsync(BootstrapSqlDatabase database, long identityId, string permissionName)
  {
    long principalId;
    await using (var context = database.CreateContext())
    {
      var register = new RegisterPlatformSupportPrincipalCommandHandler(
        new PlatformSupportPrincipalRepository(context), new TestPlatformUnitOfWork(context), new TestCurrentUser());
      principalId = (await register.HandleAsync(new RegisterPlatformSupportPrincipalCommand(identityId))).Value;
    }

    await using (var context = database.CreateContext())
    {
      var grant = new GrantPlatformPermissionCommandHandler(
        new PlatformSupportPrincipalRepository(context), new PlatformPermissionCatalog(),
        new TestPlatformUnitOfWork(context), new TestCurrentUser(), new TestClock());
      Assert.True((await grant.HandleAsync(new GrantPlatformPermissionCommand(principalId, permissionName))).IsSuccess);
    }

    return principalId;
  }

  private static async Task RevokeAsync(BootstrapSqlDatabase database, long principalId, string permissionName)
  {
    await using var context = database.CreateContext();
    var revoke = new RevokePlatformPermissionCommandHandler(
      new PlatformSupportPrincipalRepository(context), new TestPlatformUnitOfWork(context), new TestCurrentUser(), new TestClock());
    Assert.True((await revoke.HandleAsync(new RevokePlatformPermissionCommand(principalId, permissionName))).IsSuccess);
  }

  private static async Task DisableAsync(BootstrapSqlDatabase database, long principalId)
  {
    byte[] version;
    await using (var read = database.CreateContext())
    {
      version = (await read.PlatformSupportPrincipals.AsNoTracking().SingleAsync(p => p.Id == principalId)).RowVersion;
    }

    await using var context = database.CreateContext();
    var disable = new DisablePlatformSupportPrincipalCommandHandler(
      new PlatformSupportPrincipalRepository(context), new PlatformAuthenticationSessionRepository(context), new TestPlatformUnitOfWork(context), new TestCurrentUser(), new TestClock());
    Assert.True((await disable.HandleAsync(new DisablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
  }

  // ---- Raw SQL readers ----

  private static async Task<int> ReadInt32Async(BootstrapSqlDatabase database, string commandText)
  {
    await using var context = database.CreateContext();
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private static async Task<string> ReadStringAsync(BootstrapSqlDatabase database, string commandText)
  {
    await using var context = database.CreateContext();
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
  }

  // ---- Harness ----

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

  private sealed class BootstrapSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 10, 11, 0, 0, TimeSpan.Zero);

    public static async Task<BootstrapSqlDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP003_BOOT_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new BootstrapSqlDatabase(builder.ConnectionString);
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
    public DateTimeOffset UtcNow => BootstrapSqlDatabase.Now;
  }
}
