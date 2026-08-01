using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Tenants;

public sealed record ReactivateTenantCommand(
  Guid TenantId,
  TenantStatusChangeReason Reason,
  byte[] ExpectedRowVersion);
