namespace SSAS.Platform.Domain.Enums;

// Lifecycle of a physical tenant-storage database endpoint (ADR-017 / ADR-018).
//
// This is ONE of the four orthogonal status dimensions ADR-018 defines. The other three —
// ConnectivityStatus, SchemaCompatibilityStatus and MigrationExecutionStatus — are deliberately NOT
// modelled yet: they are maintained by the ADR-018 health slice, and persisting them before that slice
// exists would leave columns permanently reading Unknown, which the ADR-018 gating table treats as DENY.
//
// Ready is a lifecycle position here, not a derived readiness verdict. ADR-018 keeps overall readiness a
// conclusion drawn from all four dimensions plus routing and credential validity, so no independently
// writable IsReady flag exists.
//
// Onboarding is included in the value set for forward compatibility with ADR-021 customer-managed
// onboarding; nothing in this slice writes it.
public enum TenantDatabaseProvisioningStatus
{
  Registered = 1,
  Provisioning = 2,
  Onboarding = 3,
  Ready = 4,
  Disabled = 5
}
