using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.TenantSubscriptions;

public interface ITenantSubscriptionQueries
{
  Task<Result<IEnumerable<TenantSubscriptionDto>>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
  Task<Result<TenantSubscriptionDto>> GetCurrentTenantSubscriptionAsync(Guid tenantId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);
  Task<Result<IEnumerable<TenantSubscriptionDto>>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default);
}
