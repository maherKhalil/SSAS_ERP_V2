using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
  public string? UserId => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.Subject);

  public string? UserName => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.Name);

  public string? Email => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.Email);

  public Guid? CompanyId
  {
    get
    {
      var companyIdValue = GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.CompanyId);

      return Guid.TryParse(companyIdValue, out var companyId) ? companyId : null;
    }
  }

  public string? SessionId => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.SessionId);

  public string? TokenId => GetValidatedIdentity()?.FindFirstValue(JwtClaimTypes.JwtId);

  public IReadOnlyCollection<string> Roles => GetClaimValues(JwtClaimTypes.Role);

  public IReadOnlyCollection<string> Permissions => GetClaimValues(JwtClaimTypes.Permission);

  private ClaimsPrincipal? GetValidatedIdentity()
  {
    var user = httpContextAccessor.HttpContext?.User;

    return user?.Identity?.IsAuthenticated == true ? user : null;
  }

  private string[] GetClaimValues(string claimType)
  {
    return GetValidatedIdentity()?
      .FindAll(claimType)
      .Select(claim => claim.Value)
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Distinct(StringComparer.Ordinal)
      .ToArray() ?? [];
  }
}
