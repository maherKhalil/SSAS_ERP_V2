using System.Security.Claims;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

namespace SSAS.Host.API.Authorization;

internal static class TenantAuthorizationContext
{
  public static bool HasValidatedTenant(ClaimsPrincipal user, ICurrentTenant currentTenant)
  {
    if (user.Identity?.IsAuthenticated != true || currentTenant.TenantId is not { } currentTenantId)
    {
      return false;
    }

    var tenantClaims = user.Claims
      .Where(claim => StringComparer.Ordinal.Equals(claim.Type, JwtClaimTypes.TenantId))
      .Select(claim => Guid.TryParse(claim.Value, out var tenantId) ? tenantId : (Guid?)null)
      .ToArray();

    return tenantClaims.Length == 1 && tenantClaims[0] == currentTenantId;
  }
}
