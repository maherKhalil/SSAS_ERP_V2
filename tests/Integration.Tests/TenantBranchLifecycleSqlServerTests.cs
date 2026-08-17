using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Branches;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// THE BRANCH LIFECYCLE AGAINST REAL SQL SERVER (Branch foundation B1a).
//
// Against real SQL because every rule this slice adds is enforced by something only a real server has: a
// filtered unique index, a rowversion, an application lock, and a cross-database read. An in-memory
// provider would agree with all of them and prove none.
[Trait("Category", "SqlServer")]
public sealed class TenantBranchLifecycleSqlServerTests
{
  // ---- A. THE FIRST BRANCH IS ACTIVE AND MAIN, whatever the caller asked for.
  [Fact]
  public async Task The_first_branch_a_tenant_creates_becomes_its_active_main_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();

    // Asked for explicitly as NOT main: the domain rule must still win, or a tenant emerges from onboarding
    // with no main branch and nothing to default to.
    var created = await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", false));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.True(created.Value.IsMainBranch);
    Assert.True(created.Value.IsActive);
  }

  // ---- B + C. CODE UNIQUENESS IS PER TENANT, and case-insensitive through normalization.
  [Fact]
  public async Task A_branch_code_is_unique_within_a_tenant_and_free_in_another()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();

    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var duplicate = await service.CreateAsync(new CreateBranchRequest("ruh", "Riyadh Again", false));
    Assert.True(duplicate.IsFailure);
    Assert.Equal(BranchErrors.CodeAlreadyExists.Code, duplicate.Error.Code);

    // The same code in a DIFFERENT tenant is a different branch entirely.
    var other = await fixture.ServiceForTenantB().CreateAsync(new CreateBranchRequest("RUH", "Riyadh B", true));
    Assert.True(other.IsSuccess, other.IsFailure ? other.Error.Code : null);
  }

  // ---- D. A SECOND MAIN IS REFUSED AT CREATE. Promotion is Update's job, atomically.
  [Fact]
  public async Task A_second_main_branch_cannot_be_created_alongside_an_existing_one()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var second = await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", true));

    Assert.True(second.IsFailure);
    Assert.Equal(BranchErrors.MainBranchAlreadyExists.Code, second.Error.Code);
  }

  // ---- E + F. A VALID SWITCH LEAVES EXACTLY ONE ACTIVE MAIN, and renaming works.
  [Fact]
  public async Task Promoting_a_branch_moves_the_main_flag_and_leaves_exactly_one()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var promoted = await service.UpdateAsync(
      new UpdateBranchRequest(jeddah.BranchId, "JED", "Jeddah Main", true, jeddah.RowVersion));

    Assert.True(promoted.IsSuccess, promoted.IsFailure ? promoted.Error.Code : null);
    Assert.True(promoted.Value.IsMainBranch);
    Assert.Equal("Jeddah Main", promoted.Value.BranchName);

    var mains = (await service.ListAsync()).Value.Where(branch => branch.IsMainBranch).ToArray();
    Assert.Single(mains);
    Assert.Equal(jeddah.BranchId, mains[0].BranchId);
    Assert.False((await service.GetAsync(riyadh.BranchId)).Value.IsMainBranch);
  }

  // ---- G. A STALE ROWVERSION IS REFUSED rather than silently overwriting a peer's edit.
  [Fact]
  public async Task An_update_carrying_a_stale_row_version_is_refused()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var branch = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    var first = await service.UpdateAsync(
      new UpdateBranchRequest(branch.BranchId, "RUH", "Riyadh One", true, branch.RowVersion));
    Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);

    // Same original token, now superseded.
    var stale = await service.UpdateAsync(
      new UpdateBranchRequest(branch.BranchId, "RUH", "Riyadh Two", true, branch.RowVersion));

    Assert.True(stale.IsFailure);
    Assert.Equal(BranchErrors.ConcurrencyConflict.Code, stale.Error.Code);
    Assert.Equal("Riyadh One", (await service.GetAsync(branch.BranchId)).Value.BranchName);
  }

  // ---- H + N + O. DEACTIVATION RETIRES WITHOUT DELETING.
  [Fact]
  public async Task Deactivating_a_non_main_branch_retains_it_as_history()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var deactivated = await service.DeactivateAsync(
      new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion));
    Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);

    // Gone from the active list, still explicable in the administration view — and STILL A ROW.
    Assert.DoesNotContain((await service.ListAsync()).Value, branch => branch.BranchId == jeddah.BranchId);
    Assert.Contains((await service.ListAsync(includeInactive: true)).Value,
      branch => branch.BranchId == jeddah.BranchId && !branch.IsActive);
    Assert.Equal(2, await fixture.BranchRowCountAsync());
  }

  // ---- I. RETIRING THE MAIN BRANCH MOVES THE FLAG IN THE SAME TRANSACTION.
  [Fact]
  public async Task Deactivating_the_main_branch_promotes_its_named_replacement_atomically()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    // Without a named successor the tenant would be left with active branches and no main.
    var unnamed = await service.DeactivateAsync(
      new DeactivateBranchRequest(riyadh.BranchId, null, riyadh.RowVersion));
    Assert.True(unnamed.IsFailure);
    Assert.Equal(BranchErrors.ReplacementMainBranchRequired.Code, unnamed.Error.Code);

    var retired = await service.DeactivateAsync(
      new DeactivateBranchRequest(riyadh.BranchId, jeddah.BranchId, riyadh.RowVersion));
    Assert.True(retired.IsSuccess, retired.IsFailure ? retired.Error.Code : null);

    var active = (await service.ListAsync()).Value;
    var main = Assert.Single(active, branch => branch.IsMainBranch);
    Assert.Equal(jeddah.BranchId, main.BranchId);
    Assert.False((await service.GetAsync(riyadh.BranchId)).Value.IsActive);
  }

  // ---- J. ONBOARDING IS NOT REVERSIBLE.
  [Fact]
  public async Task The_only_active_branch_cannot_be_deactivated()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var only = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    var refused = await service.DeactivateAsync(
      new DeactivateBranchRequest(only.BranchId, null, only.RowVersion));

    Assert.True(refused.IsFailure);
    Assert.Equal(BranchErrors.CannotDeactivateOnlyActiveBranch.Code, refused.Error.Code);
    Assert.True((await service.GetAsync(only.BranchId)).Value.IsActive);
  }

  // ---- K + L. THE STRANDED-USER RULE, READ ACROSS THE PLANE BOUNDARY.
  [Fact]
  public async Task A_deactivation_that_would_strand_a_normal_user_is_refused_until_they_have_another()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    // A normal user who can only reach Jeddah.
    var userId = await fixture.SeedNormalUserAsync("clerk@example.test");
    await fixture.GrantBranchAsync(userId, jeddah.BranchId);

    var refused = await service.DeactivateAsync(
      new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion));
    Assert.True(refused.IsFailure);
    Assert.Equal(BranchErrors.DeactivationWouldStrandUsers.Code, refused.Error.Code);
    Assert.True((await service.GetAsync(jeddah.BranchId)).Value.IsActive);

    // Give them Riyadh as well and the same deactivation becomes safe.
    await fixture.GrantBranchAsync(userId, riyadh.BranchId);
    var allowed = await service.DeactivateAsync(
      new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion));

    Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Error.Code : null);

    // THE ASSIGNMENT ROW SURVIVES the deactivation: it is history, and the resolver intersects with active
    // branches rather than relying on rows being deleted.
    Assert.Equal(2, await fixture.AccessRowCountAsync(userId));
  }

  // ---- M. A TENANT ADMINISTRATOR IS NEVER STRANDED, and needs no assignment rows to prove it.
  [Fact]
  public async Task A_tenant_administrator_holds_no_access_rows_and_never_blocks_a_deactivation()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    Assert.Equal(0, await fixture.AccessRowCountAsync(fixture.AdministratorUserId));

    // Even with an access row naming only this branch, the administrator's implicit scope exempts them.
    await fixture.GrantBranchAsync(fixture.AdministratorUserId, jeddah.BranchId);
    var deactivated = await service.DeactivateAsync(
      new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion));

    Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);
  }

  // ---- ONBOARDING STATE, the primitive B1c consumes.
  [Fact]
  public async Task The_onboarding_state_reports_first_branch_required_until_one_exists()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();

    var before = await service.GetOnboardingStateAsync();
    Assert.True(before.Value.FirstBranchRequired);
    Assert.Equal(0, before.Value.ActiveBranchCount);

    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var after = await service.GetOnboardingStateAsync();
    Assert.False(after.Value.FirstBranchRequired);
    Assert.Equal(1, after.Value.ActiveBranchCount);
  }

  // ---- AUTHORITY. Branch administration requires Platform.Tenant.Administer, asked of the database.
  [Fact]
  public async Task A_user_without_tenant_administrator_authority_cannot_administer_branches()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    Assert.True((await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var normalUserId = await fixture.SeedNormalUserAsync("nobody@example.test");
    var asNormalUser = fixture.Service(normalUserId);

    var created = await asNormalUser.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false));
    Assert.True(created.IsFailure);
    Assert.Equal(BranchErrors.TenantAdministratorRequired.Code, created.Error.Code);

    // ...and the authority is functional-permission independent: listing is refused too.
    Assert.True((await asNormalUser.ListAsync()).IsFailure);
  }

  // ---- R. CROSS-TENANT REFERENCES ARE SIMPLY NOT FOUND.
  [Fact]
  public async Task A_branch_from_another_tenant_cannot_be_read_or_updated()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    Assert.True((await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var foreign = (await fixture.ServiceForTenantB().CreateAsync(
      new CreateBranchRequest("JED", "Jeddah B", true))).Value;

    var read = await fixture.Service().GetAsync(foreign.BranchId);
    Assert.True(read.IsFailure);
    Assert.Equal(BranchErrors.NotFound.Code, read.Error.Code);

    var written = await fixture.Service().UpdateAsync(
      new UpdateBranchRequest(foreign.BranchId, "JED", "Hijacked", true, foreign.RowVersion));
    Assert.True(written.IsFailure);
    Assert.Equal(BranchErrors.NotFound.Code, written.Error.Code);
  }

  // ---- P. CONCURRENT CREATES OF THE SAME CODE: the database decides, exactly once.
  [Fact]
  public async Task Two_concurrent_creates_of_the_same_code_produce_exactly_one_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    Assert.True((await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var results = await Task.WhenAll(
      Task.Run(() => fixture.Service().CreateAsync(new CreateBranchRequest("JED", "Jeddah A", false))),
      Task.Run(() => fixture.Service().CreateAsync(new CreateBranchRequest("JED", "Jeddah B", false))));

    Assert.Single(results, result => result.IsSuccess);
    var loser = Assert.Single(results, result => result.IsFailure);
    Assert.Equal(BranchErrors.CodeAlreadyExists.Code, loser.Error.Code);
    Assert.Equal(2, await fixture.BranchRowCountAsync());
  }

  // ---- Q. CONCURRENT PROMOTION: the topology lock serialises, and one active main survives.
  [Fact]
  public async Task Two_concurrent_promotions_leave_exactly_one_active_main_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;
    var dammam = (await service.CreateAsync(new CreateBranchRequest("DMM", "Dammam", false))).Value;

    await Task.WhenAll(
      Task.Run(() => fixture.Service().UpdateAsync(
        new UpdateBranchRequest(jeddah.BranchId, "JED", "Jeddah", true, jeddah.RowVersion))),
      Task.Run(() => fixture.Service().UpdateAsync(
        new UpdateBranchRequest(dammam.BranchId, "DMM", "Dammam", true, dammam.RowVersion))));

    // Whoever won, the tenant has exactly one active main — never zero and never two.
    Assert.Single((await service.ListAsync()).Value, branch => branch.IsMainBranch);
  }

  // ---- B0 REGRESSION: the branch foundation did not disturb the Phase E tenant write fence.
  [Fact]
  public async Task Branch_writes_still_pass_through_the_phase_e_tenant_write_fence()
  {
    await using var fixture = await BranchFixture.CreateAsync();

    // The fence is a real sp_getapplock round trip on the tenant connection for every application write.
    // A branch create is an ordinary tenant write, so it must have taken it and still succeeded.
    Assert.True((await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    Assert.Equal(1, await fixture.BranchRowCountAsync());

    // And it still REFUSES when the tenant is frozen mid-cutover. The fence signals by throwing, which the
    // branch service deliberately does not swallow: a frozen tenant is an infrastructure state, not a
    // validation failure to be folded into a Result the caller might treat as ordinary.
    await fixture.FreezeTenantAsync();

    await Assert.ThrowsAsync<TenantStorageUnavailableException>(
      () => fixture.Service().CreateAsync(new CreateBranchRequest("JED", "Jeddah", false)));

    // Nothing landed: branch persistence is fenced exactly as Company persistence is.
    Assert.Equal(1, await fixture.BranchRowCountAsync());
  }

  private sealed class BranchFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "branch-b1a-tests";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private string platformCatalog = string.Empty;

    public string TenantCatalog { get; private set; } = string.Empty;

    public string TenantCatalogB { get; private set; } = string.Empty;

    public Guid TenantA { get; private set; }

    public Guid TenantB { get; private set; }

    public long AdministratorUserId { get; private set; }

    public long AdministratorUserIdB { get; private set; }

    public static async Task<BranchFixture> CreateAsync()
    {
      var fixture = new BranchFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    private async Task InitializeAsync()
    {
      platformCatalog = $"SSAS_B1A_Platform_{token}";
      TenantCatalog = $"SSAS_B1A_Tenant_{token}";
      TenantCatalogB = $"SSAS_B1A_TenantB_{token}";

      foreach (var catalog in new[] { platformCatalog, TenantCatalog, TenantCatalogB })
      {
        await ExecuteAsync("master", $"CREATE DATABASE [{catalog}]");
      }

      foreach (var catalog in new[] { TenantCatalog, TenantCatalogB })
      {
        await using var connection = new SqlConnection(ConnectionFor(catalog));
        var options = new DbContextOptionsBuilder<TenantDbContext>()
          .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
            TenantPersistenceConstants.MigrationHistoryTable,
            TenantPersistenceConstants.MigrationHistorySchema))
          .Options;
        await using var context = new TenantDbContext(
          options, new TestUser(), new TestTenant(null), new TestClock());
        await context.Database.MigrateAsync();
      }

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      var databaseA = await RegisterAsync(platform, TenantCatalog);
      var databaseB = await RegisterAsync(platform, TenantCatalogB);

      TenantA = await SeedTenantAsync(platform, "B1AAA", databaseA);
      TenantB = await SeedTenantAsync(platform, "B1BBB", databaseB);

      AdministratorUserId = await SeedAdministratorAsync(TenantA, "admin-a@example.test");
      AdministratorUserIdB = await SeedAdministratorAsync(TenantB, "admin-b@example.test");
    }

    // The production service graph, wired against the real databases.
    public TenantBranchService Service(long? asUserId = null) =>
      BuildService(TenantA, asUserId ?? AdministratorUserId);

    public TenantBranchService ServiceForTenantB() => BuildService(TenantB, AdministratorUserIdB);

    private TenantBranchService BuildService(Guid tenantId, long tenantUserId)
    {
      var platform = PlatformContext();
      var authority = new TenantAdministratorAuthority(platform);
      var factory = new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(tenantId), new TestClock(),
        new TenantCutoverWriteFence(
          new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5)),
          Options.Create(freeze)));

      return new TenantBranchService(
        platform, factory, authority, new TestTenant(tenantId), new TestSession(tenantId, tenantUserId));
    }

    // Seeding writes TENANT-OWNED rows (TenantUser, Role, the assignments), so the context must carry the
    // tenant the write-side guard demands — the same rule production code obeys.
    public async Task<long> SeedNormalUserAsync(string email)
    {
      await using var platform = PlatformContext(TenantA);
      return await AddUserAsync(platform, TenantA, email, administrator: false);
    }

    private async Task<long> SeedAdministratorAsync(Guid tenantId, string email)
    {
      await using var platform = PlatformContext(tenantId);
      return await AddUserAsync(platform, tenantId, email, administrator: true);
    }

    private static async Task<long> AddUserAsync(
      PlatformDbContext platform, Guid tenantId, string email, bool administrator)
    {
      var identity = Identity.Create(AuthenticationSubject.Create($"sub-{Guid.NewGuid():N}").Value);
      platform.Identities.Add(identity);
      await platform.SaveChangesAsync();

      var user = TenantUser.CreateActive(
        identity.Id, tenantId, EmailAddress.Create(email).Value,
        UserDisplayName.Create("Test User").Value, Guid.NewGuid(), Now);
      platform.TenantUsers.Add(user);
      await platform.SaveChangesAsync();

      if (!administrator)
      {
        return user.Id;
      }

      // AUTHORITY THROUGH AN ORDINARY ROLE, exactly as production grants it — a custom tenant role holding
      // Platform.Tenant.Administer. Nothing in the fixture sets a flag the production code would not see.
      var role = Role.CreateCustom(
        tenantId, RoleName.Create($"Branch Admins {Guid.NewGuid():N}"[..24]).Value, null, Guid.NewGuid(), Now);
      platform.Roles.Add(role);
      await platform.SaveChangesAsync();

      var definition = new PermissionDefinition(
        PermissionName.Create(PlatformPermissionNames.AdministerTenant).Value,
        PermissionScope.Tenant,
        "Administer the tenant");
      Assert.True(role.AssignPermission(definition, Actor, Guid.NewGuid(), Now).IsSuccess);
      Assert.True(user.AssignRole(role, Actor, Guid.NewGuid(), Now).IsSuccess);
      await platform.SaveChangesAsync();

      return user.Id;
    }

    public async Task GrantBranchAsync(long tenantUserId, Guid branchId)
    {
      await using var platform = PlatformContext();
      platform.UserBranchAccess.Add(UserBranchAccess.Create(TenantA, tenantUserId, branchId).Value);
      await platform.SaveChangesAsync();
    }

    public async Task<int> AccessRowCountAsync(long tenantUserId)
    {
      await using var platform = PlatformContext();
      return await platform.UserBranchAccess
        .CountAsync(access => access.TenantId == TenantA && access.TenantUserId == tenantUserId);
    }

    public async Task<int> BranchRowCountAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor(TenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT COUNT(*) FROM [tenant].[Branches]";
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    // Drives the tenant into the Phase E frozen state so the fence's refusal can be observed on a
    // branch-aware write path.
    public async Task FreezeTenantAsync()
    {
      await using var platform = PlatformContext();
      var databaseId = await platform.TenantDatabaseAssignments
        .Where(assignment => assignment.TenantId == TenantA && assignment.EndedUtc == null)
        .Select(assignment => assignment.TenantDatabaseId)
        .SingleAsync();

      var store = new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5));
      var begun = await store.BeginAsync(
        new SSAS.Platform.Application.Abstractions.Persistence.TenantCutoverBeginRequest(
          TenantA, databaseId, databaseId + 1, Actor));
      if (begun.IsSuccess)
      {
        await store.RequestFreezeAsync(begun.Value, Actor);
        await store.FreezeAsync(begun.Value, Actor);
      }
    }

    private static async Task<long> RegisterAsync(PlatformDbContext platform, string databaseName)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Dedicated, ServerKey,
        databaseName, TenantDatabaseProvisioningStatus.Ready, Actor, Now).Value;

      var observedUtc = DateTimeOffset.UtcNow;
      database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, Actor, observedUtc);
      database.RecordSchemaHealth(
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, null, null, Actor, observedUtc);
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    private static async Task<Guid> SeedTenantAsync(PlatformDbContext platform, string code, long databaseId)
    {
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Branch {code}").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(tenant.Id, databaseId, "b1a", Actor, Now).Value);
      await platform.SaveChangesAsync();
      return tenant.Id;
    }

    private PlatformDbContext PlatformContext(Guid? tenantId = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(platformCatalog))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new TestTenant(tenantId), new TestClock());
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=true;TrustServerCertificate=true";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    private static async Task ExecuteAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { TenantCatalog, TenantCatalogB, platformCatalog })
      {
        try
        {
          await ExecuteAsync("master",
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]");
        }
        catch (SqlException)
        {
          // A catalog that never got created, or is still held by a pooled connection, must not fail the
          // test that already made its point.
        }
      }
    }

    private sealed class TestUser : ICurrentUser
    {
      public string? UserId => Actor;

      public string? UserName => Actor;

      public string? Email => null;

      public Guid? CompanyId => null;

      public string? SessionId => null;

      public string? TokenId => null;

      public IReadOnlyCollection<string> Roles => [];

      public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestTenant(Guid? tenantId) : ICurrentTenant
    {
      public Guid? TenantId => tenantId;
    }

    private sealed class TestClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class TestSession(Guid tenantId, long tenantUserId) : ICurrentAuthenticationSession
    {
      public CurrentAuthenticationSession? Value => new(
        1, tenantId, tenantUserId, 1, AuthenticationClientId.Create("web").Value, 1);
    }
  }
}
