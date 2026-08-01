namespace SSAS.Platform.Domain.Enums;

public enum TenantStatusChangeReason
{
  Created,
  ProvisioningCompleted,
  Administrative,
  Security,
  Compliance,
  Operational,
  CustomerClosure,
  IssueResolved
}
