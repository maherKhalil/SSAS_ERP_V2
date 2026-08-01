using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Authentication;

public interface IAccessTokenIssuer
{
  Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc);
}
