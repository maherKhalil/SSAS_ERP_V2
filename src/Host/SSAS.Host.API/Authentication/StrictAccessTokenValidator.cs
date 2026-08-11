using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Host.API.Authentication;

// Structural, stateless post-signature validation of the two token profiles (ADR-015 / DEC-TEN-0022).
// Profile is selected by the server-issued security_plane claim: absent or "tenant" => tenant profile
// (unchanged, tenant-bound); "platform" => platform profile (non-tenant, permission-based); anything else
// (blank, unknown, wrong-case, duplicate) => rejected. No database, catalog, principal-status, or session
// lookup occurs here; live status/version checks belong to issuance and refresh (Phase 3C-4).
public static class StrictAccessTokenValidator
{
  // Tenant profile: unchanged from prior phases (tenant_id/tenant_user_id required, client V1Web, etc.).
  private static readonly string[] TenantCriticalClaims =
  [
    "iss", "aud", JwtClaimTypes.Subject, JwtClaimTypes.JwtId, "iat", "nbf", "exp",
    JwtClaimTypes.IdentityId, JwtClaimTypes.TenantId, JwtClaimTypes.TenantUserId,
    JwtClaimTypes.SessionId, JwtClaimTypes.ClientId, JwtClaimTypes.SecurityVersion
  ];

  // Platform profile: no tenant_id/tenant_user_id; security_plane present; permission-based.
  private static readonly string[] PlatformCriticalClaims =
  [
    "iss", "aud", JwtClaimTypes.Subject, JwtClaimTypes.JwtId, "iat", "nbf", "exp",
    JwtClaimTypes.IdentityId, JwtClaimTypes.SessionId, JwtClaimTypes.ClientId,
    JwtClaimTypes.SecurityVersion, JwtClaimTypes.SecurityPlane
  ];

  // A platform token must carry none of these; even a single (or blank) occurrence is rejected.
  private static readonly string[] PlatformForbiddenClaims =
  [
    JwtClaimTypes.TenantId, JwtClaimTypes.TenantUserId, JwtClaimTypes.Role, JwtClaimTypes.CompanyId
  ];

  public static Task ValidateAsync(TokenValidatedContext context)
  {
    var principal = context.Principal;
    if (principal is null || !IsValid(principal))
    {
      context.Fail("The access token claims are invalid.");
    }

    return Task.CompletedTask;
  }

  private static bool IsValid(ClaimsPrincipal principal) => ClassifyPlane(principal) switch
  {
    Plane.Tenant => IsValidTenantProfile(principal),
    Plane.Platform => IsValidPlatformProfile(principal),
    _ => false
  };

  private enum Plane
  {
    Invalid,
    Tenant,
    Platform
  }

  private static Plane ClassifyPlane(ClaimsPrincipal principal)
  {
    var planeClaims = principal.FindAll(JwtClaimTypes.SecurityPlane).ToArray();
    if (planeClaims.Length == 0)
    {
      return Plane.Tenant; // Legacy tenant token: absence ⇒ tenant.
    }

    if (planeClaims.Length > 1)
    {
      return Plane.Invalid; // Duplicate security_plane is never valid.
    }

    var value = planeClaims[0].Value;

    // Exact ordinal match only — no normalization, no trimming into validity.
    if (string.Equals(value, SecurityPlane.Tenant, StringComparison.Ordinal))
    {
      return Plane.Tenant;
    }

    return string.Equals(value, SecurityPlane.Platform, StringComparison.Ordinal) ? Plane.Platform : Plane.Invalid;
  }

  private static bool IsValidTenantProfile(ClaimsPrincipal principal) =>
    !TenantCriticalClaims.Any(type => principal.FindAll(type).Count() != 1) &&
    !string.IsNullOrWhiteSpace(principal.FindFirstValue(JwtClaimTypes.Subject)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.IdentityId)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.TenantUserId)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.SessionId)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.SecurityVersion)) &&
    TryCanonicalGuid(principal.FindFirstValue(JwtClaimTypes.TenantId), "D", out var tenantId) && tenantId != Guid.Empty &&
    TryCanonicalGuid(principal.FindFirstValue(JwtClaimTypes.JwtId), "N", out _) &&
    TryNumericDate(principal.FindFirstValue("iat")) &&
    TryNumericDate(principal.FindFirstValue("nbf")) &&
    TryNumericDate(principal.FindFirstValue("exp")) &&
    string.Equals(principal.FindFirstValue(JwtClaimTypes.ClientId), AuthenticationClientId.V1Web, StringComparison.Ordinal) &&
    !HasDuplicates(principal, JwtClaimTypes.Role) &&
    !HasDuplicates(principal, JwtClaimTypes.Permission);

  private static bool IsValidPlatformProfile(ClaimsPrincipal principal) =>
    !PlatformCriticalClaims.Any(type => principal.FindAll(type).Count() != 1) &&
    PlatformForbiddenClaims.All(type => !principal.FindAll(type).Any()) &&
    !string.IsNullOrWhiteSpace(principal.FindFirstValue(JwtClaimTypes.Subject)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.IdentityId)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.SessionId)) &&
    TryPositive(principal.FindFirstValue(JwtClaimTypes.SecurityVersion)) &&
    TryCanonicalGuid(principal.FindFirstValue(JwtClaimTypes.JwtId), "N", out _) &&
    TryNumericDate(principal.FindFirstValue("iat")) &&
    TryNumericDate(principal.FindFirstValue("nbf")) &&
    TryNumericDate(principal.FindFirstValue("exp")) &&
    string.Equals(principal.FindFirstValue(JwtClaimTypes.ClientId), AuthenticationClientId.V1Web, StringComparison.Ordinal) &&
    HasAtLeastOneNonBlankUniquePermission(principal);

  // A platform token is permission-based: at least one permission, none blank, no duplicates
  // (zero-permission issuance is denied — DEC-TEN-0022 — so the profile enforces it structurally too).
  private static bool HasAtLeastOneNonBlankUniquePermission(ClaimsPrincipal principal)
  {
    var permissions = principal.FindAll(JwtClaimTypes.Permission).Select(claim => claim.Value).ToArray();
    return permissions.Length >= 1 &&
      !permissions.Any(string.IsNullOrWhiteSpace) &&
      permissions.Distinct(StringComparer.Ordinal).Count() == permissions.Length;
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
