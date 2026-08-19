using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Companies;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;
using PlatformCompany = SSAS.Platform.Domain.Companies.Company;
using PlatformTenant = SSAS.Platform.Domain.Tenants.Tenant;

namespace SSAS.Integration.Tests;

// THE COMPANY OWNERSHIP BOUNDARY AGAINST REAL SQL SERVER (FP-006C1, ADR-025).
//
// Against real SQL because every rule this slice adds is enforced by something only a real server has: a
// unique index, a cross-database read between two catalogs, and the real TenantDbContext save pipeline. An
// in-memory provider would agree with all of them and prove none.
//
// ---- WHY A TEST-ONLY PROBE ENTITY.
//
// Employee does not exist yet, and no production entity implements ICompanyOwnedEntity — that is precisely
// what this slice is building the infrastructure for. Proving the generic company-write path only when the
// first real consumer arrives is the mistake ADR-023 LOW-1 records for the branch dimension: its boundary
// shipped structurally implemented and unproven, because there was nothing to exercise it.
//
// So the probe is a REAL ICompanyOwnedEntity saved through the REAL TenantDbContext, added to the model by
// a test-only IModelCustomizer. Nothing about it reaches production: the entity, the customizer and the
// table all live in this test project and in the throwaway test catalog, and the production tenant model is
// untouched — which is why the declared tenant-owned copy inventory stays exactly ["Branch", "Company"].
[Trait("Category", "SqlServer")]
public sealed class CompanyOwnershipBoundarySqlServerTests
{
  // ---- A. AN ADDED COMPANY-OWNED ENTITY IS STAMPED WITH THE TRUSTED COMPANY, and the authorizer is
  // genuinely reached on the real save path — not merely available to be called.
  [Fact]
  public async Task An_added_company_owned_entity_is_stamped_with_the_trusted_company()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    var authorizer = CompanyFixture.CountingAuthorizer(fixture.CompanyA);
    await using var context = fixture.TenantContext(authorizer);

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "stamped" });
    await context.SaveChangesAsync();

    // The wiring: the boundary actually invoked the authorizer for this save.
    Assert.Equal(1, authorizer.Calls);

    var stored = await fixture.ProbeCompanyAsync("stamped");
    Assert.Equal(fixture.CompanyA, stored);
  }

  // ---- THE AUTHORIZER IS NOT CONSULTED WHEN NOTHING COMPANY-OWNED IS IN PLAY.
  //
  // This is what keeps company creation and all tenant-global administration possible with no company
  // selected — the same property that lets the first branch be created without a branch context.
  [Fact]
  public async Task A_tenant_global_write_needs_no_company_context()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    // Deliberately an authorizer that would REFUSE if it were asked.
    var authorizer = CompanyFixture.CountingAuthorizer(fixture.CompanyA, refuse: true);
    await using var context = fixture.TenantContext(authorizer);

    context.Companies.Add(PlatformCompany.Create(
      fixture.Tenant, CompanyCode.Create($"TG{Guid.NewGuid():N}"[..8]).Value,
      CompanyName.Create("Tenant Global").Value, BaseCurrencyCode.Create("SAR").Value,
      "company-c1-tests", Guid.NewGuid(), DateTimeOffset.UtcNow).Value);

    await context.SaveChangesAsync();

    Assert.Equal(0, authorizer.Calls);
  }

  // ---- B. A SPOOFED CompanyId ON CREATE IS REFUSED, not silently rewritten to the trusted value.
  //
  // Quietly correcting it would hide the attempt, which is the whole reason a supplied value is CONFIRMED
  // rather than trusted.
  [Fact]
  public async Task A_spoofed_company_on_create_is_refused_rather_than_rewritten()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    await using var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA));

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe
    {
      Id = Guid.NewGuid(),
      Label = "spoofed",
      CompanyId = fixture.CompanyB
    });

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Company ownership", refusal.Message, StringComparison.Ordinal);

    Assert.Null(await fixture.ProbeCompanyAsync("spoofed"));
  }

  // ---- A SUPPLIED CompanyId THAT MATCHES THE TRUSTED ONE IS ACCEPTED. Confirmation, not rejection of all
  // supplied values — otherwise a legitimate re-save of a loaded entity would be impossible.
  [Fact]
  public async Task A_supplied_company_matching_the_trusted_context_is_confirmed()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    await using var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA));

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe
    {
      Id = Guid.NewGuid(),
      Label = "confirmed",
      CompanyId = fixture.CompanyA
    });

    await context.SaveChangesAsync();

    Assert.Equal(fixture.CompanyA, await fixture.ProbeCompanyAsync("confirmed"));
  }

  // ---- C. AN ORDINARY UPDATE CANNOT MUTATE CompanyId.
  //
  // There is no sanctioned company transfer, unlike branch: a record does not move between legal entities.
  [Fact]
  public async Task An_ordinary_update_cannot_change_company_ownership()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    var probeId = await fixture.SeedProbeAsync("mutate-me", fixture.CompanyA);

    await using var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA));
    var probe = await context.Set<CompanyOwnedProbe>().SingleAsync(entity => entity.Id == probeId);

    probe.CompanyId = fixture.CompanyB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("cannot be changed after an entity is created", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(fixture.CompanyA, await fixture.ProbeCompanyAsync("mutate-me"));
  }

  // ---- D. CROSS-COMPANY UPDATE AND DELETE ARE REFUSED.
  //
  // Authorizing only inserts would let a user acting within one legal entity edit or delete another's
  // records, which is the same breach as creating one there.
  [Fact]
  public async Task A_cross_company_update_is_refused()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    var probeId = await fixture.SeedProbeAsync("owned-by-b", fixture.CompanyB);

    // Acting within company A, reaching a row owned by company B.
    await using var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA));
    var probe = await context.Set<CompanyOwnedProbe>().SingleAsync(entity => entity.Id == probeId);

    probe.Label = "owned-by-b-edited";

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("must match the trusted company context", refusal.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task A_cross_company_delete_is_refused()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    var probeId = await fixture.SeedProbeAsync("delete-me", fixture.CompanyB);

    await using var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA));
    var probe = await context.Set<CompanyOwnedProbe>().SingleAsync(entity => entity.Id == probeId);

    context.Set<CompanyOwnedProbe>().Remove(probe);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("must match the trusted company context", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(fixture.CompanyB, await fixture.ProbeCompanyAsync("delete-me"));
  }

  // ---- SAME-COMPANY UPDATE AND DELETE ARE PERMITTED. The boundary refuses cross-company writes, not all
  // writes; without this the previous two tests would pass for the wrong reason.
  [Fact]
  public async Task A_same_company_update_and_delete_are_permitted()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    var probeId = await fixture.SeedProbeAsync("mine", fixture.CompanyA);

    await using (var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA)))
    {
      var probe = await context.Set<CompanyOwnedProbe>().SingleAsync(entity => entity.Id == probeId);
      probe.Label = "mine-edited";
      await context.SaveChangesAsync();
    }

    Assert.Equal(fixture.CompanyA, await fixture.ProbeCompanyAsync("mine-edited"));

    await using (var context = fixture.TenantContext(CompanyFixture.Authorizer(fixture.CompanyA)))
    {
      var probe = await context.Set<CompanyOwnedProbe>().SingleAsync(entity => entity.Id == probeId);
      context.Set<CompanyOwnedProbe>().Remove(probe);
      await context.SaveChangesAsync();
    }

    Assert.Null(await fixture.ProbeCompanyAsync("mine-edited"));
  }

  // ---- E. COMPANY AUTHORIZATION REVOKED MID-SESSION MAKES THE NEXT WRITE FAIL.
  //
  // The assignment row is deleted after the context exists and after an earlier write succeeded. Nothing
  // about the request changes; only the authoritative state does.
  [Fact]
  public async Task Revoking_company_authorization_mid_session_refuses_the_next_write()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    await using var context = fixture.TenantContext(fixture.ProductionAuthorizer(fixture.CompanyA, fixture.NormalUserId));

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "before" });
    await context.SaveChangesAsync();

    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyA);

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "after" });

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());
    Assert.Null(await fixture.ProbeCompanyAsync("after"));
  }

  // ---- A DEACTIVATED COMPANY IS NOT ACCESS EITHER, even with the assignment row intact.
  [Fact]
  public async Task Deactivating_the_company_mid_session_refuses_the_next_write()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    await using var context = fixture.TenantContext(fixture.ProductionAuthorizer(fixture.CompanyA, fixture.NormalUserId));

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "active" });
    await context.SaveChangesAsync();

    await fixture.DeactivateCompanyAsync(fixture.CompanyA);

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "inactive" });

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());
    Assert.Null(await fixture.ProbeCompanyAsync("inactive"));
  }

  // ---- F. TENANT ADMINISTRATOR AUTHORITY REVOKED MID-SESSION REMOVES IMPLICIT COMPANY AUTHORIZATION.
  //
  // The administrator holds NO assignment rows — their scope is derived from Platform.Tenant.Administer.
  // Revoking the permission must therefore take the whole scope with it, immediately.
  [Fact]
  public async Task Revoking_administrator_authority_mid_session_removes_implicit_company_access()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    Assert.Equal(0, await fixture.CompanyAccessRowCountAsync(fixture.AdministratorUserId));

    await using var context = fixture.TenantContext(
      fixture.ProductionAuthorizer(fixture.CompanyA, fixture.AdministratorUserId));

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "admin-before" });
    await context.SaveChangesAsync();

    await fixture.RevokeAdministratorAuthorityAsync();

    context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "admin-after" });

    await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());
    Assert.Null(await fixture.ProbeCompanyAsync("admin-after"));
  }

  // ---- NO COMPANY SELECTED REFUSES A COMPANY-OWNED WRITE, and a missing authorizer refuses it too.
  // Absence is never a permit.
  [Fact]
  public async Task A_company_owned_write_without_a_company_context_is_refused()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    await using (var context = fixture.TenantContext(fixture.ProductionAuthorizer(null, fixture.NormalUserId)))
    {
      context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "none" });
      await Assert.ThrowsAsync<TenantWriteAuthorizationException>(() => context.SaveChangesAsync());
    }

    // No authorizer at all — the maintenance composition.
    await using (var context = fixture.TenantContext(companyAuthorizer: null))
    {
      context.Set<CompanyOwnedProbe>().Add(new CompanyOwnedProbe { Id = Guid.NewGuid(), Label = "no-authorizer" });
      await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    Assert.Null(await fixture.ProbeCompanyAsync("none"));
    Assert.Null(await fixture.ProbeCompanyAsync("no-authorizer"));
  }

  // ================================================================================================
  // COMPANY AUTHORIZATION (ADR-025 decision 6). The resolver, against both real catalogs.
  // ================================================================================================

  // ---- NORMAL ASSIGNMENT: a user with a live row for an Active company is authorized, and sees exactly
  // that company and no other.
  [Fact]
  public async Task A_normal_user_with_an_assignment_is_authorized_for_that_company_only()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    var authorized = await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, fixture.CompanyA);
    Assert.True(authorized.IsSuccess);

    var other = await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, fixture.CompanyB);
    Assert.True(other.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, other.Error.Code);

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, fixture.NormalUserId);
    Assert.True(permitted.IsSuccess);
    Assert.Equal([fixture.CompanyA], permitted.Value.Select(company => company.CompanyId).ToArray());
  }

  // ---- NO ASSIGNMENT: refused, and the permitted set is empty rather than everything.
  //
  // Unlike branch there is no minimum-one invariant, so empty is an ordinary answer — but it must never
  // widen into "all".
  [Fact]
  public async Task A_normal_user_without_an_assignment_is_refused_and_sees_nothing()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    var unassigned = await fixture.SeedNormalUserAsync("unassigned@example.test");

    var authorized = await resolver.AuthorizeCompanyAsync(fixture.Tenant, unassigned, fixture.CompanyA);
    Assert.True(authorized.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, authorized.Error.Code);

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, unassigned);
    Assert.True(permitted.IsSuccess);
    Assert.Empty(permitted.Value);
  }

  // ---- INACTIVE COMPANY: the assignment row survives deactivation deliberately, so reactivation restores
  // prior access — and filtering at resolution is what stops the retained row granting entry meanwhile.
  [Fact]
  public async Task An_assignment_to_an_inactive_company_is_not_access()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.NormalUserId, fixture.CompanyA)).IsSuccess);

    await fixture.DeactivateCompanyAsync(fixture.CompanyA);

    var authorized = await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, fixture.CompanyA);
    Assert.True(authorized.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, authorized.Error.Code);

    // The row is still there. Access is gone because the company is not Active, not because the row went.
    Assert.Equal(1, await fixture.CompanyAccessRowCountAsync(fixture.NormalUserId));

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, fixture.NormalUserId);
    Assert.True(permitted.IsSuccess);
    Assert.Empty(permitted.Value);
  }

  // ---- REMOVED ASSIGNMENT: previously successful, then not. Authorization is live, never remembered.
  [Fact]
  public async Task Removing_an_assignment_takes_effect_on_the_next_authorization_call()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.NormalUserId, fixture.CompanyA)).IsSuccess);

    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyA);

    var authorized = await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, fixture.CompanyA);
    Assert.True(authorized.IsFailure);
  }

  // ---- TENANT ADMINISTRATOR: every Active company, with no assignment rows at all.
  [Fact]
  public async Task A_tenant_administrator_reaches_every_active_company_without_assignment_rows()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    Assert.Equal(0, await fixture.CompanyAccessRowCountAsync(fixture.AdministratorUserId));

    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA)).IsSuccess);
    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyB)).IsSuccess);

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, fixture.AdministratorUserId);
    Assert.True(permitted.IsSuccess);
    Assert.Equal(2, permitted.Value.Count);
  }

  // ---- AND AN ADMINISTRATOR'S IMPLICIT SCOPE STILL EXCLUDES INACTIVE COMPANIES. "All companies" means all
  // ACTIVE companies, for an administrator exactly as for anyone else.
  [Fact]
  public async Task An_administrators_implicit_scope_excludes_inactive_companies()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    await fixture.DeactivateCompanyAsync(fixture.CompanyB);

    var authorized = await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyB);
    Assert.True(authorized.IsFailure);

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, fixture.AdministratorUserId);
    Assert.True(permitted.IsSuccess);
    Assert.Equal([fixture.CompanyA], permitted.Value.Select(company => company.CompanyId).ToArray());
  }

  // ---- ADMIN REVOKED: the permission is the authority, so revoking it removes the whole implicit scope.
  [Fact]
  public async Task Revoking_administrator_authority_removes_implicit_company_scope()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA)).IsSuccess);

    await fixture.RevokeAdministratorAuthorityAsync();

    var authorized = await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA);
    Assert.True(authorized.IsFailure);

    var permitted = await resolver.GetPermittedCompaniesAsync(fixture.Tenant, fixture.AdministratorUserId);
    Assert.True(permitted.IsSuccess);
    Assert.Empty(permitted.Value);
  }

  // ---- TENANT ISOLATION: a company identifier from another tenant never authorizes, even for that
  // tenant's own administrator asking within the wrong tenant.
  [Fact]
  public async Task A_company_from_another_tenant_never_authorizes()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    var foreign = await fixture.SeedCompanyInOtherTenantAsync();

    var authorized = await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.AdministratorUserId, foreign);
    Assert.True(authorized.IsFailure);
    Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, authorized.Error.Code);
  }

  // ---- NO EXISTENCE DISCLOSURE. Nonexistent, cross-tenant, inactive and unauthorized are indistinguishable
  // to the caller, so a company identifier cannot be probed for existence.
  [Fact]
  public async Task Every_company_refusal_is_indistinguishable()
  {
    await using var fixture = await CompanyFixture.CreateAsync();
    var resolver = fixture.Resolver();

    await fixture.DeactivateCompanyAsync(fixture.CompanyB);
    var foreign = await fixture.SeedCompanyInOtherTenantAsync();
    var unassigned = await fixture.SeedNormalUserAsync("probe@example.test");

    var refusals = new[]
    {
      // Does not exist.
      await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, Guid.NewGuid()),
      // Another tenant's company.
      await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, foreign),
      // Exists, this tenant, but inactive.
      await resolver.AuthorizeCompanyAsync(fixture.Tenant, fixture.NormalUserId, fixture.CompanyB),
      // Exists, active, this tenant — but this user is not authorized for it.
      await resolver.AuthorizeCompanyAsync(fixture.Tenant, unassigned, fixture.CompanyA)
    };

    Assert.All(refusals, refusal =>
    {
      Assert.True(refusal.IsFailure);
      Assert.Equal(CompanyAccessErrors.InvalidSelection.Code, refusal.Error.Code);
    });

    // And nothing in the message names a table, a database, or a company.
    Assert.All(refusals, refusal =>
    {
      Assert.DoesNotContain("Companies", refusal.Error.Message, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("tenant.", refusal.Error.Message, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("platform.", refusal.Error.Message, StringComparison.OrdinalIgnoreCase);
    });
  }

  // ---- COMPANY SCOPE IS NOT FUNCTIONAL AUTHORITY (ADR-025 decision 8).
  //
  // Platform.Tenant.Administer widens the COMPANY set and grants no operation. The resolver is the only
  // thing it feeds, and it deliberately answers nothing about permissions.
  [Fact]
  public async Task Administrator_authority_grants_company_scope_and_no_functional_permission()
  {
    await using var fixture = await CompanyFixture.CreateAsync();

    // Scope: yes.
    Assert.True((await fixture.Resolver().AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA)).IsSuccess);

    // Operations: the administrator holds exactly ONE permission, and it is the tenant-administration
    // authority itself. Nothing about company scope has added a functional grant.
    var held = await fixture.PermissionsHeldAsync(fixture.AdministratorUserId);
    Assert.Equal([PlatformPermissionNames.AdministerTenant], held);

    // And the resolver exposes no permission surface at all — company scope cannot imply an operation
    // because there is nowhere for it to say so.
    var resolverMembers = typeof(ITenantCompanyAccessResolver).GetMethods().Select(method => method.Name);
    Assert.DoesNotContain(resolverMembers, name => name.Contains("Permission", StringComparison.Ordinal));
  }

  // ================================================================================================
  // FIXTURE
  // ================================================================================================

  private sealed class CompanyFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "company-c1-tests";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private string platformCatalog = string.Empty;
    private string tenantCatalog = string.Empty;
    private string otherTenantCatalog = string.Empty;

    public Guid Tenant { get; private set; }

    public Guid OtherTenant { get; private set; }

    public Guid CompanyA { get; private set; }

    public Guid CompanyB { get; private set; }

    public long AdministratorUserId { get; private set; }

    public long NormalUserId { get; private set; }

    public static async Task<CompanyFixture> CreateAsync()
    {
      var fixture = new CompanyFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    private async Task InitializeAsync()
    {
      platformCatalog = $"SSAS_C1_Platform_{token}";
      tenantCatalog = $"SSAS_C1_Tenant_{token}";
      otherTenantCatalog = $"SSAS_C1_TenantB_{token}";

      foreach (var catalog in new[] { platformCatalog, tenantCatalog, otherTenantCatalog })
      {
        await ExecuteAsync("master", $"CREATE DATABASE [{catalog}]");
      }

      foreach (var catalog in new[] { tenantCatalog, otherTenantCatalog })
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

      // The probe table exists ONLY in the test catalog, created here rather than by a migration, because
      // it is test infrastructure and must never enter the production tenant schema.
      await ExecuteAsync(tenantCatalog, """
        CREATE TABLE [tenant].[CompanyOwnedProbe](
          [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CompanyOwnedProbe] PRIMARY KEY,
          [TenantId] UNIQUEIDENTIFIER NOT NULL,
          [CompanyId] UNIQUEIDENTIFIER NOT NULL,
          [Label] NVARCHAR(64) NOT NULL);
        """);

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      var databaseA = await RegisterAsync(platform, tenantCatalog);
      var databaseB = await RegisterAsync(platform, otherTenantCatalog);

      Tenant = await SeedTenantAsync(platform, "C1AAA", databaseA);
      OtherTenant = await SeedTenantAsync(platform, "C1BBB", databaseB);

      AdministratorUserId = await SeedUserAsync(Tenant, "admin@example.test", administrator: true);
      NormalUserId = await SeedUserAsync(Tenant, "normal@example.test", administrator: false);

      CompanyA = await SeedCompanyAsync(Tenant, tenantCatalog, "CMPA");
      CompanyB = await SeedCompanyAsync(Tenant, tenantCatalog, "CMPB");

      // The normal user is assigned to company A only, so "authorized" and "exists" stay distinguishable.
      await GrantCompanyAsync(NormalUserId, CompanyA);
    }

    // ---- THE PRODUCTION GRAPH: the real resolver over both real catalogs.
    public TenantCompanyAccessResolver Resolver()
    {
      var platform = PlatformContext(Tenant);
      return new TenantCompanyAccessResolver(
        platform, TenantContextFactory(Tenant), new TenantAdministratorAuthority(platform));
    }

    // The real write authorizer over the real resolver: what production composes.
    public CompanyWriteAuthorizer ProductionAuthorizer(Guid? selected, long tenantUserId)
    {
      var platform = PlatformContext(Tenant);
      var contextResolver = new CompanyContextResolver(
        new TestTenant(Tenant),
        new TenantCompanyAccessResolver(
          platform, TenantContextFactory(Tenant), new TenantAdministratorAuthority(platform)),
        new TestSelection(selected),
        new TestSession(Tenant, tenantUserId));

      return new CompanyWriteAuthorizer(contextResolver, new TestTenant(Tenant));
    }

    // A stable authorizer for the stamping/spoofing cases, where the point is the BOUNDARY's behaviour
    // rather than the resolver's. Still a real ICompanyWriteAuthorizer on the real save path.
    public static CountingCompanyAuthorizer Authorizer(Guid company) =>
      CountingAuthorizer(company);

    public static CountingCompanyAuthorizer CountingAuthorizer(Guid company, bool refuse = false) =>
      new(company, refuse);

    public TenantDbContext TenantContext(ICompanyWriteAuthorizer? companyAuthorizer)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        // TEST-ONLY MODEL EXTENSION. The probe is added BEFORE base.Customize runs OnModelCreating, so the
        // tenant global query filter is applied to it exactly as it is to any production tenant-owned type.
        // Replacing the service also gives this model its own cache key, so the production tenant model is
        // untouched.
        .ReplaceService<IModelCustomizer, ProbeModelCustomizer>()
        .Options;

      return new TenantDbContext(
        options, new TestUser(), new TestTenant(Tenant), new TestClock(),
        writeFence: null, branchAuthorizer: null, companyAuthorizer: companyAuthorizer);
    }

    public async Task<Guid> SeedProbeAsync(string label, Guid companyId)
    {
      var id = Guid.NewGuid();
      await ExecuteAsync(tenantCatalog, $"""
        INSERT INTO [tenant].[CompanyOwnedProbe] ([Id], [TenantId], [CompanyId], [Label])
        VALUES ('{id:D}', '{Tenant:D}', '{companyId:D}', '{label}');
        """);
      return id;
    }

    public async Task<Guid?> ProbeCompanyAsync(string label)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT [CompanyId] FROM [tenant].[CompanyOwnedProbe] WHERE [Label] = @label";
      command.Parameters.AddWithValue("@label", label);
      var result = await command.ExecuteScalarAsync();
      return result is Guid companyId ? companyId : null;
    }

    public async Task<long> SeedNormalUserAsync(string email) =>
      await SeedUserAsync(Tenant, email, administrator: false);

    public async Task<Guid> SeedCompanyInOtherTenantAsync() =>
      await SeedCompanyAsync(OtherTenant, otherTenantCatalog, "FRGN");

    public async Task GrantCompanyAsync(long tenantUserId, Guid companyId)
    {
      await using var platform = PlatformContext();
      platform.UserCompanyAccess.Add(UserCompanyAccess.Create(Tenant, tenantUserId, companyId).Value);
      await platform.SaveChangesAsync();
    }

    public async Task<int> CompanyAccessRowCountAsync(long tenantUserId)
    {
      await using var platform = PlatformContext();
      return await platform.UserCompanyAccess
        .CountAsync(access => access.TenantId == Tenant && access.TenantUserId == tenantUserId);
    }

    public Task RevokeCompanyAssignmentAsync(long tenantUserId, Guid companyId) => ExecuteAsync(
      platformCatalog,
      $"DELETE FROM [platform].[UserCompanyAccess] WHERE [TenantId] = '{Tenant:D}' AND [TenantUserId] = {tenantUserId} AND [CompanyId] = '{companyId:D}'");

    public Task RevokeAdministratorAuthorityAsync() => ExecuteAsync(
      platformCatalog,
      $"UPDATE [platform].[RolePermissionAssignments] SET [RemovedUtc] = SYSDATETIMEOFFSET(), [RemovedBy] = 'test' WHERE [TenantId] = '{Tenant:D}'");

    // Through the DOMAIN, not raw SQL: Company carries lifecycle-metadata constraints, so stamping Status
    // alone produces a row the database rightly refuses.
    public async Task DeactivateCompanyAsync(Guid companyId)
    {
      await using var context = TenantContext(companyAuthorizer: null);
      var company = await context.Companies.SingleAsync(candidate => candidate.Id == companyId);
      var deactivated = company.Deactivate(
        CompanyStatusChangeReason.Administrative, Actor, Guid.NewGuid(), DateTimeOffset.UtcNow);
      Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);
      await context.SaveChangesAsync();
    }

    public async Task<string[]> PermissionsHeldAsync(long tenantUserId)
    {
      await using var platform = PlatformContext();

      var roleIds = await platform.Set<TenantUserRoleAssignment>()
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(assignment => assignment.TenantId == Tenant &&
          assignment.TenantUserId == tenantUserId &&
          assignment.RemovedUtc == null)
        .Select(assignment => assignment.RoleId)
        .ToListAsync();

      return await platform.Set<RolePermissionAssignment>()
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Where(grant => grant.TenantId == Tenant && roleIds.Contains(grant.RoleId) && grant.RemovedUtc == null)
        .Select(grant => grant.PermissionName.Value)
        .ToArrayAsync();
    }

    private static async Task<Guid> SeedCompanyAsync(Guid tenantId, string catalog, string code)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(catalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      await using var context = new TenantDbContext(
        options, new TestUser(), new TestTenant(tenantId), new TestClock());

      var company = PlatformCompany.Create(
        tenantId, CompanyCode.Create(code).Value, CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("SAR").Value, Actor, Guid.NewGuid(), Now).Value;

      // Created Inactive by DEC-CMP-0011; the tests need Active companies, activated the ordinary way.
      Assert.True(company.Activate(
        CompanyStatusChangeReason.Administrative, Actor, Guid.NewGuid(), Now).IsSuccess);

      context.Companies.Add(company);
      await context.SaveChangesAsync();
      return company.Id;
    }

    private async Task<long> SeedUserAsync(Guid tenantId, string email, bool administrator)
    {
      await using var platform = PlatformContext(tenantId);

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

      // AUTHORITY THROUGH AN ORDINARY ROLE, exactly as production grants it.
      var role = Role.CreateCustom(
        tenantId, RoleName.Create($"Company Admins {Guid.NewGuid():N}"[..24]).Value, null, Guid.NewGuid(), Now);
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
        TenantCode.Create(code).Value, TenantName.Create($"Company {code}").Value,
        Actor, Guid.NewGuid(), Now).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(tenant.Id, databaseId, "c1", Actor, Now).Value);
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
      foreach (var catalog in new[] { tenantCatalog, otherTenantCatalog, platformCatalog })
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

    private sealed class TestSession(Guid tenantId, long tenantUserId) : ICurrentAuthenticationSession
    {
      public CurrentAuthenticationSession? Value => new(
        1, tenantId, tenantUserId, 1, AuthenticationClientId.Create("web").Value, 1);
    }
  }

  // Counts invocations so the WIRING can be proven, not just the rules: a boundary that never calls its
  // authorizer would satisfy every stamping assertion and still be broken.
  private sealed class CountingCompanyAuthorizer(Guid company, bool refuse) : ICompanyWriteAuthorizer
  {
    public int Calls { get; private set; }

    public Task<Result<Guid>> AuthorizeCurrentCompanyAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(refuse
        ? Result.Failure<Guid>(CompanyAccessErrors.InvalidSelection)
        : Result.Success(company));
    }
  }

  // The test-only company-owned entity. It is a real ICompanyOwnedEntity and a real ITenantOwnedEntity, so
  // it travels the same save pipeline any production company-owned entity will.
  private sealed class CompanyOwnedProbe : ITenantOwnedEntity, ICompanyOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string Label { get; set; } = string.Empty;
  }

  // Adds the probe to the tenant model BEFORE OnModelCreating runs, so PersistenceDbContext's
  // tenant-filter loop sees it and applies the global tenant filter exactly as it would to a production
  // tenant-owned type.
  private sealed class ProbeModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
  {
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
      ArgumentNullException.ThrowIfNull(modelBuilder);

      modelBuilder.Entity<CompanyOwnedProbe>(entity =>
      {
        entity.ToTable("CompanyOwnedProbe", TenantPersistenceConstants.Schema);
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Label).HasMaxLength(64).IsRequired();
      });

      base.Customize(modelBuilder, context);
    }
  }
}
