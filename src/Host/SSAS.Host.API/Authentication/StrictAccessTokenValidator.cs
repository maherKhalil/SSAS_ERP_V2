using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Host.API.Authentication;

public static class StrictAccessTokenValidator
{
  private static readonly string[] CriticalClaims =
  [
    "iss", "aud", JwtClaimTypes.Subject, JwtClaimTypes.JwtId, "iat", "nbf", "exp",
    JwtClaimTypes.IdentityId, JwtClaimTypes.TenantId, JwtClaimTypes.TenantUserId,
    JwtClaimTypes.SessionId, JwtClaimTypes.ClientId, JwtClaimTypes.SecurityVersion
  ];

  public static Task ValidateAsync(TokenValidatedContext context)
  {
    var principal = context.Principal;
    if (principal is null || CriticalClaims.Any(type => principal.FindAll(type).Count() != 1) ||
      string.IsNullOrWhiteSpace(principal.FindFirstValue(JwtClaimTypes.Subject)) ||
      !TryPositive(principal.FindFirstValue(JwtClaimTypes.IdentityId)) ||
      !TryPositive(principal.FindFirstValue(JwtClaimTypes.TenantUserId)) ||
      !TryPositive(principal.FindFirstValue(JwtClaimTypes.SessionId)) ||
      !TryPositive(principal.FindFirstValue(JwtClaimTypes.SecurityVersion)) ||
      !TryCanonicalGuid(principal.FindFirstValue(JwtClaimTypes.TenantId), "D", out var tenantId) || tenantId == Guid.Empty ||
      !TryCanonicalGuid(principal.FindFirstValue(JwtClaimTypes.JwtId), "N", out _) ||
      !TryNumericDate(principal.FindFirstValue("iat")) ||
      !TryNumericDate(principal.FindFirstValue("nbf")) ||
      !TryNumericDate(principal.FindFirstValue("exp")) ||
      !string.Equals(principal.FindFirstValue(JwtClaimTypes.ClientId), AuthenticationClientId.V1Web, StringComparison.Ordinal) ||
      HasDuplicates(principal, JwtClaimTypes.Role) || HasDuplicates(principal, JwtClaimTypes.Permission))
    {
      context.Fail("The access token claims are invalid.");
    }

    return Task.CompletedTask;
  }

  private static bool TryPositive(string? value) =>
    long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 &&
    string.Equals(parsed.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal);

  private static bool TryNumericDate(string? value) => TryPositive(value);

  private static bool TryCanonicalGuid(string? value, string format, out Guid parsed)
  {
    return Guid.TryParseExact(value, format, out parsed) &&
      string.Equals(parsed.ToString(format, CultureInfo.InvariantCulture), value, StringComparison.Ordinal);
  }

  private static bool HasDuplicates(ClaimsPrincipal principal, string type)
  {
    var values = principal.FindAll(type).Select(claim => claim.Value).ToArray();
    return values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length;
  }
}
