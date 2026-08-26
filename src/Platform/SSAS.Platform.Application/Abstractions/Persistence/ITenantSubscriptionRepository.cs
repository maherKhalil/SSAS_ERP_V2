using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// THE WRITE SIDE OF THE SUBSCRIPTION HISTORY (FP-014, T-041).
//
// `ITenantEntitlementReader` already reads this data, and reads it for a different purpose: it resolves
// what a tenant is entitled to, flattens the result and hands it to a cache. This is the append path, and
// it needs one fact that resolution never asks for.
//
// ---- WHY `GreatestEffectiveFromUtcAsync` RATHER THAN A `HasAny`.
//
// `TenantSubscription.Append` refuses a record that does not take effect **strictly after** the tenant's
// current latest one, and it takes that maximum as a parameter because a domain object cannot reach a
// database. So the caller must read it inside the same transaction that appends.
//
// A `bool` would answer "does this tenant hold a subscription" and nothing else. The maximum answers that
// too — **null means none** — and additionally supplies the value the append requires. One method, one
// round trip, and the monotonicity check gets a real value rather than an assumed `null`.
public interface ITenantSubscriptionRepository
{
  // The greatest `EffectiveFromUtc` recorded for this tenant, or null if the tenant holds no subscription
  // record at all.
  Task<DateTimeOffset?> GreatestEffectiveFromUtcAsync(
    Guid tenantId, CancellationToken cancellationToken = default);

  Task<bool> PlanExistsAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default);

  // Adds to the unit of work. It does NOT save: issuance shares the caller's transaction so that a tenant
  // and its trial commit together or not at all.
  Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default);
}
