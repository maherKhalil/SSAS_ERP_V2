namespace SSAS.Platform.Domain.Enums;

public enum AuthenticationSessionRevocationReason
{
  SessionLimitExceeded = 1,
  PasswordReset = 2,
  SecurityStateChanged = 3,
  IdentityIneligible = 4,
  MembershipIneligible = 5,
  TenantIneligible = 6,
  Administrative = 7
}
