namespace SSAS.Platform.Application.Subscriptions;

// INVALIDATION ON CHANGE, NEVER A TTL (FP-014, `OD-SUB-0004`).
//
// ---- TWO INVALIDATION AXES, BECAUSE A PLAN EDIT FANS OUT.
//
// `InvalidateTenant` covers the obvious event -- this tenant's subscription or grants changed.
//
// **`InvalidatePlan` covers the one that is easy to miss.** A plan is shared, so amending its modules
// or limits changes the entitlement of every tenant whose in-force record names it. An invalidation
// keyed only on `TenantId` leaves all of them stale -- the one case where `BR-SUB-0012` fails without
// anyone touching the tenant it fails for. Flagged in T-006, before there was a cache to fail.
//
// ---- WHAT IS DELIBERATELY ABSENT.
//
// **No expiry, no TTL, no refresh interval.** A cached entry holds facts that change only when
// something is written, and expiry is evaluated on read against the clock. An entry may therefore live
// until something writes -- which is exactly what `OD-SUB-0004` ruled and what a TTL would violate.
public interface ITenantEntitlementCache
{
  bool TryGet(Guid tenantId, out TenantEntitlementSnapshot snapshot);

  void Store(TenantEntitlementSnapshot snapshot);

  // Called when this tenant's own commercial records change.
  void InvalidateTenant(Guid tenantId);

  // Called when a plan changes. Evicts every tenant currently cached against that plan.
  void InvalidatePlan(Guid subscriptionPlanId);
}
