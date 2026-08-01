namespace SSAS.Platform.Application.Abstractions.Persistence;

public sealed record RefreshTokenSessionLocator(
  long AuthenticationSessionId,
  long IdentityId,
  long TenantUserId,
  Guid TenantId);
