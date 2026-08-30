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
using SSAS.Platform.Domain.Authentication;
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

  // ================= B1c: ACTIVE BRANCH SESSION FLOW =================

  // ---- A + L. EXACTLY ONE AUTHORIZED BRANCH IS CHOSEN FOR THE USER, and the choice is durable.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task A_session_with_exactly_one_authorized_branch_is_auto_selected(bool administrator)
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var riyadh = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    var userId = administrator
      ? fixture.AdministratorUserId
      : await fixture.SeedUserWithBranchesAsync("one@example.test", riyadh.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);

    var state = await fixture.BranchSessions().ResolveForSessionAsync(sessionId);

    Assert.True(state.IsSuccess, state.IsFailure ? state.Error.Code : null);
    Assert.Equal(BranchSessionOutcome.Active, state.Value.Outcome);
    Assert.Equal(riyadh.BranchId, state.Value.ActiveBranchId);

    // Durable, not just returned.
    Assert.Equal(riyadh.BranchId, await fixture.StoredBranchAsync(sessionId));
  }

  // ---- B + M. MORE THAN ONE MEANS THE USER MUST CHOOSE, and the session stays branch-less meanwhile.
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task A_session_with_several_authorized_branches_must_select_before_working(bool administrator)
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var userId = administrator
      ? fixture.AdministratorUserId
      : await fixture.SeedUserWithBranchesAsync("many@example.test", riyadh.BranchId, jeddah.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);

    var state = await fixture.BranchSessions().ResolveForSessionAsync(sessionId);

    Assert.True(state.IsSuccess, state.IsFailure ? state.Error.Code : null);
    Assert.Equal(BranchSessionOutcome.BranchSelectionRequired, state.Value.Outcome);
    Assert.Null(state.Value.ActiveBranchId);
    Assert.Equal(2, state.Value.SelectableBranches.Count);

    // NO SKIP: nothing was written, so branch-owned work stays refused.
    Assert.Null(await fixture.StoredBranchAsync(sessionId));
  }

  // ---- K. AN ADMINISTRATOR WITH NO BRANCHES IS ONBOARDING, not broken.
  [Fact]
  public async Task An_administrator_whose_tenant_has_no_branches_is_sent_to_onboarding()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var sessionId = await fixture.OpenSessionAsync(fixture.AdministratorUserId);

    var state = await fixture.BranchSessions().ResolveForSessionAsync(sessionId);

    Assert.True(state.IsSuccess, state.IsFailure ? state.Error.Code : null);
    Assert.Equal(BranchSessionOutcome.FirstBranchRequired, state.Value.Outcome);
    Assert.Null(await fixture.StoredBranchAsync(sessionId));
  }

  // ---- O. A NORMAL USER WITH NOTHING REACHABLE FAILS CLOSED. B1b makes this unreachable through supported
  // workflows, so it means the account is in a state it should not be in — never an empty picker.
  [Fact]
  public async Task A_normal_user_with_no_reachable_branch_fails_closed()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    Assert.True((await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var userId = await fixture.SeedUserWithBranchesAsync("orphan@example.test", jeddah.BranchId);

    // Their only branch is retired out from under them, leaving a retained row pointing nowhere active.
    Assert.True((await service.DeactivateAsync(
      new DeactivateBranchRequest(jeddah.BranchId, null, jeddah.RowVersion))).IsFailure);
    await fixture.ForceDeactivateBranchAsync(jeddah.BranchId);

    var state = await fixture.BranchSessions().ResolveForSessionAsync(await fixture.OpenSessionAsync(userId));

    Assert.True(state.IsFailure);
    Assert.Equal(BranchErrors.AccountIntegrityFailure.Code, state.Error.Code);
  }

  // ---- D + E + F + G + H + I + J. SELECTION AND SWITCHING, INCLUDING EVERY REFUSAL.
  [Fact]
  public async Task Branch_selection_and_switching_revalidate_and_never_strand_the_current_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;
    var dammam = (await service.CreateAsync(new CreateBranchRequest("DMM", "Dammam", false))).Value;

    var userId = await fixture.SeedUserWithBranchesAsync(
      "switcher@example.test", riyadh.BranchId, jeddah.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);
    var sessions = fixture.BranchSessions();

    // D. A valid selection lands.
    Assert.True((await sessions.SelectActiveBranchAsync(sessionId, riyadh.BranchId)).IsSuccess);
    Assert.Equal(riyadh.BranchId, await fixture.StoredBranchAsync(sessionId));

    // H. A valid switch moves it.
    Assert.True((await sessions.SelectActiveBranchAsync(sessionId, jeddah.BranchId)).IsSuccess);
    Assert.Equal(jeddah.BranchId, await fixture.StoredBranchAsync(sessionId));

    // J. Re-selecting the current branch is idempotent, not an error.
    Assert.True((await sessions.SelectActiveBranchAsync(sessionId, jeddah.BranchId)).IsSuccess);
    Assert.Equal(jeddah.BranchId, await fixture.StoredBranchAsync(sessionId));

    // E + I. An unauthorized branch is refused AND leaves the current one alone.
    var unauthorized = await sessions.SelectActiveBranchAsync(sessionId, dammam.BranchId);
    Assert.True(unauthorized.IsFailure);
    Assert.Equal(BranchErrors.InvalidSelection.Code, unauthorized.Error.Code);
    Assert.Equal(jeddah.BranchId, await fixture.StoredBranchAsync(sessionId));

    // G. Another tenant's branch is refused identically — no existence disclosure.
    var foreign = (await fixture.ServiceForTenantB().CreateAsync(
      new CreateBranchRequest("XXX", "Foreign", true))).Value;
    var crossTenant = await sessions.SelectActiveBranchAsync(sessionId, foreign.BranchId);
    Assert.True(crossTenant.IsFailure);
    Assert.Equal(BranchErrors.InvalidSelection.Code, crossTenant.Error.Code);
    Assert.Equal(jeddah.BranchId, await fixture.StoredBranchAsync(sessionId));

    // F. A deactivated branch is refused, and the current branch survives.
    await fixture.ForceDeactivateBranchAsync(riyadh.BranchId);
    var inactive = await sessions.SelectActiveBranchAsync(sessionId, riyadh.BranchId);
    Assert.True(inactive.IsFailure);
    Assert.Equal(jeddah.BranchId, await fixture.StoredBranchAsync(sessionId));
  }

  // ---- N. AN ADMINISTRATOR SELECTS THROUGH IMPLICIT SCOPE, holding no assignment rows at all.
  [Fact]
  public async Task An_administrator_selects_a_branch_without_any_access_rows()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var riyadh = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var sessionId = await fixture.OpenSessionAsync(fixture.AdministratorUserId);

    Assert.Equal(0, await fixture.AccessRowCountAsync(fixture.AdministratorUserId));
    var selected = await fixture.BranchSessions().SelectActiveBranchAsync(sessionId, riyadh.BranchId);

    Assert.True(selected.IsSuccess, selected.IsFailure ? selected.Error.Code : null);
    Assert.Equal(riyadh.BranchId, await fixture.StoredBranchAsync(sessionId));
  }

  // ---- P + Q + R + T + U. THE MID-SESSION REVOCATION MATRIX — the security core of B1c.
  //
  // In every case the session still stores a perfectly readable branch id, and in every case the write
  // boundary must refuse. This is what makes the stored branch context rather than authorization.
  [Theory]
  [InlineData("assignment-revoked")]
  [InlineData("branch-deactivated")]
  [InlineData("admin-authority-revoked")]
  [InlineData("session-revoked")]
  [InlineData("session-expired")]
  [InlineData("no-branch-selected")]
  public async Task A_stored_branch_stops_authorizing_writes_the_moment_the_grounds_change(string change)
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    var administrator = change == "admin-authority-revoked";
    var userId = administrator
      ? fixture.AdministratorUserId
      : await fixture.SeedUserWithBranchesAsync($"{change}@example.test", riyadh.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);

    if (change != "no-branch-selected")
    {
      var selected = await fixture.BranchSessions().SelectActiveBranchAsync(sessionId, riyadh.BranchId);
      Assert.True(selected.IsSuccess, selected.IsFailure ? selected.Error.Code : null);
      Assert.Equal(riyadh.BranchId, await fixture.StoredBranchAsync(sessionId));
    }

    // ---- The grounds change underneath the session.
    switch (change)
    {
      case "assignment-revoked":
        await fixture.RevokeAssignmentAsync(userId, riyadh.BranchId);
        break;
      case "branch-deactivated":
        await fixture.ForceDeactivateBranchAsync(riyadh.BranchId);
        break;
      case "admin-authority-revoked":
        await fixture.RevokeAdministratorAuthorityAsync();
        break;
      case "session-revoked":
        await fixture.RevokeSessionAsync(sessionId);
        break;
      case "session-expired":
        await fixture.ExpireSessionAsync(sessionId);
        break;
      default:
        break;
    }

    var authorized = await fixture.WriteAuthorizer(sessionId, userId)
      .AuthorizeCurrentBranchAsync(fixture.TenantA);

    Assert.True(authorized.IsFailure, $"a branch-owned write was still authorized after: {change}");
  }

  // ---- S. TWO SWITCHES ON ONE SESSION: controlled, never corrupt.
  [Fact]
  public async Task Concurrent_branch_switches_leave_the_session_on_exactly_one_authorized_branch()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var service = fixture.Service();
    var riyadh = (await service.CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var jeddah = (await service.CreateAsync(new CreateBranchRequest("JED", "Jeddah", false))).Value;

    var userId = await fixture.SeedUserWithBranchesAsync(
      "race@example.test", riyadh.BranchId, jeddah.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);

    await Task.WhenAll(
      Task.Run(() => fixture.BranchSessions().SelectActiveBranchAsync(sessionId, riyadh.BranchId)),
      Task.Run(() => fixture.BranchSessions().SelectActiveBranchAsync(sessionId, jeddah.BranchId)));

    var stored = await fixture.StoredBranchAsync(sessionId);
    Assert.True(stored == riyadh.BranchId || stored == jeddah.BranchId);
  }

  // ---- AB. NO WEB SESSION AT ALL: fail closed rather than fail to compose.
  [Fact]
  public async Task A_composition_without_a_current_session_refuses_branch_owned_writes()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    Assert.True((await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).IsSuccess);

    var authorized = await fixture.WriteAuthorizerWithoutSession().AuthorizeCurrentBranchAsync(fixture.TenantA);

    Assert.True(authorized.IsFailure);
    Assert.Equal(BranchErrors.ContextRequired.Code, authorized.Error.Code);
  }

  // ---- AA. TENANT-GLOBAL WRITES REMAIN LEGAL WITH NO BRANCH SELECTED. This is what keeps first-branch
  // onboarding reachable: creating the very first Branch is itself a tenant-global write.
  [Fact]
  public async Task Tenant_global_writes_need_no_branch_context()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var sessionId = await fixture.OpenSessionAsync(fixture.AdministratorUserId);
    Assert.Null(await fixture.StoredBranchAsync(sessionId));

    // Branch itself is tenant-global, so this succeeds with no active branch anywhere.
    var created = await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.Null(await fixture.StoredBranchAsync(sessionId));
  }

  // ---- §7. THE PER-WRITE REAUTHORIZATION COST, measured at realistic cardinality.
  [Fact]
  public async Task The_per_write_branch_authorization_seeks_its_indexes_at_realistic_cardinality()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var riyadh = (await fixture.Service().CreateAsync(new CreateBranchRequest("RUH", "Riyadh", true))).Value;
    var userId = await fixture.SeedUserWithBranchesAsync("perf@example.test", riyadh.BranchId);
    var sessionId = await fixture.OpenSessionAsync(userId);
    Assert.True((await fixture.BranchSessions().SelectActiveBranchAsync(sessionId, riyadh.BranchId)).IsSuccess);

    await fixture.SeedAccessRowsAsync(5_000);

    var session = await fixture.CapturePlanAsync(
      $"SELECT [ActiveBranchId], [Status] FROM [platform].[AuthenticationSessions] WHERE [AuthenticationSessionId] = {sessionId}");
    var authorization = await fixture.CapturePlanAsync(
      $"SELECT TOP 1 1 FROM [platform].[UserBranchAccess] WHERE [TenantId] = '{fixture.TenantA:D}' AND [TenantUserId] = {userId} AND [BranchId] = '{riyadh.BranchId:D}'");

    // Both hot-path reads must be seeks; a scan on either runs on every branch-owned write.
    Assert.NotNull(session);
    Assert.NotNull(authorization);
    Assert.DoesNotContain("Table Scan", session!.Operations, StringComparison.Ordinal);
    Assert.Contains("Seek", authorization!.Operations, StringComparison.Ordinal);
    Assert.DoesNotContain("Table Scan", authorization.Operations, StringComparison.Ordinal);
  }

  // ================================================================================================
  // §8. THE BRANCH TOPOLOGY LOCK, ACTUALLY CONTENDED (T-195).
  // ================================================================================================
  //
  // ---- ⚠ THIS LOCK WAS IN PRODUCTION WITH NO BEHAVIOURAL EVIDENCE, AND ITS REFUSAL HAD NEVER BEEN SEEN.
  //
  // `BranchTopologyLock` had one mention in a test tree: an architecture test naming the type. Worse than
  // the two locks T-190 and T-193 covered, and worse in a specific way — **`BranchErrors.TopologyBusy` is
  // produced at four sites in `src/` and was asserted NOWHERE**, so nothing had ever observed what a caller
  // who loses this race is told.
  //
  // Found by enumerating every `sp_getapplock` site rather than trusting anyone's count of them. There are
  // nine; four were already contended, including Attendance's leave-submission lock, which had the full
  // shape before any of this. **The practice was inconsistently applied, not missing** — and nothing could
  // see which sites had it.
  //
  // ---- THIS ONE IS SESSION-OWNED, SO "RELEASE" MEANS DROPPING THE CONNECTION.
  //
  // The other two are `@LockOwner = 'Transaction'`. This is `'Session'` on a dedicated connection, and the
  // type says why: *"a dead process drops its connection and the lock with it, so there is no lease to
  // expire and no stale owner to clean up."* **That sentence is a behavioural claim and the second test is
  // the first thing to check it.**
  [Fact]
  public async Task A_second_session_cannot_take_the_branch_topology_lock()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    await using var holder = await fixture.OpenPlatformConnectionAsync();
    await using var rival = await fixture.OpenPlatformConnectionAsync();

    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      holder, fixture.TenantA, TimeSpan.FromSeconds(2)));

    Assert.False(await BranchTopologyLock.TryAcquireForSessionAsync(
      rival, fixture.TenantA, TimeSpan.FromSeconds(2)));
  }

  // ⚠ THE CONTROL. A lock that had failed SHUT refuses the rival above perfectly and would pass. Only
  // showing the same acquisition SUCCEED once the holder is gone separates a working lock from a
  // permanently closed door — and a closed door here means no branch can be renamed or retired, ever.
  [Fact]
  public async Task Closing_the_holding_connection_releases_the_branch_topology_lock()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var holder = await fixture.OpenPlatformConnectionAsync();
    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      holder, fixture.TenantA, TimeSpan.FromSeconds(2)));

    // No release call anywhere: the connection simply goes away, as a killed process's would.
    await holder.DisposeAsync();

    await using var successor = await fixture.OpenPlatformConnectionAsync();
    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      successor, fixture.TenantA, TimeSpan.FromSeconds(2)));
  }

  // The resource name is per TENANT, which the type states and nothing checked. Administering one tenant's
  // branches must not stall another's.
  [Fact]
  public async Task Two_tenants_do_not_contend_for_branch_topology()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    await using var first = await fixture.OpenPlatformConnectionAsync();
    await using var second = await fixture.OpenPlatformConnectionAsync();

    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      first, fixture.TenantA, TimeSpan.FromSeconds(2)));
    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      second, fixture.TenantB, TimeSpan.FromSeconds(2)));
  }

  // ---- ⚠ AND THE REFUSAL A CALLER ACTUALLY RECEIVES, OBSERVED FOR THE FIRST TIME.
  //
  // The three above prove the primitive. This proves the PATH: `TenantBranchService.UpdateAsync` takes the
  // topology lease and answers `BranchErrors.TopologyBusy` when it cannot get it. That error is produced at
  // four sites and, until this test, was asserted at none — a reachable code nobody had ever watched
  // arrive, which is the mirror of the unreachable codes this loop keeps finding.
  [Fact]
  public async Task Branch_administration_answers_TopologyBusy_while_the_lock_is_held()
  {
    await using var fixture = await BranchFixture.CreateAsync();
    var riyadh = (await fixture.Service().CreateAsync(
      new CreateBranchRequest("RUH", "Riyadh", true))).Value;

    // A competing administrator, mid-operation, holding the tenant's topology on its own session.
    await using var competitor = await fixture.OpenPlatformConnectionAsync();
    Assert.True(await BranchTopologyLock.TryAcquireForSessionAsync(
      competitor, fixture.TenantA, TimeSpan.FromSeconds(2)));

    var renamed = await fixture.Service().UpdateAsync(new UpdateBranchRequest(
      riyadh.BranchId, "RUH", "Riyadh Central", true, riyadh.RowVersion));

    Assert.True(renamed.IsFailure);
    Assert.Equal(BranchErrors.TopologyBusy, renamed.Error);
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

    // Opens a DEDICATED platform connection. The topology lock is SESSION-owned, so each connection is an
    // independent holder and closing one releases what it held — which is the property §8 exercises and the
    // reason the type carries no lease and no cleanup.
    public async Task<SqlConnection> OpenPlatformConnectionAsync()
    {
      var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();
      return connection;
    }


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

    // ---- B1c wiring.
    public BranchSessionService BranchSessions()
    {
      var platform = PlatformContext(TenantA);
      return new BranchSessionService(
        platform,
        new TenantBranchAccessResolver(
          platform, TenantContextFactory(TenantA), new TenantAdministratorAuthority(platform)),
        new TenantAdministratorAuthority(platform),
        new TestClock());
    }

    public BranchWriteAuthorizer WriteAuthorizer(long sessionId, long tenantUserId)
    {
      var platform = PlatformContext(TenantA);
      return new BranchWriteAuthorizer(
        platform,
        new TenantBranchAccessResolver(
          platform, TenantContextFactory(TenantA), new TenantAdministratorAuthority(platform)),
        new TestClock(),
        new TestSession(TenantA, tenantUserId, sessionId));
    }

    // The non-web composition: no ICurrentAuthenticationSession at all.
    public BranchWriteAuthorizer WriteAuthorizerWithoutSession()
    {
      var platform = PlatformContext(TenantA);
      return new BranchWriteAuthorizer(
        platform,
        new TenantBranchAccessResolver(
          platform, TenantContextFactory(TenantA), new TenantAdministratorAuthority(platform)),
        new TestClock());
    }

    public async Task<long> OpenSessionAsync(long tenantUserId)
    {
      await using var platform = PlatformContext(TenantA);
      var identityId = await platform.TenantUsers
        .IgnoreQueryFilters()
        .Where(user => user.Id == tenantUserId)
        .Select(user => user.IdentityId)
        .SingleAsync();

      var now = DateTimeOffset.UtcNow;
      var session = AuthenticationSession.Create(
        identityId, tenantUserId, TenantA, "web", Guid.NewGuid(), 1,
        now, now.AddDays(30), now.AddDays(90));
      platform.Set<AuthenticationSession>().Add(session);
      await platform.SaveChangesAsync();
      return session.Id;
    }

    public async Task<Guid?> StoredBranchAsync(long sessionId)
    {
      await using var platform = PlatformContext(TenantA);
      return await platform.Set<AuthenticationSession>()
        .AsNoTracking()
        .Where(session => session.Id == sessionId)
        .Select(session => session.ActiveBranchId)
        .SingleAsync();
    }

    public async Task<long> SeedUserWithBranchesAsync(string email, params Guid[] branchIds)
    {
      var identityId = await SeedIdentityAsync();
      var created = await CreateUser().HandleAsync(
        new CreateTenantUserMembershipCommand(identityId, email, "User", [], branchIds));
      Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
      return created.Value;
    }

    public Task RevokeAssignmentAsync(long tenantUserId, Guid branchId) => ExecuteAsync(platformCatalog,
      $"DELETE FROM [platform].[UserBranchAccess] WHERE [TenantId] = '{TenantA:D}' AND [TenantUserId] = {tenantUserId} AND [BranchId] = '{branchId:D}'");

    // Direct SQL: B1a legitimately REFUSES a deactivation that would strand someone, and these cases need
    // the stranded state itself in order to prove the write boundary still refuses.
    public Task ForceDeactivateBranchAsync(Guid branchId) => ExecuteAsync(TenantCatalog,
      $"UPDATE [tenant].[Branches] SET [IsActive] = 0, [IsMainBranch] = 0 WHERE [BranchId] = '{branchId:D}'");

    public Task RevokeAdministratorAuthorityAsync() => ExecuteAsync(platformCatalog,
      $"UPDATE [platform].[RolePermissionAssignments] SET [RemovedUtc] = SYSDATETIMEOFFSET(), [RemovedBy] = 'test' WHERE [TenantId] = '{TenantA:D}'");

    // Through the DOMAIN, not raw SQL: the sessions table carries a lifecycle-metadata CHECK constraint, so
    // stamping Status alone produces a row the database rightly refuses. Revoking the way production
    // revokes is also what makes this a real test of the revocation path.
    public async Task RevokeSessionAsync(long sessionId)
    {
      await using var platform = PlatformContext(TenantA);
      var session = await platform.Set<AuthenticationSession>()
        .SingleAsync(candidate => candidate.Id == sessionId);

      var revoked = session.Revoke(
        AuthenticationSessionRevocationReason.Administrative, "test", Guid.NewGuid(), DateTimeOffset.UtcNow);
      Assert.True(revoked.IsSuccess, revoked.IsFailure ? revoked.Error.Code : null);
      await platform.SaveChangesAsync();
    }

    // The expiry CHECK relates created/idle/absolute, so the whole window moves into the past together
    // rather than idle alone being dragged behind creation.
    public Task ExpireSessionAsync(long sessionId) => ExecuteAsync(platformCatalog, $"""
      UPDATE [platform].[AuthenticationSessions]
      SET [CreatedUtc] = DATEADD(day, -40, SYSDATETIMEOFFSET()),
          [IdleExpiresUtc] = DATEADD(day, -10, SYSDATETIMEOFFSET()),
          [AbsoluteExpiresUtc] = DATEADD(day, 50, SYSDATETIMEOFFSET())
      WHERE [AuthenticationSessionId] = {sessionId}
      """);

    public async Task<MeasuredPlan?> CapturePlanAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(platformCatalog));
      await connection.OpenAsync();

      await using (var on = connection.CreateCommand())
      {
        on.CommandText = "SET SHOWPLAN_XML ON";
        await on.ExecuteNonQueryAsync();
      }

      string? xml = null;
      await using (var measured = connection.CreateCommand())
      {
        measured.CommandText = sql;
        await using var reader = await measured.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
          xml = reader.GetString(0);
        }
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
      IntegrationSqlEnvironment.BaseConnectionString;

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
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(catalog, error);
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

    // The session id matters for B1c: the write authorizer looks the durable row up by it.
    private sealed class TestSession(Guid tenantId, long tenantUserId, long sessionId = 1)
      : ICurrentAuthenticationSession
    {
      public CurrentAuthenticationSession? Value => new(
        1, tenantId, tenantUserId, sessionId, AuthenticationClientId.Create("web").Value, 1);
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
