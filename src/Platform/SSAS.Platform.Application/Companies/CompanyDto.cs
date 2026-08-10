using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Companies;

public sealed record CompanyDto(
  Guid CompanyId,
  Guid TenantId,
  string CompanyCode,
  string CompanyName,
  string BaseCurrencyCode,
  CompanyStatus Status,
  DateTimeOffset CreatedUtc,
  string? CreatedBy,
  DateTimeOffset ModifiedUtc,
  string? ModifiedBy,
  DateTimeOffset StatusChangedUtc,
  string StatusChangedBy,
  CompanyStatusChangeReason StatusChangeReasonCode,
  byte[] RowVersion);
