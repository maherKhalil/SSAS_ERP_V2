using System.Globalization;
using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class CurrentAuthenticationSessionAccessor(IHttpContextAccessor httpContextAccessor)
  : ICurrentAuthenticationSession
{
  public CurrentAuthenticationSession? Value
  {
    get
    {
      var principal = httpContextAccessor.HttpContext?.User;
      if (principal?.Identity?.IsAuthenticated != true ||
        !TryPositiveInt64(principal.FindFirst(JwtClaimTypes.IdentityId)?.Value, out var identityId) ||
        !TryPositiveInt64(principal.FindFirst(JwtClaimTypes.TenantUserId)?.Value, out var tenantUserId) ||
        !TryPositiveInt64(principal.FindFirst(JwtClaimTypes.SessionId)?.Value, out var sessionId) ||
        !TryPositiveInt64(principal.FindFirst(JwtClaimTypes.SecurityVersion)?.Value, out var securityVersion) ||
        !TryCanonicalTenantId(principal.FindFirst(JwtClaimTypes.TenantId)?.Value, out var tenantId) ||
        tenantId == Guid.Empty)
      {
        return null;
      }

      var client = AuthenticationClientId.Create(principal.FindFirst(JwtClaimTypes.ClientId)?.Value);
      return client.IsFailure
        ? null
        : new CurrentAuthenticationSession(identityId, tenantId, tenantUserId, sessionId, client.Value, securityVersion);
    }
  }

  private static bool TryPositiveInt64(string? value, out long parsed) =>
    long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0 &&
    string.Equals(parsed.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal);

  private static bool TryCanonicalTenantId(string? value, out Guid tenantId) =>
    Guid.TryParseExact(value, "D", out tenantId) &&
    string.Equals(tenantId.ToString("D", CultureInfo.InvariantCulture), value, StringComparison.Ordinal);
}
