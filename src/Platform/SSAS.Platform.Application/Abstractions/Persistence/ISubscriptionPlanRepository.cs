using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ISubscriptionPlanRepository
{
  Task<SubscriptionPlan?> GetByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default);
  Task<bool> NormalizedCodeExistsAsync(string normalizedPlanCode, CancellationToken cancellationToken = default);
  Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
}
