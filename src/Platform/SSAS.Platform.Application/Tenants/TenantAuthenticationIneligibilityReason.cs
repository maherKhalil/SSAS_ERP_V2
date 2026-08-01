namespace SSAS.Platform.Application.Tenants;

public enum TenantAuthenticationIneligibilityReason
{
  None,
  TenantNotFound,
  Provisioning,
  Suspended,
  Archived
}
