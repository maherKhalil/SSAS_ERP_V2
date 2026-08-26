namespace SSAS.Platform.Application.Subscriptions;

// THE PLATFORM-DATABASE READ BEHIND THE ENABLEMENT GATE (FP-014, `OD-SUB-0004`).
//
// **The Platform database only.** `REQ-SUB-0005` and `DEC-SUB-0004` require the subscription surface to
// stay readable and administrable while the tenant's ERP database is unavailable, and `ADR-017`
// § Platform database boundary is what makes that true: entitlement is answered without touching the
// tenant server at all.
//
// It returns the FACTS at an instant, never a decision -- see `TenantEntitlementSnapshot` for why the
// distinction is the whole cache design.
public interface ITenantEntitlementReader
{
  // Never returns null and never throws for an unknown tenant: a tenant with no subscription record
  // resolves to `TenantEntitlementSnapshot.None`, which is entitled to nothing. That is an ordinary
  // answer under `CON-0001`, not a fault.
  Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken);
}
