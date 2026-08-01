using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Tenants;

public sealed record TenantDto(
  Guid TenantId,
  string TenantCode,
  string TenantName,
  TenantStatus Status,
  DateTimeOffset CreatedUtc,
  string CreatedBy,
  DateTimeOffset? ModifiedUtc,
  string? ModifiedBy,
  DateTimeOffset StatusChangedUtc,
  string StatusChangedBy,
  TenantStatusChangeReason StatusChangeReasonCode,
  byte[] RowVersion);
