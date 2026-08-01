namespace SSAS.Platform.Application.Authentication;

public sealed record AccessTokenClaims(
  string Subject,
  long IdentityId,
  Guid TenantId,
  long TenantUserId,
  long AuthenticationSessionId,
  AuthenticationClientId ClientId,
  long SecurityVersion,
  IReadOnlyList<string> Roles,
  IReadOnlyList<string> Permissions);
