using Microsoft.AspNetCore.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

namespace SSAS.Host.API.Authorization;

public sealed class RoleAuthorizationHandler(
  ICurrentTenant currentTenant,
  LiveTenantEligibilityAuthorization tenantEligibility,
  IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<RoleRequirement>
{
  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    RoleRequirement requirement)
  {
    if (TenantAuthorizationContext.HasValidatedTenant(context.User, currentTenant) &&
      currentTenant.TenantId is { } tenantId &&
      await tenantEligibility.IsActiveAsync(tenantId, httpContextAccessor.HttpContext?.RequestAborted ?? default) &&
      context.User.Claims.Any(claim =>
        StringComparer.Ordinal.Equals(claim.Type, JwtClaimTypes.Role) &&
        StringComparer.Ordinal.Equals(claim.Value, requirement.Role)))
    {
      context.Succeed(requirement);
    }

  }
}
