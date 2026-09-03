using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Architecture.Tests;

// Durable platform-plane authorization invariants (ADR-015, DEC-TEN-0018).
// Behavior rules over the code-owned permission catalog and the tenant-token claim filter.
public sealed class PlatformPlaneAuthorizationArchitectureTests
{
  private const string PlatformTenantPrefix = "Platform.Tenants.";

  [Fact]
  public void Every_platform_tenant_permission_is_platform_support_scoped()
  {
    var catalog = new PlatformPermissionCatalog();

    var platformTenantPermissions = catalog.All
      .Where(permission => permission.Name.Value.StartsWith(PlatformTenantPrefix, StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(platformTenantPermissions);
    Assert.All(platformTenantPermissions, permission => Assert.Equal(PermissionScope.PlatformSupport, permission.Scope));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0088")]
  // `AC-TEN-0088` — *"NO `Platform.Support.View` (or equivalent) permission is introduced in Phase 4;
  // authority reads reuse `Platform.Support.Administer`."* ⚠ **THE ABSENCE IS A CONSEQUENCE OF THE EXACT SET
  // RATHER THAN A BAN ON A NAME**: the assertion below pins the PlatformSupport family at exactly four
  // permissions, so `Platform.Support.View` — and any other read-shaped sibling nobody has thought of —
  // cannot appear without reddening. **A name ban would have caught only the spelling the criterion
  // happened to guess.** Same idiom as the projection member pin: foreclose the class, not the instance.
  //
  // ⚠ THE CRITERION'S SECOND CLAUSE IS NOT HERE. *"Authority reads REUSE `Platform.Support.Administer`"* is
  // a claim about which permission the read ENDPOINTS require — behavioural, on the route — and its site is
  // `AC-TEN-0087`'s territory rather than the catalog.
  public void The_platform_support_family_is_exactly_the_approved_permissions()
  {
    // PlatformSupport scope = the Platform.Tenants.* tenant-admin family (ADR-015) plus the
    // Platform.Support.Administer authority-administration permission (ADR-016). Nothing else.
    var catalog = new PlatformPermissionCatalog();

    Assert.Equal(
      ["Platform.Support.Administer", "Platform.Tenants.Lifecycle", "Platform.Tenants.Manage", "Platform.Tenants.View"],
      catalog.All.Where(permission => permission.Scope == PermissionScope.PlatformSupport)
        .Select(permission => permission.Name.Value)
        .OrderBy(value => value, StringComparer.Ordinal));
  }

  [Fact]
  public void Every_non_platform_support_permission_stays_tenant_scoped()
  {
    // The tenant plane (Company, Localization, IAM) is unchanged: everything outside the
    // PlatformSupport-scoped family remains PermissionScope.Tenant.
    var catalog = new PlatformPermissionCatalog();

    Assert.All(
      catalog.All.Where(permission => permission.Scope != PermissionScope.PlatformSupport),
      permission => Assert.Equal(PermissionScope.Tenant, permission.Scope));
  }

  // ⚠ CITES `AC-IAM-0004` — *"A tenant role, INCLUDING A ROLE NAMED ADMINISTRATOR, does not grant
  // platform-support access."* This is the mechanism that makes it true: every catalog name is offered to
  // the tenant-token filter and only Tenant-scoped ones survive, so a tenant role naming a PlatformSupport
  // permission grants nothing at issuance.
  //
  // ⚠⚠ THE *"including a role named Administrator"* CLAUSE IS **NOT** CARRIED HERE, AND MY FIRST VERSION OF
  // THIS COMMENT CLAIMED IT WAS — *"satisfied by construction, the filter keys on scope and never on a
  // name."* **THAT WAS TRUE-BY-CONSTRUCTION REASONING AND IT IS NOT AN OBSERVATION.** It described today's
  // implementation; the clause exists because of tomorrow's.
  //
  // ⚠⚠⚠ AND THE CHECK THAT KILLED IT IS WORTH KEEPING: **NO ROLE FLOWS THROUGH THIS TEST AT ALL, NAMED OR
  // OTHERWISE.** `FilterToTenantScope` takes PERMISSION NAMES, so there was never an Administrator case here
  // to be subsumed — the question *does a role named Administrator reach this filter* has the answer
  // *no role reaches this filter*. **A clause cannot be satisfied by construction in a test whose subject is
  // one layer below the clause's subject.**
  //
  // The clause IS observed, elsewhere and directly: `IdentityAccessDomainTests.Administrator_role_name_
  // does_not_imply_permissions` builds a custom role literally named `"Administrator"` and asserts
  // `ActivePermissions` is empty. **That is the case this file cannot contain.**
  //
  // So what THIS test carries is the SCOPE mechanism of `AC-IAM-0004` — a tenant token cannot carry a
  // PlatformSupport permission however it was granted — and not the name clause.
  //
  // ⚠⚠⚠ AND THE ANTI-VACUITY CONTROL IS ALREADY IN THE TEST, WHICH IS WHY THE CITATION IS SAFE. A filter
  // that returned NOTHING would satisfy *no PlatformSupport permission survives* perfectly. The second
  // assertion pins `filtered.Count` to the exact number of Tenant-scoped permissions in the catalog, so the
  // universal is over a set proved non-empty and proved complete. `The_platform_support_family_is_exactly_
  // the_approved_permissions` closes the other side by exact set equality, so *PlatformSupport* is a fixed
  // population rather than whatever happens to be scoped that way today.
  //
  // NOT cited: `AC-IAM-0003` (*platform support CAN access an authorized tenant, and the action is
  // audited*). That is the permissive direction plus an audit obligation, and nothing here grants access or
  // observes an audit record — this file only proves the tenant plane cannot reach the platform one.
  [Fact]
  [Trait("Criterion", "AC-IAM-0004")]
  public void Tenant_token_claim_filter_removes_every_platform_support_permission()
  {
    // Claim issuance must scope-filter: no PlatformSupport catalog permission can survive into a tenant token.
    var catalog = new PlatformPermissionCatalog();
    var allNames = catalog.All.Select(permission => permission.Name.Value).ToArray();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(allNames, catalog);

    // Every surviving name resolves to Tenant scope; no PlatformSupport permission (Tenants.* or Support.*) survives.
    Assert.All(
      filtered,
      name => Assert.True(catalog.TryGet(name, out var permission) && permission.Scope == PermissionScope.Tenant));
    Assert.Equal(
      catalog.All.Count(permission => permission.Scope == PermissionScope.Tenant),
      filtered.Count);
  }

  // ---- Phase 3C-1 platform token profile (ADR-015 / DEC-TEN-0022) ----

  [Fact]
  public void Security_plane_claim_name_and_platform_value_are_exact()
  {
    Assert.Equal("security_plane", JwtClaimTypes.SecurityPlane);
    Assert.Equal("platform", SecurityPlane.Platform);
    Assert.Equal("tenant", SecurityPlane.Tenant);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0074")]
  // ⚠ `AC-TEN-0074` CITED FOR ITS BAN LIST, WHICH IS FIVE OF ITS SIX BANS, AND NOT FOR ITS POSITIVE HALF.
  //
  // *"A platform access token FORBIDS `tenant_id`, `tenant_user_id`, `role`, `company_id`, any
  // principal-status claim, and ANY BOOTSTRAP/CONFIG CLAIM; ‖ it CARRIES `security_plane=platform` exactly
  // once plus `identity_id`, `session_id`, `client_id`, `security_version`, and one or more active
  // catalog-valid PlatformSupport permission claims."*
  //
  //   five bans        CARRIED — `TenantId`, `TenantUserId`, `CompanyId`, `Role`/`Roles`, `Status`.
  //   bootstrap/config NOT CARRIED — no bootstrap- or configuration-shaped name is banned below, and the
  //                    criterion names that class explicitly.
  //   the positive     ELSEWHERE — `PlatformAccessTokenClaimsTests` asserts the members are present and
  //                    populated; this test is a shape ban and cannot see what a claims instance carries.
  //
  // ⚠⚠ AND ONE APPARENT CONTRADICTION THAT IS NOT ONE, WORTH WRITING DOWN BEFORE SOMEONE "FIXES" IT: the ban
  // below includes `SecurityPlane` while the criterion REQUIRES `security_plane=platform` on the token.
  // **The plane is stamped by the ISSUER, not carried on the claims record**, so banning the property here
  // and requiring the claim there are the same design. A reader comparing the two lines without that fact
  // would conclude the test contradicts the criterion.
  public void Platform_access_token_claims_carry_no_tenant_or_role_shaped_fields()
  {
    // The platform profile is structurally non-tenant and permission-based (no roles/status/plane field).
    var properties = typeof(PlatformAccessTokenClaims).GetProperties().Select(property => property.Name).ToArray();

    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId", "Roles", "Role", "Status", "SecurityPlane" })
    {
      Assert.DoesNotContain(forbidden, properties);
    }
  }

  [Fact]
  public void Tenant_access_token_claims_remain_tenant_shaped()
  {
    var properties = typeof(AccessTokenClaims).GetProperties().Select(property => property.Name).ToArray();

    Assert.Contains("TenantId", properties);
    Assert.Contains("TenantUserId", properties);
    Assert.Contains("Roles", properties);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0075")]
  // `AC-TEN-0075` — *"The security plane is selected by a SERVER-SIDE TYPED ISSUER PATH and a distinct
  // `PlatformAccessTokenClaimsProvider`; there is NO CALLER-CONTROLLABLE `IssueToken(bool/string)` API and
  // the tenant claims provider remains tenant-only and unchanged."*
  //
  // The middle clause is asserted exactly: every `Issue` overload takes precisely two parameters, the first
  // one of the two strongly-typed claims records and the second a timestamp — **so there is no overload a
  // caller could use to name a plane**, and the ban is on the SHAPE of the API rather than on a method name
  // somebody might rename. The third clause is the neighbouring `Tenant_access_token_claims_remain_tenant_
  // shaped`, and the distinct provider type exists and is exercised in `PlatformAccessTokenClaimsTests`.
  public void Access_token_issuer_selects_the_plane_by_type_never_by_a_caller_flag()
  {
    // No Issue overload may take a bool or a raw string (a plane selector). The plane is chosen by the
    // strongly-typed claims argument (AccessTokenClaims vs PlatformAccessTokenClaims) only.
    var issueMethods = typeof(IAccessTokenIssuer).GetMethods().Where(method => method.Name == "Issue").ToArray();

    Assert.NotEmpty(issueMethods);
    foreach (var method in issueMethods)
    {
      var parameters = method.GetParameters();
      Assert.Equal(2, parameters.Length);
      Assert.Contains(parameters[0].ParameterType, new[] { typeof(AccessTokenClaims), typeof(PlatformAccessTokenClaims) });
      Assert.Equal(typeof(DateTimeOffset), parameters[1].ParameterType);
      Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(string));
    }
  }
}
