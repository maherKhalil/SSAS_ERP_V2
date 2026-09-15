using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

internal sealed class SubscriptionPlanRepository(PlatformDbContext dbContext) : ISubscriptionPlanRepository
{
  public async Task<SubscriptionPlan?> GetByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
  {
    return await dbContext.Set<SubscriptionPlan>()
      .SingleOrDefaultAsync(p => p.Id == subscriptionPlanId, cancellationToken);
  }

  public async Task<bool> NormalizedCodeExistsAsync(string normalizedPlanCode, CancellationToken cancellationToken = default)
  {
    return await dbContext.Set<SubscriptionPlan>()
      .AnyAsync(p => p.NormalizedPlanCode == normalizedPlanCode, cancellationToken);
  }

  public async Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
  {
    await dbContext.Set<SubscriptionPlan>().AddAsync(plan, cancellationToken);
  }
}
