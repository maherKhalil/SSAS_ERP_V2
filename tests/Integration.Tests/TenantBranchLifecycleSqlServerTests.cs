using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.TenantUsers;
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

  // ================= B1b: MANDATORY USER BRANCH ASSIGNMENTS =================

  // ---- A + B. A NORMAL USER MUST NAME AT LEAST ONE BRANCH, and nothing is persisted when they do not.
  [Fact]
  public async Task A_normal_user_cannot_be_created_without_branches_and_can_be_with_one()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var branch = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    var identityId = await fixture.SeedIdentityAsync();
    var refused = await fixture.CreateUser().HandleAsync(
      new CreateTenantUserMembershipCommand(identityId, "none@example.test", "No Branch", [], []));

    Assert.True(refused.IsFailure);
    Assert.Equal(BranchErrors.UserMustHaveAtLeastOneBranch.Code, refused.Error.Code);
    Assert.Equal(0, await fixture.UserRowCountAsync("none@example.test"));

    var created = await fixture.CreateUser().HandleAsync(
      new CreateTenantUserMembershipCommand(identityId, "ok@example.test", "One Branch", [], [branch.BranchId]));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.Equal(1, await fixture.AccessRowCountAsync(created.Value));
  }

  // ---- D + E + F. ONE BAD BRANCH REJECTS THE WHOLE REQUEST, AND NO USER SURVIVES IT.
  [Theory]
  [InlineData("inactive")]
  [InlineData("foreign")]
  [InlineData("unknown")]
  public async Task One_unassignable_branch_rolls_the_whole_user_creation_back(string flavour)
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var good = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var spare = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var bad = flavour switch
    {
      "inactive" => await fixture.DeactivatedBranchAsync(spare),
      "foreign" => (await fixture.ServiceForTenantB().CreateAsync(
        new CreateBranchRequest("XXX", "Foreign", true))).Value.BranchId,
      _ => Guid.NewGuid()
    };

    var identityId = await fixture.SeedIdentityAsync();
    var result = await fixture.CreateUser().HandleAsync(
      new CreateTenantUserMembershipCommand(
        identityId, "partial@example.test", "Partial", [], [good.BranchId, bad]));

    Assert.True(result.IsFailure);
    Assert.Equal(BranchErrors.AssignmentInvalid.Code, result.Error.Code);

    // NO PARTIAL USER, and no partial assignment either.
    Assert.Equal(0, await fixture.UserRowCountAsync("partial@example.test"));
  }

  // ---- H + I + J. THE ADMINISTRATOR EXEMPTION, end to end.
  [Fact]
  public async Task An_administrator_is_created_without_branches_and_sees_every_active_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();

    // Created BEFORE the tenant has any branch at all — the case that makes the exemption necessary.
    var roleId = await fixture.SeedAdministratorRoleAsync();
    var identityId = await fixture.SeedIdentityAsync();
    var created = await fixture.CreateUser().HandleAsync(
      new CreateTenantUserMembershipCommand(identityId, "newadmin@example.test", "Admin", [roleId], []));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.Equal(0, await fixture.AccessRowCountAsync(created.Value));

    // Branches created afterwards appear in scope with no backfill of access rows.
    var riyadh = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await fixture.Service().CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var scope = await fixture.Resolver().GetPermittedBranchesAsync(fixture.TenantA, created.Value);
    Assert.True(scope.IsSuccess, scope.IsFailure ? scope.Error.Code : null);
    Assert.Equal(
      new[] { jeddah.BranchId, riyadh.BranchId }.OrderBy(id => id),
      scope.Value.Select(branch => branch.BranchId).OrderBy(id => id));
    Assert.Equal(0, await fixture.AccessRowCountAsync(created.Value));
  }

  // ---- K + L + T + U. THE REPLACE-SET UPDATE AND THE NEVER-ZERO RULE.
  [Fact]
  public async Task A_branch_set_can_be_narrowed_but_never_emptied_for_an_active_normal_user()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var identityId = await fixture.SeedIdentityAsync();
    var userId = (await fixture.CreateUser().HandleAsync(new CreateTenantUserMembershipCommand(
      identityId, "clerk@example.test", "Clerk", [], [riyadh.BranchId, jeddah.BranchId]))).Value;

    // Riyadh + Jeddah -> Jeddah: allowed.
    var narrowed = await fixture.SetBranches().HandleAsync(
      new SetTenantUserBranchesCommand(userId, [jeddah.BranchId]));
    Assert.True(narrowed.IsSuccess, narrowed.IsFailure ? narrowed.Error.Code : null);
    Assert.Equal(1, await fixture.AccessRowCountAsync(userId));

    // Jeddah -> []: refused, and the surviving assignment is untouched.
    var emptied = await fixture.SetBranches().HandleAsync(new SetTenantUserBranchesCommand(userId, []));
    Assert.True(emptied.IsFailure);
    Assert.Equal(BranchErrors.UserMustHaveAtLeastOneBranch.Code, emptied.Error.Code);
    Assert.Equal(1, await fixture.AccessRowCountAsync(userId));

    // The resolver sees exactly the surviving ACTIVE branch — a retained row for an inactive branch would
    // not have granted anything either.
    var scope = await fixture.Resolver().GetPermittedBranchesAsync(fixture.TenantA, userId);
    Assert.Equal(jeddah.BranchId, Assert.Single(scope.Value).BranchId);
  }

  // ---- Q. R1: DEACTIVATION vs A CONCURRENT NARROWING OF THE SURVIVING BRANCH.
  //
  // The B1a race, now closed. Admin 1 retires Riyadh believing Jeddah keeps the user safe; Admin 2
  // simultaneously narrows that same user to Riyadh only. Serialised on one tenant resource, whichever runs
  // second sees the first's committed effect — so the user cannot end with no active branch.
  [Fact]
  public async Task A_deactivation_racing_a_branch_set_update_cannot_strand_the_user()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var identityId = await fixture.SeedIdentityAsync();
    var userId = (await fixture.CreateUser().HandleAsync(new CreateTenantUserMembershipCommand(
      identityId, "race1@example.test", "Race", [], [riyadh.BranchId, jeddah.BranchId]))).Value;

    await Task.WhenAll(
      Task.Run(() => fixture.Service().DeactivateAsync(
        new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion))),
      Task.Run(() => fixture.SetBranches().HandleAsync(
        new SetTenantUserBranchesCommand(userId, [jeddah.BranchId]))));

    // WHATEVER THE ORDER, the user retains at least one ACTIVE branch.
    var scope = await fixture.Resolver().GetPermittedBranchesAsync(fixture.TenantA, userId);
    Assert.True(scope.IsSuccess, scope.IsFailure ? scope.Error.Code : null);
    Assert.NotEmpty(scope.Value);
  }

  // ---- R. R2: DEACTIVATION vs A CONCURRENT CREATE ASSIGNED ONLY TO THAT BRANCH.
  [Fact]
  public async Task A_deactivation_racing_a_user_creation_cannot_create_a_stranded_user()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var identityId = await fixture.SeedIdentityAsync();

    var outcomes = await Task.WhenAll(
      Task.Run(async () => (object)await fixture.Service().DeactivateAsync(
        new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion))),
      Task.Run(async () => (object)await fixture.CreateUser().HandleAsync(
        new CreateTenantUserMembershipCommand(identityId, "race2@example.test", "Race2", [], [jeddah.BranchId]))));

    // EXACTLY ONE OF THE TWO MAY WIN. If the deactivation went first the creation revalidates Jeddah under
    // the lease and refuses it as inactive; if the creation went first the deactivation sees the new user
    // and refuses to strand them. Both succeeding is the state that must be impossible.
    var deactivated = ((Result)outcomes[0]).IsSuccess;
    var createdUser = (Result<long>)outcomes[1];
    Assert.False(deactivated && createdUser.IsSuccess);

    if (createdUser.IsSuccess)
    {
      var scope = await fixture.Resolver().GetPermittedBranchesAsync(fixture.TenantA, createdUser.Value);
      Assert.NotEmpty(scope.Value);
    }
    else
    {
      Assert.Equal(0, await fixture.UserRowCountAsync("race2@example.test"));
    }
  }

  // ---- S. TWO TENANTS ARE INDEPENDENT: the lease is per tenant, not global.
  [Fact]
  public async Task Two_tenants_can_change_their_branch_topology_independently()
  {
    await using var fixture = await BranchFixture.CreateAsync();

    var results = await Task.WhenAll(
      Task.Run(() => fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))),
      Task.Run(() => fixture.ServiceForTenantB().CreateAsync(new CreateBranchRequest("RUH", "Riyadh B", true))));

    Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null));
  }

  // ---- §20/§24. THE DEACTIVATION-IMPACT QUERY AT REALISTIC CARDINALITY.
  //
  // This is what closes B1a LOW-b: the index was previously matched by SHAPE, which proves nothing. Measured
  // against thousands of assignment rows, because a scan here runs while an administrator waits and grows
  // with the estate.
  [Fact]
  public async Task The_deactivation_impact_query_seeks_its_index_at_realistic_cardinality()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var branch = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    await fixture.SeedAccessRowsAsync(5_000);

    var plan = await fixture.CaptureImpactPlanAsync(branch.BranchId);

    Assert.NotNull(plan);
    Assert.Contains("IX_UserBranchAccess_TenantId_BranchId", plan!.Indexes, StringComparison.Ordinal);
    Assert.Contains("Index Seek", plan.Operations, StringComparison.Ordinal);
    Assert.DoesNotContain("Index Scan", plan.Operations, StringComparison.Ordinal);
    Assert.DoesNotContain("Table Scan", plan.Operations, StringComparison.Ordinal);
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

    // ---- B1b wiring: the real handlers over the real repositories, no fakes.
    public CreateTenantUserMembershipCommandHandler CreateUser()
    {
      var platform = PlatformContext(TenantA);
      return new CreateTenantUserMembershipCommandHandler(
        new IdentityRepository(platform),
        new TenantUserRepository(platform),
        new RoleRepository(platform),
        new UserBranchAccessRepository(platform),
        new TenantAdministratorAuthority(platform),
        new TenantBranchValidator(TenantContextFactory(TenantA)),
        new BranchTopologyGuard(platform),
        new PlatformUnitOfWork(platform, new NoOpDomainEventDispatcher()),
        new TestTenant(TenantA), new TestUser(), new TestClock());
    }

    public SetTenantUserBranchesCommandHandler SetBranches()
    {
      var platform = PlatformContext(TenantA);
      return new SetTenantUserBranchesCommandHandler(
        new TenantUserRepository(platform),
        new UserBranchAccessRepository(platform),
        new TenantAdministratorAuthority(platform),
        new TenantBranchValidator(TenantContextFactory(TenantA)),
        new BranchTopologyGuard(platform),
        new PlatformUnitOfWork(platform, new NoOpDomainEventDispatcher()),
        new TestTenant(TenantA), new TestUser());
    }

    public TenantBranchAccessResolver Resolver()
    {
      var platform = PlatformContext(TenantA);
      return new TenantBranchAccessResolver(
        platform, TenantContextFactory(TenantA), new TenantAdministratorAuthority(platform));
    }

    public async Task<long> SeedIdentityAsync()
    {
      await using var platform = PlatformContext(TenantA);
      var identity = Identity.Create(AuthenticationSubject.Create($"sub-{Guid.NewGuid():N}").Value);
      platform.Identities.Add(identity);
      await platform.SaveChangesAsync();
      return identity.Id;
    }

    // A role carrying Platform.Tenant.Administer, granted the ordinary way.
    public async Task<long> SeedAdministratorRoleAsync()
    {
      await using var platform = PlatformContext(TenantA);
      var role = Role.CreateCustom(
        TenantA, RoleName.Create($"Admins {Guid.NewGuid():N}"[..20]).Value, null, Guid.NewGuid(), Now);
      platform.Roles.Add(role);
      await platform.SaveChangesAsync();

      var definition = new PermissionDefinition(
        PermissionName.Create(PlatformPermissionNames.AdministerTenant).Value,
        PermissionScope.Tenant, "Administer the tenant");
      Assert.True(role.AssignPermission(definition, Actor, Guid.NewGuid(), Now).IsSuccess);
      await platform.SaveChangesAsync();
      return role.Id;
    }

    public async Task<Guid> DeactivatedBranchAsync(BranchDto branch)
    {
      var deactivated = await Service().DeactivateAsync(
        new DeactivateBranchRequest(branch.BranchId, null, branch.RowVersion));
      Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);
      return branch.BranchId;
    }

    // Raw SQL: Email is a value object, so an equality predicate on its inner value does not translate —
    // and the question here is simply whether a row exists at all.
    public async Task<int> UserRowCountAsync(string email)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT COUNT(*) FROM [platform].[TenantUsers] WHERE [TenantId] = @tenant AND [Email] = @email";
      command.Parameters.AddWithValue("@tenant", TenantA);
      command.Parameters.AddWithValue("@email", email);
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    // Bulk assignment rows so the impact query is measured against a realistic estate rather than a
    // one-row fixture, where every plan looks like a seek.
    public async Task SeedAccessRowsAsync(int count)
    {
      // Rows must satisfy the composite FK to TenantUsers, so they hang off a REAL membership. Distinct
      // BranchIds keep them unique and give the (TenantId, BranchId) index a realistic key distribution.
      await ExecuteAsync(platformCatalog, $"""
        SET NOCOUNT ON;
        DECLARE @i INT = 0;
        WHILE @i < {count}
        BEGIN
          INSERT INTO [platform].[UserBranchAccess]
            ([TenantId], [TenantUserId], [BranchId], [CreatedUtc], [ModifiedUtc])
          VALUES ('{TenantA:D}', {AdministratorUserId}, NEWID(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
          SET @i = @i + 1;
        END;
        """);

      await ExecuteAsync(platformCatalog,
        "UPDATE STATISTICS [platform].[UserBranchAccess] WITH FULLSCAN;");
    }

    public async Task<MeasuredPlan?> CaptureImpactPlanAsync(Guid branchId)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();

      // SET SHOWPLAN_XML must be alone in its batch, so the switch and the measured statement are three
      // separate commands on one connection.
      await using (var on = connection.CreateCommand())
      {
        on.CommandText = "SET SHOWPLAN_XML ON";
        await on.ExecuteNonQueryAsync();
      }

      string? xml = null;
      await using (var measured = connection.CreateCommand())
      {
        measured.CommandText = $"""
          SELECT DISTINCT [TenantUserId] FROM [platform].[UserBranchAccess]
          WHERE [TenantId] = '{TenantA:D}' AND [BranchId] = '{branchId:D}'
          """;

        await using var reader = await measured.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
          xml = reader.GetString(0);
        }
      }

      await using (var off = connection.CreateCommand())
      {
        off.CommandText = "SET SHOWPLAN_XML OFF";
        await off.ExecuteNonQueryAsync();
      }

      return xml is null ? null : new MeasuredPlan(xml, xml);
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

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
      public Task DispatchAsync(
        IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    }

    private TenantDbContextFactory TenantContextFactory(Guid tenantId)
    {
      var platform = PlatformContext(tenantId);
      return new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(tenantId), new TestClock(),
        new TenantCutoverWriteFence(
          new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5)),
          Options.Create(freeze)));
    }

    // The plan XML, kept whole: the assertions look for operator and index names inside it.
    public sealed record MeasuredPlan(string Operations, string Indexes);
  }
}
