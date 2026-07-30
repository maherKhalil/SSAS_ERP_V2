using Microsoft.AspNetCore.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;

namespace SSAS.Host.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
  protected override Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    PermissionRequirement requirement)
  {
    if (context.User.Identity?.IsAuthenticated == true &&
      context.User.HasClaim(JwtClaimTypes.Permission, requirement.Permission))
    {
      context.Succeed(requirement);
    }

    return Task.CompletedTask;
  }
}
