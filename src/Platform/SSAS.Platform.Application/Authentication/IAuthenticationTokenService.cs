using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Application.Authentication;

public interface IAuthenticationTokenService
{
  GeneratedTenantSelectionProof GenerateTenantSelectionProof(long identityId, long securityVersion, AuthenticationClientId clientId);

  GeneratedRefreshToken GenerateRefreshToken(long authenticationSessionId, Guid tokenFamilyId, AuthenticationClientId clientId);

  bool TryReadPublicId(SensitiveAuthenticationTokenInput token, out Guid publicId);

  bool VerifyTenantSelection(TenantSelectionTransaction transaction, SensitiveAuthenticationTokenInput proof);

  bool VerifyRefreshToken(AuthenticationSession session, RefreshTokenRecord record, SensitiveAuthenticationTokenInput token);

  // Platform-plane refresh verification (ADR-016 Phase 3C / DEC-TEN-0022). Same refresh-token crypto as the
  // tenant plane, bound to the PLATFORM session id/family/client; cross-plane isolation is structural — a
  // platform token's public id only resolves in the platform store, and the hash binds the platform session id.
  bool VerifyRefreshToken(PlatformAuthenticationSession session, PlatformRefreshTokenRecord record, SensitiveAuthenticationTokenInput token);
}
