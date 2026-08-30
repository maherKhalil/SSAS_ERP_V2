using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.IdentityAccess;

// Phase 1 platform-plane permission infrastructure (ADR-015, DEC-TEN-0018):
// tenant-token claim isolation and tenant-facing catalog scoping.
public sealed class PlatformPlanePermissionTests
{
  [Fact]
  public void Claim_filter_drops_known_platform_support_permissions()
  {
    var catalog = new PlatformPermissionCatalog();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(
      new[] { PlatformPermissionNames.ViewTenants, PlatformPermissionNames.ManageTenants, PlatformPermissionNames.TenantLifecycle },
      catalog);

    Assert.Empty(filtered);
  }

  [Fact]
  public void Claim_filter_keeps_tenant_scoped_permissions()
  {
    var catalog = new PlatformPermissionCatalog();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(
      new[] { PlatformPermissionNames.ViewCompanies, PlatformPermissionNames.ViewUsers },
      catalog);

    Assert.Equal(new[] { PlatformPermissionNames.ViewCompanies, PlatformPermissionNames.ViewUsers }, filtered);
  }

  [Fact]
  public void Claim_filter_preserves_unknown_permission_names()
  {
    // Unknown names follow existing safe behavior: they are preserved. An unknown name cannot match a
    // known platform permission requirement, so passing it through changes no authorization outcome.
    var catalog = new PlatformPermissionCatalog();
    var unknownNames = new[] { "Platform.Unknown.Thing" };

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(unknownNames, catalog);

    Assert.Equal(unknownNames, filtered);
  }

  [Fact]
  public void Corrupt_platform_support_assignment_never_becomes_a_tenant_token_claim()
  {
    // AC-TEN-0030 / TS-TEN-0054: even if stored role-permission data is corrupted to contain a
    // PlatformSupport permission alongside a legitimate tenant permission, only the tenant permission
    // is emitted into the tenant token. Scope is derived from the code-owned catalog, not stored data.
    var catalog = new PlatformPermissionCatalog();

    var filtered = TenantPermissionClaimFilter.FilterToTenantScope(
      new[] { PlatformPermissionNames.ViewCompanies, PlatformPermissionNames.ManageTenants },
      catalog);

    Assert.Equal(new[] { PlatformPermissionNames.ViewCompanies }, filtered);
    Assert.DoesNotContain(PlatformPermissionNames.ManageTenants, filtered);
  }

  [Fact]
  public async Task Tenant_facing_catalog_query_excludes_platform_support_permissions()
  {
    var handler = new ListPermissionCatalogQueryHandler(
      new PlatformPermissionCatalog(),
      new StubCurrentTenant(Guid.NewGuid()),
      new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new ListPermissionCatalogQuery());

    Assert.True(result.IsSuccess);
    Assert.NotEmpty(result.Value);
    Assert.All(result.Value, permission => Assert.Equal(PermissionScope.Tenant, permission.Scope));
    Assert.DoesNotContain(result.Value, permission => permission.Name.StartsWith("Platform.Tenants.", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Tenant_facing_catalog_query_requires_a_trusted_tenant_actor()
  {
    var handler = new ListPermissionCatalogQueryHandler(
      new PlatformPermissionCatalog(),
      new StubCurrentTenant(null),
      new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new ListPermissionCatalogQuery());

    Assert.True(result.IsFailure);
  }

  private sealed class StubCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class StubCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }
}
