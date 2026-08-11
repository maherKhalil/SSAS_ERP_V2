namespace SSAS.Platform.Domain.Enums;

// Platform-plane session revocation reasons (ADR-016 Phase 3C / DEC-TEN-0022). Deliberately separate from
// the tenant AuthenticationSessionRevocationReason: the tenant-only MembershipIneligible/TenantIneligible
// reasons are NOT carried into the platform plane, and PlatformPrincipalIneligible is added for the
// Disabled-principal / zero-usable-permission continuation denial. Reuse/compromise is a status, not a reason.
public enum PlatformAuthenticationSessionRevocationReason
{
  SessionLimitExceeded = 1,
  SecurityStateChanged = 2,
  IdentityIneligible = 3,
  Administrative = 4,
  UserLogout = 5,
  PlatformPrincipalIneligible = 6
}
