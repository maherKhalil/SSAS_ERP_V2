namespace SSAS.Platform.Application.Authentication;

public sealed record SessionCreated(
  long AuthenticationSessionId,
  long IdentityId,
  long TenantUserId,
  Guid TenantId,
  AuthenticationClientId ClientId,
  SensitiveRefreshToken RefreshToken);
