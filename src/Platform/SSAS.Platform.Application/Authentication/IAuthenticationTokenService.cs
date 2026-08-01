using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Authentication;

public interface IAuthenticationTokenService
{
  GeneratedTenantSelectionProof GenerateTenantSelectionProof(long identityId, long securityVersion, AuthenticationClientId clientId);

  GeneratedRefreshToken GenerateRefreshToken(long authenticationSessionId, Guid tokenFamilyId, AuthenticationClientId clientId);

  bool TryReadPublicId(SensitiveAuthenticationTokenInput token, out Guid publicId);

  bool VerifyTenantSelection(TenantSelectionTransaction transaction, SensitiveAuthenticationTokenInput proof);

  bool VerifyRefreshToken(AuthenticationSession session, RefreshTokenRecord record, SensitiveAuthenticationTokenInput token);
}
