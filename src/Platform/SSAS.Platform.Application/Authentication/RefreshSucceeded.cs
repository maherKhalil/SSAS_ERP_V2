namespace SSAS.Platform.Application.Authentication;

public sealed record RefreshSucceeded(
  long AuthenticationSessionId,
  long IdentityId,
  long TenantUserId,
  Guid TenantId,
  AuthenticationClientId ClientId,
  SensitiveRefreshToken RefreshToken,
  DateTimeOffset RefreshTokenExpiresUtc,
  IssuedAccessToken AccessToken);
