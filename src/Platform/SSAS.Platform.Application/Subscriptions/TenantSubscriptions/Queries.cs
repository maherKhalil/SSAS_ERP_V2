using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.TenantSubscriptions;

public record GetTenantSubscriptionsQuery(Guid TenantId);
public record GetCurrentTenantSubscriptionQuery(Guid TenantId, DateTimeOffset? AsOfUtc);
public record GetAllSubscriptionsQuery();

public sealed class GetTenantSubscriptionsQueryHandler(ITenantSubscriptionQueries queries)
{
  public Task<Result<IEnumerable<TenantSubscriptionDto>>> HandleAsync(GetTenantSubscriptionsQuery query, CancellationToken cancellationToken)
  {
    return queries.GetTenantSubscriptionsAsync(query.TenantId, cancellationToken);
  }
}

public sealed class GetCurrentTenantSubscriptionQueryHandler(ITenantSubscriptionQueries queries)
{
  public Task<Result<TenantSubscriptionDto>> HandleAsync(GetCurrentTenantSubscriptionQuery query, CancellationToken cancellationToken)
  {
    return queries.GetCurrentTenantSubscriptionAsync(query.TenantId, query.AsOfUtc ?? DateTimeOffset.UtcNow, cancellationToken);
  }
}

public sealed class GetAllSubscriptionsQueryHandler(ITenantSubscriptionQueries queries)
{
  public Task<Result<IEnumerable<TenantSubscriptionDto>>> HandleAsync(GetAllSubscriptionsQuery query, CancellationToken cancellationToken)
  {
    return queries.GetAllSubscriptionsAsync(cancellationToken);
  }
}
