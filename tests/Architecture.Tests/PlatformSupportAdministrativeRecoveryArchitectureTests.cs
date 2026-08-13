using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Architecture.Tests;

// Phase 4D-0 durable invariants (DEC-TEN-0026). General usable platform authority and usable platform
// ADMINISTRATIVE authority are two distinct live states; collapsing them back into a single predicate would
// silently restore the lockout this phase exists to remove. Phase 4D authority-management HTTP stays absent.
public sealed class PlatformSupportAdministrativeRecoveryArchitectureTests
{
  [Fact]
  public void Authority_state_service_exposes_general_and_administrative_predicates_separately()
  {
    var members = typeof(IPlatformSupportAuthorityStateReadService)
      .GetMethods()
      .Select(method => method.Name)
      .ToArray();

    Assert.Contains("HasUsablePlatformAuthorityAsync", members);
    Assert.Contains("HasUsablePlatformAdministrativeAuthorityAsync", members);
    // Exactly two state questions — neither may be folded into the other or hidden behind a shared flag.
    Assert.Equal(2, members.Length);
  }

  [Fact]
  public void Administrative_authority_is_anchored_to_the_canonical_administer_permission()
  {
    // Recovery keys off exactly Platform.Support.Administer from the code-owned catalog; no parallel
    // "admin" permission may be introduced to express administrative capability.
    var catalog = new PlatformPermissionCatalog();

    Assert.Equal("Platform.Support.Administer", PlatformPermissionNames.AdministerPlatformSupport);
    Assert.True(catalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var administer));
    Assert.Equal(PermissionScope.PlatformSupport, administer.Scope);
  }

  [Fact]
  public void Phase_4D_authority_management_http_is_not_exposed_yet()
  {
    // 4D-0 is state logic only: authority mutation/read routes remain gated until 4D.
    var apiTypes = typeof(PlatformAuthenticatedResponse).Assembly
      .GetTypes()
      .Select(type => type.Name)
      .ToArray();

    foreach (var forbidden in new[]
      {
        "PlatformSupportPrincipalEndpointRouteBuilderExtensions",
        "PlatformSupportAuthorityEndpointRouteBuilderExtensions",
        "PlatformSupportRecoveryEndpointRouteBuilderExtensions"
      })
    {
      Assert.DoesNotContain(forbidden, apiTypes);
    }
  }
}
