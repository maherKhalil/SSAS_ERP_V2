namespace SSAS.Platform.Application.Authentication;

public sealed record CurrentAuthenticationSession(
  long IdentityId,
  Guid TenantId,
  long TenantUserId,
  long AuthenticationSessionId,
  AuthenticationClientId ClientId,
  long SecurityVersion);
