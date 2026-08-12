using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Host.API.Authorization;

// Platform-plane request authorization (ADR-015 §8 / DEC-TEN-0022). Fail-closed and STATELESS: the decision is
// made purely from the validated ClaimsPrincipal plus the code-owned permission catalog. It performs NO database,
// principal-status, or session lookup — the approved model is that an already-issued short-lived access JWT
// retains its authority until natural expiry, and live changes take effect at refresh (not per request).
//
// A request is authorized iff: it is authenticated; it carries exactly one security_plane claim equal (ordinal) to
// "platform"; the requested permission is catalog-known with PermissionScope.PlatformSupport; and the token
// carries that exact permission claim. A tenant token (no platform plane) can never satisfy this requirement, and
// this handler never satisfies a tenant PermissionRequirement/RoleRequirement.
public sealed class PlatformPermissionAuthorizationHandler(IPermissionCatalog permissionCatalog)
  : AuthorizationHandler<PlatformPermissionRequirement>
{
  protected override Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    PlatformPermissionRequirement requirement)
  {
    if (IsPlatformPlane(context.User) &&
      permissionCatalog.TryGet(requirement.Permission, out var permission) &&
      permission.Scope == PermissionScope.PlatformSupport &&
      context.User.Claims.Any(claim =>
        StringComparer.Ordinal.Equals(claim.Type, JwtClaimTypes.Permission) &&
        StringComparer.Ordinal.Equals(claim.Value, requirement.Permission)))
    {
      context.Succeed(requirement);
    }

    return Task.CompletedTask;
  }

  // Trusted discriminator is the security_plane claim only — never permission presence, tenant-claim absence, or
  // id heuristics. Exactly one claim, ordinal-exact "platform" (no trim, no case-fold); anything else denies.
  private static bool IsPlatformPlane(ClaimsPrincipal user)
  {
    if (user.Identity is not { IsAuthenticated: true })
    {
      return false;
    }

    var planes = user.FindAll(JwtClaimTypes.SecurityPlane).ToArray();
    return planes.Length == 1 && StringComparer.Ordinal.Equals(planes[0].Value, SecurityPlane.Platform);
  }
}
