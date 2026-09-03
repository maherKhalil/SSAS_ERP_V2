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
  [Trait("Criterion", "AC-TEN-0091")]
  // `AC-TEN-0091`'s SECOND half — *"`PlatformPermissionAuthorizationHandler` and the plane-authenticated
  // policies perform NO LIVE PRINCIPAL-STATUS / PER-REQUEST DB [authorization]."* Carried by an ABSENT
  // DEPENDENCY, which is the strongest available form: **a handler cannot query a database it has no way to
  // reach.** The no-new-claim half is on `PlatformSupportAuthenticationEndToEndTests.Platform_login_...`,
  // same trait.
  //
  // ⚠ AND THE BAN INCLUDES `Principal` AND `Session` AS WELL AS THE THREE PERSISTENCE SPELLINGS, which is
  // what makes it *no live PRINCIPAL-STATUS* rather than merely *no database*: **a read service is not the
  // only way to reach live status, and a `PlatformSupportPrincipal` parameter would have been one.**
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
  [Trait("Criterion", "AC-TEN-0068")]
  // ⚠ THE TRAIT IS NEW; THE CITATION WAS ALREADY HERE IN PROSE. The comment below has named `AC-TEN-0068`
  // since it was written, and the assertions are the criterion verbatim — *"No `SecurityVersion` is added to
  // `PlatformSupportPrincipal`"*. **A trait census reported this criterion uncited for as long as it has
  // existed, and a text census reported it covered; the assertions were right the whole time.** Found by
  // auditing my OWN identical slip on `AC-TEN-0042` two commits ago, which is the only reason anyone looked.
  //
  // ⚠⚠ AND THE EXISTING ANTI-VACUITY NOTE (258) IS THE SHARPEST IN THIS FILE: the assertions are bound to
  // `nameof(AuthenticationAccount.SecurityVersion)` — the tenant-plane account that DOES carry the version —
  // because `GetProperty`/`GetField` return null for a member that is ABSENT **and** for one that is
  // MISSPELT. **A bare string would have asserted nothing a typo could not satisfy**, which is the same
  // failure as a matcher that matches nothing, arriving in an absence assertion instead of a ban.
  public void Platform_support_principal_has_no_security_version_member()
  {
    // L4: principal status is a separate platform-plane state; the principal carries no SecurityVersion, so a
    // platform-only Disable can never bump a version (that would kill tenant access). AC-TEN-0068.
    var principal = typeof(PlatformSupportPrincipal);
    // ⚠ Bound to `AuthenticationAccount.SecurityVersion` (258): the tenant-plane account that DOES carry
    // the version, same context, same kind. `GetProperty`/`GetField` return null for a member that is
    // absent AND for one that is misspelt, so the bare strings asserted nothing a typo could not satisfy.
    Assert.Null(principal.GetProperty(nameof(SSAS.Platform.Domain.Authentication.AuthenticationAccount.SecurityVersion)));
    Assert.Null(principal.GetField(
      nameof(SSAS.Platform.Domain.Authentication.AuthenticationAccount.SecurityVersion),
      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
  }
}
