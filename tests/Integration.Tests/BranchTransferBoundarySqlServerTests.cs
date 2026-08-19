using SSAS.BuildingBlocks.Tenancy.Branches;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Branches;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;
using PlatformCompany = SSAS.Platform.Domain.Companies.Company;
using PlatformTenant = SSAS.Platform.Domain.Tenants.Tenant;

namespace SSAS.Integration.Tests;

// THE SANCTIONED BRANCH-TRANSFER CHANNEL AGAINST REAL SQL SERVER (FP-006C2, ADR-024 decisions 2, 3, 6, 11
// and 12).
//
// Against real SQL because the whole point is the behaviour of the real save pipeline: EF's original-value
// tracking, the live cross-catalog authorization reads, and the interaction with the tenant, company and
// branch boundaries in one `SaveChangesAsync`. An in-memory provider would agree with all of it and prove
// none of it.
//
// ---- THE PROBE CARRIES ALL THREE OWNERSHIP DIMENSIONS.
//
// Employee will be tenant-, company- and branch-owned, so the probe is too. That is what lets T14 prove a
// sanctioned BRANCH transfer grants nothing whatsoever on the COMPANY dimension — a claim that cannot be
// made with a branch-only probe. Nothing about it reaches production: the entity, the customizer and the
// table live only in this test project and in the throwaway test catalog.
[Trait("Category", "SqlServer")]
public sealed class BranchTransferBoundarySqlServerTests
{
  // ---- T1. ORDINARY MUTATION IS STILL REFUSED. The default invariant is unchanged: without a sanctioned
  // declaration a BranchId change fails exactly as it did before this slice (ADR-024 decision 2).
  [Fact]
  public async Task T1_An_ordinary_branch_mutation_is_still_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    probe.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("cannot be changed after an entity is created", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T2. AN EXACT SANCTIONED TRANSFER SUCCEEDS, and the row really moves.
  [Fact]
  public async Task T2_An_exact_sanctioned_transfer_succeeds()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await context.SaveChangesAsync();

    Assert.Equal(fixture.BranchB, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T3. A DECLARATION FOR ONE ENTITY DOES NOT AUTHORIZE ANOTHER, even one making the identical move.
  [Fact]
  public async Task T3_A_declaration_for_one_entity_does_not_authorize_another()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var declaredId = await fixture.SeedProbeAsync(fixture.BranchA);
    var otherId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var declared = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == declaredId);
    var other = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == otherId);

    // The declaration names `declared`; the mutation is applied to `other`.
    using var transfer = fixture.Declare(declared, fixture.BranchA, fixture.BranchB);
    other.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(otherId));
  }

  // ---- T4. A WRONG SOURCE IS REFUSED. The declared source must be the branch the entity is actually
  // leaving, read from EF's ORIGINAL value.
  [Fact]
  public async Task T4_A_declaration_naming_the_wrong_source_is_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    // Declares a move out of C, but the row is in A.
    using var transfer = fixture.Declare(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T5. A WRONG DESTINATION IS REFUSED. A declaration for A -> C does not authorize A -> B.
  [Fact]
  public async Task T5_A_declaration_naming_the_wrong_destination_is_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchC);
    probe.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T6. NO LEAKAGE PAST THE SCOPE. A completed transfer does not authorize the next one.
  [Fact]
  public async Task T6_A_disposed_declaration_does_not_authorize_a_later_save()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var firstId = await fixture.SeedProbeAsync(fixture.BranchA);
    var secondId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);

    var first = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == firstId);
    using (fixture.Declare(first, fixture.BranchA, fixture.BranchB))
    {
      first.BranchId = fixture.BranchB;
      await context.SaveChangesAsync();
    }

    Assert.Equal(fixture.BranchB, await fixture.ProbeBranchAsync(firstId));

    // The scope is closed. The identical move on another entity is now an ordinary mutation.
    var second = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == secondId);
    second.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(secondId));
  }

  // ---- T7. ONE DECLARATION DOES NOT WIDEN THE WHOLE SAVE. A sanctioned entity and an unsanctioned one in
  // the same unit of work: the unsanctioned mutation is still refused, and nothing commits.
  [Fact]
  public async Task T7_A_second_unsanctioned_entity_in_the_same_save_is_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var sanctionedId = await fixture.SeedProbeAsync(fixture.BranchA);
    var stowawayId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var sanctioned = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == sanctionedId);
    var stowaway = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == stowawayId);

    using var transfer = fixture.Declare(sanctioned, fixture.BranchA, fixture.BranchB);

    // Both move A -> B. Only one of them is declared.
    sanctioned.BranchId = fixture.BranchB;
    stowaway.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    // The save was refused as a whole, so neither row moved.
    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(sanctionedId));
    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(stowawayId));
  }

  // ---- T8. SOURCE AUTHORIZATION IS RE-ASKED AT SAVE TIME. Opening the scope earlier proves nothing: the
  // execution branch is revalidated by the existing branch write authorizer on every save.
  [Fact]
  public async Task T8_Revoking_source_branch_access_before_the_save_refuses_the_transfer()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.NormalUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    // The declaration is already open and the mutation already applied. Only the authoritative state changes.
    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T9. THE DECLARATION IS NOT DESTINATION AUTHORIZATION. Naming a destination authorizes nothing; the
  // channel re-asks ITenantBranchAccessResolver, and a destination the caller cannot reach refuses.
  [Fact]
  public async Task T9_A_declaration_does_not_authorize_an_unreachable_destination()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    // This user is assigned to A and B, but never to C.
    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.NormalUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchC);
    probe.BranchId = fixture.BranchC;

    var refusal = await Assert.ThrowsAsync<TenantWriteAuthorizationException>(
      () => context.SaveChangesAsync());
    Assert.Equal(BranchErrors.InvalidSelection.Code, refusal.Error.Code);

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- AND REVOKING DESTINATION ACCESS BEFORE THE SAVE REFUSES TOO, for the same reason.
  [Fact]
  public async Task T9b_Revoking_destination_access_before_the_save_refuses_the_transfer()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.NormalUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchB);

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T10. A NORMAL USER CANNOT RECOVER OUT OF AN INACTIVE SOURCE. The recovery is administrator-only.
  [Fact]
  public async Task T10_A_normal_user_cannot_transfer_out_of_an_inactive_source_branch()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.NormalUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<TenantWriteAuthorizationException>(
      () => context.SaveChangesAsync());
    Assert.Equal(BranchTransferErrors.TransferNotPermitted.Code, refusal.Error.Code);

    Assert.Equal(fixture.BranchC, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T11. THE NARROW RECOVERY SUCCEEDS for a tenant administrator, out of an inactive source into an
  // active destination (ADR-024 decision 12).
  [Fact]
  public async Task T11_A_tenant_administrator_can_transfer_out_of_an_inactive_source_branch()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await context.SaveChangesAsync();

    Assert.Equal(fixture.BranchB, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- THE RECOVERY IS NOT A GENERAL KEY TO THE INACTIVE BRANCH. An administrator may move an entity OUT;
  // they may not perform an ordinary update on a row still sitting in the inactive branch.
  [Fact]
  public async Task T11b_The_recovery_grants_no_ordinary_write_authority_over_the_inactive_branch()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    // No declaration: an ordinary edit of a row in the inactive branch.
    probe.Label = "edited-in-place";

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
  }

  // ---- AND RECOVERY MODE CANNOT BE USED TO REACH AN ACTIVE BRANCH THE CALLER IS NOT IN. Without the
  // inactive-source check an administrator could use recovery to edit an active branch's rows from anywhere,
  // which widens their reach instead of restoring it.
  [Fact]
  public async Task T11c_Recovery_mode_is_refused_when_the_source_branch_is_still_active()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);

    // BranchC is deliberately left ACTIVE.
    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<TenantWriteAuthorizationException>(
      () => context.SaveChangesAsync());
    Assert.Equal(BranchTransferErrors.TransferNotPermitted.Code, refusal.Error.Code);

    Assert.Equal(fixture.BranchC, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T12. ADMINISTRATOR AUTHORITY IS RE-ASKED AT SAVE TIME, exactly like every other input.
  [Fact]
  public async Task T12_Revoking_administrator_authority_before_the_save_refuses_the_recovery()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await fixture.RevokeAdministratorAuthorityAsync();

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchC, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T12b. A REVOKED SESSION REFUSES THE RECOVERY TOO.
  //
  // The recovery relaxes exactly one thing — which branch the entity may be leaving. It does not relax who
  // the caller is or whether their session is still usable, so the durable session is re-read on this path
  // exactly as on every other branch-owned write (BR-PLT-0014, ADR-023 decision 10).
  [Fact]
  public async Task T12b_A_revoked_session_refuses_the_inactive_source_recovery()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(
      fixture.BranchA, asUserId: fixture.AdministratorUserId, out var sessionId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await fixture.RevokeSessionAsync(sessionId);

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchC, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- AND A REVOKED SESSION REFUSES AN ORDINARY TRANSFER, which is the same guard on the ordinary path.
  [Fact]
  public async Task T12c_A_revoked_session_refuses_an_ordinary_transfer()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA, out var sessionId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await fixture.RevokeSessionAsync(sessionId);

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- A RECOVERY STILL REQUIRES A VALID BRANCH CONTEXT. The exception is about which branch the entity
  // may LEAVE, not about the caller being allowed to work from nowhere.
  [Fact]
  public async Task T12d_A_recovery_still_requires_a_usable_execution_branch_context()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchC);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    // The administrator's session has selected no branch at all.
    await using var context = fixture.TenantContext(
      activeBranch: null, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.DeclareRecovery(probe, fixture.BranchC, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<TenantWriteAuthorizationException>(
      () => context.SaveChangesAsync());
    Assert.Equal(BranchErrors.SelectionRequired.Code, refusal.Error.Code);

    Assert.Equal(fixture.BranchC, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T13. NEVER INTO AN INACTIVE DESTINATION. The resolver intersects with active branches, so a
  // deactivated destination is unreachable however the transfer is declared.
  [Fact]
  public async Task T13_A_transfer_into_an_inactive_destination_is_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);
    await fixture.DeactivateBranchAsync(fixture.BranchC);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchC);
    probe.BranchId = fixture.BranchC;

    var refusal = await Assert.ThrowsAsync<TenantWriteAuthorizationException>(
      () => context.SaveChangesAsync());
    Assert.Equal(BranchErrors.InvalidSelection.Code, refusal.Error.Code);

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- AND A DESTINATION DEACTIVATED AFTER THE DECLARATION IS OPENED IS REFUSED TOO, because the
  // destination is re-asked at save time rather than captured when the scope opened.
  [Fact]
  public async Task T13b_A_destination_deactivated_after_the_declaration_is_refused()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await fixture.DeactivateBranchAsync(fixture.BranchB);

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- T14. THE COMPANY BOUNDARY IS UNTOUCHED BY A BRANCH TRANSFER.
  //
  // A sanctioned branch transfer authorizes a BRANCH change and nothing else. The company dimension is
  // independent (ADR-023, ADR-025), so CompanyId stays immutable and the company write boundary still runs.
  [Fact]
  public async Task T14_A_sanctioned_branch_transfer_does_not_permit_a_company_change()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);

    // A legitimate branch transfer, plus an attempt to ride the company dimension along with it.
    probe.BranchId = fixture.BranchB;
    probe.CompanyId = fixture.CompanyB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Company ownership cannot be changed", refusal.Message, StringComparison.Ordinal);

    // Neither dimension moved.
    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
    Assert.Equal(fixture.CompanyA, await fixture.ProbeCompanyAsync(probeId));
  }

  // ---- AND THE COMPANY BOUNDARY STILL REFUSES A TRANSFER WHOSE COMPANY CONTEXT IS NOT AUTHORIZED. The
  // transfer channel does not bypass the company boundary; both must pass.
  [Fact]
  public async Task T14b_A_sanctioned_branch_transfer_still_requires_company_authorization()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    // The context is acting within company B, while the row belongs to company A.
    await using var context = fixture.TenantContext(fixture.BranchA, company: fixture.CompanyB);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("must match the trusted company context", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- A CONTEXT WITH NO TRANSFER AUTHORIZER AT ALL KEEPS THE ORIGINAL INVARIANT IN FULL. Absence is
  // never a permit: the maintenance composition can no more transfer than it could before this slice.
  [Fact]
  public async Task Without_a_transfer_authorizer_no_branch_mutation_is_ever_permitted()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA, withTransferAuthorizer: false);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    // Even with a declaration open in the scope, a context that was not given the authorizer cannot see it.
    using var transfer = fixture.Declare(probe, fixture.BranchA, fixture.BranchB);
    probe.BranchId = fixture.BranchB;

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ---- ORDINARY SAME-BRANCH WORK IS COMPLETELY UNAFFECTED, so the tests above cannot be passing because
  // the boundary refuses everything.
  [Fact]
  public async Task An_ordinary_same_branch_update_still_succeeds()
  {
    await using var fixture = await TransferFixture.CreateAsync();
    var probeId = await fixture.SeedProbeAsync(fixture.BranchA);

    await using var context = fixture.TenantContext(fixture.BranchA);
    var probe = await context.Set<TransferProbe>().SingleAsync(entity => entity.Id == probeId);

    probe.Label = "ordinary-edit";
    await context.SaveChangesAsync();

    Assert.Equal(fixture.BranchA, await fixture.ProbeBranchAsync(probeId));
  }

  // ================================================================================================
  // FIXTURE
  // ================================================================================================

  private sealed class TransferFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "branch-transfer-c2-tests";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private readonly BranchTransferScope transferScope = new();
    private string platformCatalog = string.Empty;
    private string tenantCatalog = string.Empty;

    public Guid Tenant { get; private set; }

    public Guid CompanyA { get; private set; }

    public Guid CompanyB { get; private set; }

    public Guid BranchA { get; private set; }

    public Guid BranchB { get; private set; }

    public Guid BranchC { get; private set; }

    public long AdministratorUserId { get; private set; }

    public long NormalUserId { get; private set; }

    public static async Task<TransferFixture> CreateAsync()
    {
      var fixture = new TransferFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    private async Task InitializeAsync()
    {
      platformCatalog = $"SSAS_C2_Platform_{token}";
      tenantCatalog = $"SSAS_C2_Tenant_{token}";

      foreach (var catalog in new[] { platformCatalog, tenantCatalog })
      {
        await ExecuteAsync("master", $"CREATE DATABASE [{catalog}]");
      }

      await using (var connection = new SqlConnection(ConnectionFor(tenantCatalog)))
      {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
          .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
            TenantPersistenceConstants.MigrationHistoryTable,
            TenantPersistenceConstants.MigrationHistorySchema))
          .Options;
        await using var context = new TenantDbContext(
          options, new TestUser(), new TestTenant(null), new TestClock());
        await context.Database.MigrateAsync();
      }

      // The probe table exists ONLY in the test catalog, created here rather than by a migration, because
      // it is test infrastructure and must never enter the production tenant schema.
      await ExecuteAsync(tenantCatalog, """
        CREATE TABLE [tenant].[TransferProbe](
          [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_TransferProbe] PRIMARY KEY,
          [TenantId] UNIQUEIDENTIFIER NOT NULL,
          [CompanyId] UNIQUEIDENTIFIER NOT NULL,
          [BranchId] UNIQUEIDENTIFIER NOT NULL,
          [Label] NVARCHAR(64) NOT NULL);
        """);

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      var databaseId = await RegisterAsync(platform, tenantCatalog);
      Tenant = await SeedTenantAsync(platform, "C2AAA", databaseId);

      AdministratorUserId = await SeedUserAsync("admin@example.test", administrator: true);
      NormalUserId = await SeedUserAsync("normal@example.test", administrator: false);

      CompanyA = await SeedCompanyAsync("CMPA");
      CompanyB = await SeedCompanyAsync("CMPB");

      BranchA = await SeedBranchAsync("BRA", main: true);
      BranchB = await SeedBranchAsync("BRB", main: false);
      BranchC = await SeedBranchAsync("BRC", main: false);

      // The normal user reaches A and B, never C — so "authorized" and "exists" stay distinguishable.
      await GrantBranchAsync(NormalUserId, BranchA);
      await GrantBranchAsync(NormalUserId, BranchB);
      await GrantCompanyAsync(NormalUserId, CompanyA);
      await GrantCompanyAsync(NormalUserId, CompanyB);
    }

    // Opens a sanctioned declaration the way a command handler would, after its own authorization.
    public IDisposable Declare(TransferProbe probe, Guid source, Guid destination) =>
      Open(probe, source, destination, BranchTransferMode.CurrentBranch);

    public IDisposable DeclareRecovery(TransferProbe probe, Guid source, Guid destination) =>
      Open(probe, source, destination, BranchTransferMode.InactiveSourceRecovery);

    private IDisposable Open(TransferProbe probe, Guid source, Guid destination, BranchTransferMode mode)
    {
      var declaration = BranchTransferDeclaration.Create(probe, source, destination, mode);
      Assert.True(declaration.IsSuccess, declaration.IsFailure ? declaration.Error.Code : null);

      var begun = transferScope.Begin(declaration.Value);
      Assert.True(begun.IsSuccess, begun.IsFailure ? begun.Error.Code : null);
      return begun.Value;
    }

    // The production graph, wired against the real databases: the real branch write authorizer, the real
    // company write authorizer and the real transfer authorizer over the real resolvers.
    public TenantDbContext TenantContext(
      Guid? activeBranch,
      long? asUserId = null,
      Guid? company = null,
      bool withTransferAuthorizer = true) =>
      TenantContext(activeBranch, out _, asUserId, company, withTransferAuthorizer);

    public TenantDbContext TenantContext(Guid? activeBranch, out long sessionId) =>
      TenantContext(activeBranch, out sessionId, null, null, true);

    public TenantDbContext TenantContext(Guid? activeBranch, long? asUserId, out long sessionId) =>
      TenantContext(activeBranch, out sessionId, asUserId, null, true);

    private TenantDbContext TenantContext(
      Guid? activeBranch,
      out long sessionId,
      long? asUserId,
      Guid? company,
      bool withTransferAuthorizer)
    {
      var tenantUserId = asUserId ?? AdministratorUserId;
      sessionId = SessionFor(tenantUserId, activeBranch).GetAwaiter().GetResult();

      var platform = PlatformContext(Tenant);
      var accessResolver = new TenantBranchAccessResolver(
        platform, ReadContextFactory(), new TenantAdministratorAuthority(platform));
      var session = new TestSession(Tenant, tenantUserId, sessionId);

      var branchAuthorizer = new BranchWriteAuthorizer(
        platform, accessResolver, new TestClock(), session);

      var companyPlatform = PlatformContext(Tenant);
      var companyAuthorizer = new SSAS.Platform.Infrastructure.Companies.CompanyWriteAuthorizer(
        new SSAS.Platform.Infrastructure.Companies.CompanyContextResolver(
          new TestTenant(Tenant),
          new SSAS.Platform.Infrastructure.Companies.TenantCompanyAccessResolver(
            companyPlatform, ReadContextFactory(), new TenantAdministratorAuthority(companyPlatform)),
          new TestSelection(company ?? CompanyA),
          session),
        new TestTenant(Tenant));

      IBranchTransferAuthorizer? transferAuthorizer = withTransferAuthorizer
        ? new BranchTransferAuthorizer(
            transferScope,
            new TenantBranchAccessResolver(
              PlatformContext(Tenant), ReadContextFactory(), new TenantAdministratorAuthority(platform)),
            new TenantAdministratorAuthority(PlatformContext(Tenant)),
            ReadContextFactory(),
            session)
        : null;

      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        // TEST-ONLY MODEL EXTENSION. The probe is added BEFORE base.Customize runs OnModelCreating, so the
        // tenant global query filter is applied to it exactly as it is to any production tenant-owned type.
        .ReplaceService<IModelCustomizer, ProbeModelCustomizer>()
        .Options;

      return new TenantDbContext(
        options, new TestUser(), new TestTenant(Tenant), new TestClock(),
        writeFence: null,
        branchAuthorizer: branchAuthorizer,
        companyAuthorizer: companyAuthorizer,
        branchTransferAuthorizer: transferAuthorizer);
    }

    public async Task<Guid> SeedProbeAsync(Guid branchId)
    {
      var id = Guid.NewGuid();
      await ExecuteAsync(tenantCatalog, $"""
        INSERT INTO [tenant].[TransferProbe] ([Id], [TenantId], [CompanyId], [BranchId], [Label])
        VALUES ('{id:D}', '{Tenant:D}', '{CompanyA:D}', '{branchId:D}', 'probe');
        """);
      return id;
    }

    public async Task<Guid?> ProbeBranchAsync(Guid id) => await ScalarGuidAsync("BranchId", id);

    public async Task<Guid?> ProbeCompanyAsync(Guid id) => await ScalarGuidAsync("CompanyId", id);

    private async Task<Guid?> ScalarGuidAsync(string column, Guid id)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"SELECT [{column}] FROM [tenant].[TransferProbe] WHERE [Id] = @id";
      command.Parameters.AddWithValue("@id", id);
      var result = await command.ExecuteScalarAsync();
      return result is Guid value ? value : null;
    }

    public Task RevokeBranchAssignmentAsync(long tenantUserId, Guid branchId) => ExecuteAsync(
      platformCatalog,
      $"DELETE FROM [platform].[UserBranchAccess] WHERE [TenantId] = '{Tenant:D}' AND [TenantUserId] = {tenantUserId} AND [BranchId] = '{branchId:D}'");

    public Task RevokeAdministratorAuthorityAsync() => ExecuteAsync(
      platformCatalog,
      $"UPDATE [platform].[RolePermissionAssignments] SET [RemovedUtc] = SYSDATETIMEOFFSET(), [RemovedBy] = 'test' WHERE [TenantId] = '{Tenant:D}'");

    // Direct SQL: branch deactivation legitimately refuses when it would strand a user or retire the main
    // branch, and these cases need the deactivated state itself in order to prove the boundary's behaviour.
    public Task DeactivateBranchAsync(Guid branchId) => ExecuteAsync(
      tenantCatalog,
      $"UPDATE [tenant].[Branches] SET [IsActive] = 0, [IsMainBranch] = 0 WHERE [BranchId] = '{branchId:D}'");

    private async Task<long> SessionFor(long tenantUserId, Guid? activeBranch)
    {
      await using var platform = PlatformContext(Tenant);

      var identityId = await platform.TenantUsers
        .IgnoreQueryFilters()
        .Where(user => user.Id == tenantUserId)
        .Select(user => user.IdentityId)
        .SingleAsync();

      var now = DateTimeOffset.UtcNow;
      var session = SSAS.Platform.Domain.Authentication.AuthenticationSession.Create(
        identityId, tenantUserId, Tenant, "web", Guid.NewGuid(), 1,
        now, now.AddDays(30), now.AddDays(90));
      platform.Set<SSAS.Platform.Domain.Authentication.AuthenticationSession>().Add(session);
      await platform.SaveChangesAsync();

      // A null active branch is the "authenticated but has not selected a branch yet" state, which every
      // branch-owned write must refuse.
      if (activeBranch is { } branchId)
      {
        session.SelectBranch(branchId);
        await platform.SaveChangesAsync();
      }

      return session.Id;
    }

    // Through the DOMAIN, not raw SQL: the sessions table carries a lifecycle-metadata CHECK constraint, so
    // stamping Status alone produces a row the database rightly refuses. Revoking the way production revokes
    // is also what makes this a real test of the revocation path.
    public async Task RevokeSessionAsync(long sessionId)
    {
      await using var platform = PlatformContext(Tenant);
      var session = await platform.Set<SSAS.Platform.Domain.Authentication.AuthenticationSession>()
        .SingleAsync(candidate => candidate.Id == sessionId);

      var revoked = session.Revoke(
        AuthenticationSessionRevocationReason.Administrative, "test", Guid.NewGuid(), DateTimeOffset.UtcNow);
      Assert.True(revoked.IsSuccess, revoked.IsFailure ? revoked.Error.Code : null);
      await platform.SaveChangesAsync();
    }

    private async Task<Guid> SeedBranchAsync(string code, bool main)
    {
      await using var context = TenantOnlyContext();
      var branch = Branch.Create(
        Tenant, BranchCode.Create(code).Value, BranchName.Create($"Branch {code}").Value,
        main, Actor).Value;
      context.Branches.Add(branch);
      await context.SaveChangesAsync();
      return branch.Id;
    }

    private async Task<Guid> SeedCompanyAsync(string code)
    {
      await using var context = TenantOnlyContext();
      var company = PlatformCompany.Create(
        Tenant, CompanyCode.Create(code).Value, CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("SAR").Value, Actor, Guid.NewGuid(), Now).Value;
      Assert.True(company.Activate(
        CompanyStatusChangeReason.Administrative, Actor, Guid.NewGuid(), Now).IsSuccess);
      context.Companies.Add(company);
      await context.SaveChangesAsync();
      return company.Id;
    }

    // A plain tenant context with no branch, company or transfer authorizer: seeding tenant-global rows.
    private TenantDbContext TenantOnlyContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(options, new TestUser(), new TestTenant(Tenant), new TestClock());
    }

    public async Task GrantBranchAsync(long tenantUserId, Guid branchId)
    {
      await using var platform = PlatformContext();
      platform.UserBranchAccess.Add(
        SSAS.Platform.Domain.Branches.UserBranchAccess.Create(Tenant, tenantUserId, branchId).Value);
      await platform.SaveChangesAsync();
    }

    public async Task GrantCompanyAsync(long tenantUserId, Guid companyId)
    {
      await using var platform = PlatformContext();
      platform.UserCompanyAccess.Add(
        SSAS.Platform.Domain.Companies.UserCompanyAccess.Create(Tenant, tenantUserId, companyId).Value);
      await platform.SaveChangesAsync();
    }

    private async Task<long> SeedUserAsync(string email, bool administrator)
    {
      await using var platform = PlatformContext(Tenant);

      var identity = Identity.Create(AuthenticationSubject.Create($"sub-{Guid.NewGuid():N}").Value);
      platform.Identities.Add(identity);
      await platform.SaveChangesAsync();

      var user = TenantUser.CreateActive(
        identity.Id, Tenant, EmailAddress.Create(email).Value,
        UserDisplayName.Create("Test User").Value, Guid.NewGuid(), Now);
      platform.TenantUsers.Add(user);
      await platform.SaveChangesAsync();

      if (!administrator)
      {
        return user.Id;
      }

      var role = Role.CreateCustom(
        Tenant, RoleName.Create($"Transfer Admins {Guid.NewGuid():N}"[..24]).Value, null, Guid.NewGuid(), Now);
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
      var tenant = PlatformTenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Transfer {code}").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(tenant.Id, databaseId, "c2", Actor, Now).Value);
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

    private TenantDbContextFactory ReadContextFactory()
    {
      var platform = PlatformContext(Tenant);
      return new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(Tenant), new TestClock(),
        new TenantCutoverWriteFence(
          new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5)),
          Options.Create(freeze)));
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
      foreach (var catalog in new[] { tenantCatalog, platformCatalog })
      {
        try
        {
          await ExecuteAsync("master",
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]");
        }
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(catalog, error);
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

    private sealed class TestSelection(Guid? companyId) : ICompanySelection
    {
      public Result<Guid?> Requested => Result.Success(companyId);
    }

    private sealed class TestSession(Guid tenantId, long tenantUserId, long sessionId)
      : ICurrentAuthenticationSession
    {
      public CurrentAuthenticationSession? Value => new(
        1, tenantId, tenantUserId, sessionId, AuthenticationClientId.Create("web").Value, 1);
    }
  }

  // The test-only entity. It carries all three ownership dimensions, exactly as Employee will, so the
  // branch-transfer channel can be proven not to affect the company one.
  private sealed class TransferProbe : ITenantOwnedEntity, ICompanyOwnedEntity, IBranchOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public string Label { get; set; } = string.Empty;
  }

  private sealed class ProbeModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
  {
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
      ArgumentNullException.ThrowIfNull(modelBuilder);

      modelBuilder.Entity<TransferProbe>(entity =>
      {
        entity.ToTable("TransferProbe", TenantPersistenceConstants.Schema);
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Label).HasMaxLength(64).IsRequired();
      });

      base.Customize(modelBuilder, context);
    }
  }
}
