namespace SSAS.Platform.Application.Authentication;

public sealed record TenantSelectedAutomatically(SessionCreated Session) : BeginTenantAccessResult;
