namespace SSAS.Platform.Application.Tenants;

public sealed record CreateTenantCommand(string TenantCode, string TenantName);
