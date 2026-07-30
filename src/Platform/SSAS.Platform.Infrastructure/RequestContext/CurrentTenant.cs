using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
  public Guid? TenantId
  {
    get
    {
      var user = GetValidatedIdentity();
      var tenantIdValue = user?.FindFirstValue(JwtClaimTypes.TenantId);

      return Guid.TryParse(tenantIdValue, out var tenantId) ? tenantId : null;
    }
  }

  private ClaimsPrincipal? GetValidatedIdentity()
  {
    var user = httpContextAccessor.HttpContext?.User;

    return user?.Identity?.IsAuthenticated == true ? user : null;
  }
}
