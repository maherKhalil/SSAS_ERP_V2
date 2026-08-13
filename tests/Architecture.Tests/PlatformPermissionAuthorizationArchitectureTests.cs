using System.Reflection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Architecture.Tests;

// Phase 4A durable authorization invariants (ADR-015 §8 / DEC-TEN-0022). The platform permission authorization
// handler must stay STATELESS — claims + code-owned catalog only — so an already-issued short-lived access JWT
// keeps its authority until expiry; live changes are applied at refresh, never per request.
public sealed class PlatformPermissionAuthorizationArchitectureTests
{
  [Fact]
  public void Platform_permission_handler_depends_only_on_the_permission_catalog_and_no_persistence()
  {
    var parameters = typeof(PlatformPermissionAuthorizationHandler).GetConstructors().Single().GetParameters();

    // The single dependency is the code-owned catalog.
    Assert.Contains(parameters, parameter => parameter.ParameterType.Name == "IPermissionCatalog");

    // No DbContext, repository, read-service, or tenant/session/current-context dependency may be introduced.
    foreach (var parameter in parameters)
    {
      var typeName = parameter.ParameterType.Name;
      Assert.DoesNotContain("DbContext", typeName, StringComparison.Ordinal);
      Assert.DoesNotContain("Repository", typeName, StringComparison.Ordinal);
      Assert.DoesNotContain("ReadService", typeName, StringComparison.Ordinal);
      Assert.DoesNotContain("CurrentTenant", typeName, StringComparison.Ordinal);
      Assert.DoesNotContain("Session", typeName, StringComparison.Ordinal);
      Assert.DoesNotContain("Principal", typeName, StringComparison.Ordinal);
    }
  }

  [Fact]
  public void Platform_permission_handler_holds_no_persistence_field()
  {
    var fields = typeof(PlatformPermissionAuthorizationHandler)
      .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

    Assert.DoesNotContain(fields, field =>
      field.FieldType.Name.Contains("DbContext", StringComparison.Ordinal) ||
      field.FieldType.Name.Contains("Repository", StringComparison.Ordinal) ||
      field.FieldType.Name.Contains("ReadService", StringComparison.Ordinal));
  }

  [Fact]
  public void Platform_permission_policy_prefix_is_distinct_from_the_tenant_prefixes()
  {
    Assert.Equal("PlatformPermission:", PlatformPermissionAuthorizationDefaults.PolicyPrefix);

    // Neither prefix is a prefix of the other, so the shared policy provider cannot conflate the two planes.
    Assert.False(PlatformPermissionAuthorizationDefaults.PolicyPrefix.StartsWith(PermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal));
    Assert.False(PermissionAuthorizationDefaults.PolicyPrefix.StartsWith(PlatformPermissionAuthorizationDefaults.PolicyPrefix, StringComparison.Ordinal));
    Assert.NotEqual(RoleAuthorizationDefaults.PolicyPrefix, PlatformPermissionAuthorizationDefaults.PolicyPrefix);
  }

  [Fact]
  public void Platform_support_principal_has_no_security_version_member()
  {
    // L4: principal status is a separate platform-plane state; the principal carries no SecurityVersion, so a
    // platform-only Disable can never bump a version (that would kill tenant access). AC-TEN-0068.
    var principal = typeof(PlatformSupportPrincipal);
    Assert.Null(principal.GetProperty("SecurityVersion"));
    Assert.Null(principal.GetField("SecurityVersion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
  }
}
