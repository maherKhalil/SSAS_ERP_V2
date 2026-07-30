using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
  public string? UserId => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.Subject);

  public string? UserName => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.Name);

  private ClaimsPrincipal? GetValidatedIdentity()
  {
    var user = httpContextAccessor.HttpContext?.User;

    return user?.Identity?.IsAuthenticated == true ? user : null;
  }
}
