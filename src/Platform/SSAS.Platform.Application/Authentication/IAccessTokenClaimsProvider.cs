using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Authentication;

public interface IAccessTokenClaimsProvider
{
  Task<Result<AccessTokenClaims>> GetClaimsAsync(
    long authenticationSessionId,
    long identityId,
    long tenantUserId,
    Guid tenantId,
    AuthenticationClientId clientId,
    long securityVersion,
    CancellationToken cancellationToken = default);
}
