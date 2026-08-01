using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Tenants;

public sealed record ListTenantsQuery(TenantStatus? Status, int PageNumber, int PageSize);
