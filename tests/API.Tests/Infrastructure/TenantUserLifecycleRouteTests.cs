using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Platform.Application.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE TENANT-USER LIFECYCLE ROUTES EXIST AND ARE GATED (T-091).
// ==================================================================================================
//
// ---- WHY THIS IS ASSERTED AND NOT LEFT TO THE HANDLER TESTS.
//
// **Both handlers already existed, both were registered in DI, and neither was reachable from anything.**
// `Platform.Users.Deactivate` and `Platform.Users.Reactivate` were both catalog-defined and required by no
// endpoint — a permission an administrator could grant that authorized nothing.
//
// **That is the exact mirror of FP-006P**, which was a route requiring a permission no catalog defined and
// so refusing every caller. `EndpointPermissionCatalogJoinTests` asserts endpoints against the catalog;
// nothing asserts the catalog against endpoints, so the mirror can recur silently.
//
// This does not close the general case — that is recorded and is deliberately not widened into here. It
// closes these two, by asserting the property that was missing: **the route exists, and it requires the
// permission that was declared for it.**
//
// ---- AND WHY REINSTATEMENT IS ASSERTED BESIDE DEACTIVATION.
//
// T-091 makes termination close accounts. Without reinstatement, rehiring produces a user nobody can
// restore — `IssueTenantUserInvitation` refuses a deactivated user — **and the one half-state
// `TerminateEmployeeCommandHandler` can reach has no compensating action.** The repair has to exist in the
// change that creates the thing needing repair, so it is asserted in the same place.
//
// ---- IT ENUMERATES FROM THE REAL HOST.
//
// `HostWebApplicationFactory` wraps the real `Program`, so this asserts the surface production actually
// mounts, not one a test host assembled.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantUserLifecycleRouteTests(HostWebApplicationFactory factory)
{
  [Theory]
  [Trait("Criterion", "REQ-SS-0007")]
  [InlineData("/api/platform/tenant-users/{tenantUserId:long}/deactivation", "Platform.Users.Deactivate")]
  [InlineData("/api/platform/tenant-users/{tenantUserId:long}/reactivation", "Platform.Users.Reactivate")]
  public void The_lifecycle_route_is_mounted_and_requires_its_own_permission(string pattern, string permission)
  {
    var endpoint = Routes().SingleOrDefault(route => route.RoutePattern.RawText == pattern);

    Assert.NotNull(endpoint);

    // POST, following `Companies`' activate/deactivate: a lifecycle transition is a named administrative
    // act, not the assignment of a status field.
    Assert.Equal(["POST"], endpoint!.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods);

    // The policy carries the `Permission:` prefix `RequirePermission` stamps on it — asserted as the whole
    // string rather than with `Contains`, so a route gated on a DIFFERENT permission with a similar name
    // cannot satisfy this.
    Assert.Equal($"Permission:{permission}", endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
  }

  // ---- THE TWO PERMISSIONS ARE DISTINCT, AND THAT IS THE POINT OF HAVING TWO.
  //
  // Restoring an account is a different decision from closing one — the `GL.Drafts.Manage` /
  // `GL.Journals.Post` precedent. A single constant used by both routes would pass the assertions above
  // and make the separation a fiction.
  [Fact]
  public void Deactivation_and_reactivation_do_not_share_a_permission() =>
    Assert.NotEqual(
      PlatformPermissionNames.DeactivateUsers,
      PlatformPermissionNames.ReactivateUsers,
      StringComparer.Ordinal);

  private RouteEndpoint[] Routes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
  ];
}
