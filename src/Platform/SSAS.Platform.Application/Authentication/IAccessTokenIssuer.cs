using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Authentication;

public interface IAccessTokenIssuer
{
  Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc);

  // Platform-plane profile (ADR-015 / DEC-TEN-0022). The plane is selected by the strongly-typed
  // argument, never by a caller-supplied flag/string; the issuer stamps security_plane=platform and
  // emits no tenant_id/tenant_user_id/role/company_id.
  Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc);
}
