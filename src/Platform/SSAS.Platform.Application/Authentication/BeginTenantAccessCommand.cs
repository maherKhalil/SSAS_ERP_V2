namespace SSAS.Platform.Application.Authentication;

public sealed record BeginTenantAccessCommand(VerifiedIdentity VerifiedIdentity, AuthenticationClientId ClientId);
