using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain;

namespace SSAS.Host.API.Authentication;

public sealed class AccessTokenIssuer(ISigningKeyProvider keyProvider, IOptions<JwtOptions> optionsAccessor)
  : IAccessTokenIssuer
{
  private readonly JwtOptions options = optionsAccessor.Value;

  public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc)
  {
    try
    {
      if (claims.ClientId.Value != AuthenticationClientId.V1Web || claims.TenantId == Guid.Empty ||
        claims.IdentityId <= 0 || claims.TenantUserId <= 0 || claims.AuthenticationSessionId <= 0 ||
        claims.SecurityVersion <= 0 || string.IsNullOrWhiteSpace(claims.Subject))
        return Failure();

      if (claims.Roles.Any(string.IsNullOrWhiteSpace) || claims.Permissions.Any(string.IsNullOrWhiteSpace))
        return Failure();
      var roles = claims.Roles.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
      var permissions = claims.Permissions.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

      var tokenClaims = new List<Claim>
      {
        new(JwtClaimTypes.Subject, claims.Subject),
        new(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
        new("iat", issuedUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        new(JwtClaimTypes.IdentityId, claims.IdentityId.ToString(CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.TenantId, claims.TenantId.ToString("D", CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.TenantUserId, claims.TenantUserId.ToString(CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.SessionId, claims.AuthenticationSessionId.ToString(CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.ClientId, claims.ClientId.Value),
        new(JwtClaimTypes.SecurityVersion, claims.SecurityVersion.ToString(CultureInfo.InvariantCulture))
      };
      tokenClaims.AddRange(roles.Select(value => new Claim(JwtClaimTypes.Role, value)));
      tokenClaims.AddRange(permissions.Select(value => new Claim(JwtClaimTypes.Permission, value)));

      var expires = issuedUtc.Add(options.AccessTokenLifetime);
      var key = keyProvider.Snapshot.ActiveSigningKey;
      var token = new JwtSecurityToken(
        options.Issuer,
        options.Audience,
        tokenClaims,
        issuedUtc.UtcDateTime,
        expires.UtcDateTime,
        new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
      token.Header[JwtHeaderParameterNames.Kid] = key.KeyId;
      var encoded = new JwtSecurityTokenHandler().WriteToken(token);
      if (encoded.Length > options.MaximumEncodedTokenSize) return Failure();
      return Result.Success(new IssuedAccessToken(new SensitiveAccessToken(encoded), expires));
    }
    catch (Exception)
    {
      return Failure();
    }
  }

  public Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc)
  {
    try
    {
      ArgumentNullException.ThrowIfNull(claims);
      if (claims.ClientId is null || string.IsNullOrWhiteSpace(claims.Subject) ||
        claims.IdentityId <= 0 || claims.AuthenticationSessionId <= 0 || claims.SecurityVersion <= 0)
        return Failure();

      // A platform token carries no roles and must carry at least one platform-support permission.
      if (claims.Permissions is not { Count: > 0 } || claims.Permissions.Any(string.IsNullOrWhiteSpace))
        return Failure();
      var permissions = claims.Permissions.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

      // Platform profile: security_plane=platform, no tenant_id/tenant_user_id/role/company_id. The plane
      // is stamped here from the typed path, never from caller input.
      var tokenClaims = new List<Claim>
      {
        new(JwtClaimTypes.Subject, claims.Subject),
        new(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)),
        new("iat", issuedUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        new(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
        new(JwtClaimTypes.IdentityId, claims.IdentityId.ToString(CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.SessionId, claims.AuthenticationSessionId.ToString(CultureInfo.InvariantCulture)),
        new(JwtClaimTypes.ClientId, claims.ClientId.Value),
        new(JwtClaimTypes.SecurityVersion, claims.SecurityVersion.ToString(CultureInfo.InvariantCulture))
      };
      tokenClaims.AddRange(permissions.Select(value => new Claim(JwtClaimTypes.Permission, value)));

      var expires = issuedUtc.Add(options.AccessTokenLifetime);
      var key = keyProvider.Snapshot.ActiveSigningKey;
      var token = new JwtSecurityToken(
        options.Issuer,
        options.Audience,
        tokenClaims,
        issuedUtc.UtcDateTime,
        expires.UtcDateTime,
        new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
      token.Header[JwtHeaderParameterNames.Kid] = key.KeyId;
      var encoded = new JwtSecurityTokenHandler().WriteToken(token);
      if (encoded.Length > options.MaximumEncodedTokenSize) return Failure();
      return Result.Success(new IssuedAccessToken(new SensitiveAccessToken(encoded), expires));
    }
    catch (Exception)
    {
      return Failure();
    }
  }

  private static Result<IssuedAccessToken> Failure() =>
    Result.Failure<IssuedAccessToken>(AuthenticationErrors.AccessTokenIssuanceUnavailable);
}
