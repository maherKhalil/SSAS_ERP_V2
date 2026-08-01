namespace SSAS.Platform.Application.Tenants;

public sealed record ActivateTenantCommand(Guid TenantId, byte[] ExpectedRowVersion);
