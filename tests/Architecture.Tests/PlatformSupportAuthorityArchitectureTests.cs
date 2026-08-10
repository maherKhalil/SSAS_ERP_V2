using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Architecture.Tests;

// Durable Phase-2 platform-support authority invariants (ADR-015 / DEC-TEN-0018).
public sealed class PlatformSupportAuthorityArchitectureTests
{
  [Fact]
  public void Platform_support_authority_is_global_and_not_tenant_or_company_owned()
  {
    foreach (var type in new[] { typeof(PlatformSupportPrincipal), typeof(PlatformPermissionAssignment) })
    {
      Assert.DoesNotContain(typeof(ITenantOwnedEntity), type.GetInterfaces());
      Assert.Null(type.GetProperty("TenantId"));
      Assert.Null(type.GetProperty("CompanyId"));
    }
  }

  [Fact]
  public void No_platform_role_construct_is_introduced()
  {
    var forbidden = new[] { "PlatformRole", "SupportRole", "AdminRole", "SuperAdminRole", "PlatformSupportRole" };

    foreach (var assembly in new[] { typeof(PlatformSupportPrincipal).Assembly, typeof(PlatformSupportPermissionFilter).Assembly })
    {
      Assert.DoesNotContain(assembly.GetTypes(), type => forbidden.Contains(type.Name, StringComparer.Ordinal));
    }
  }

  [Fact]
  public void Only_platform_support_permissions_can_be_granted_to_a_principal()
  {
    var catalog = new PlatformPermissionCatalog();
    var principal = PlatformSupportPrincipal.Register(1).Value;
    var occurred = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var tenantScoped));
    Assert.True(principal.GrantPermission(tenantScoped, "actor", occurred).IsFailure);

    Assert.True(catalog.TryGet(PlatformPermissionNames.ManageTenants, out var platformScoped));
    Assert.Equal(PermissionScope.PlatformSupport, platformScoped.Scope);
    Assert.True(principal.GrantPermission(platformScoped, "actor", occurred).IsSuccess);
  }

  [Fact]
  public void The_two_permission_planes_stay_structurally_separate()
  {
    var catalog = new PlatformPermissionCatalog();
    var occurred = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    // A tenant role cannot receive a PlatformSupport permission...
    var role = Role.CreateCustom(Guid.NewGuid(), RoleName.Create("Administrator").Value, null, Guid.NewGuid(), occurred);
    Assert.True(catalog.TryGet(PlatformPermissionNames.ManageTenants, out var platformPermission));
    Assert.True(role.AssignPermission(platformPermission, "actor", Guid.NewGuid(), occurred).IsFailure);

    // ...and a platform-support principal cannot receive a Tenant permission.
    var principal = PlatformSupportPrincipal.Register(1).Value;
    Assert.True(catalog.TryGet(PlatformPermissionNames.ViewCompanies, out var tenantPermission));
    Assert.True(principal.GrantPermission(tenantPermission, "actor", occurred).IsFailure);
  }

  [Fact]
  public void No_platform_token_profile_or_handler_is_introduced_in_this_phase()
  {
    // security_plane claim is a Phase-3 concern and must not exist yet.
    var claimValues = typeof(JwtClaimTypes)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();
    Assert.DoesNotContain("security_plane", claimValues);

    // The platform authorization handler and RequirePlatformPermission convention are Phase-4 concerns.
    var hostAssembly = typeof(SSAS.Host.API.Authorization.PermissionAuthorizationHandler).Assembly;
    Assert.DoesNotContain(hostAssembly.GetTypes(), type => type.Name == "PlatformPermissionAuthorizationHandler");

    var apiAssembly = typeof(RowVersionCodec).Assembly;
    Assert.DoesNotContain(
      apiAssembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)),
      method => method.Name == "RequirePlatformPermission");
  }

  [Fact]
  public void Principal_status_enum_has_exactly_active_and_disabled()
  {
    Assert.Equal(
      ["Active", "Disabled"],
      Enum.GetNames<PlatformSupportPrincipalStatus>().OrderBy(name => name, StringComparer.Ordinal));
  }

  [Fact]
  public void No_bootstrap_configuration_is_introduced_in_this_phase()
  {
    // Genesis/recovery bootstrap (DEC-TEN-0019) is Phase 3B, not Phase 3A.
    var forbidden = new[] { "PlatformSupportBootstrapOptions", "PlatformSupportBootstrap", "PlatformSupportBootstrapGate" };

    foreach (var assembly in new[] { typeof(PlatformSupportPrincipal).Assembly, typeof(PlatformSupportPermissionFilter).Assembly })
    {
      Assert.DoesNotContain(assembly.GetTypes(), type => forbidden.Contains(type.Name, StringComparer.Ordinal));
    }
  }
}
