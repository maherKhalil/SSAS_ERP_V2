using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

// The append path's persistence. The read is deliberately a `MaxAsync` over the covering index
// `UX_TenantSubscriptions_Tenant_EffectiveFromDesc` rather than a fetch of the latest entity: the caller
// needs one instant, not an aggregate, and materialising a record here would put a tracked append-only
// entity in the change tracker for no reason.
public sealed class TenantSubscriptionRepository(PlatformDbContext dbContext) : ITenantSubscriptionRepository
{
  public async Task<DateTimeOffset?> GreatestEffectiveFromUtcAsync(
    Guid tenantId, CancellationToken cancellationToken = default) =>
    await dbContext.TenantSubscriptions
      .Where(subscription => subscription.TenantId == tenantId)
      .Select(subscription => (DateTimeOffset?)subscription.EffectiveFromUtc)
      .MaxAsync(cancellationToken);

  public Task<bool> PlanExistsAsync(
    Guid subscriptionPlanId, CancellationToken cancellationToken = default) =>
    dbContext.SubscriptionPlans.AnyAsync(plan => plan.Id == subscriptionPlanId, cancellationToken);

  public async Task AddAsync(
    TenantSubscription subscription, CancellationToken cancellationToken = default)
  {
    await dbContext.TenantSubscriptions.AddAsync(subscription, cancellationToken);
  }
}
