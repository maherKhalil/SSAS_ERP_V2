using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class GetSubscriptionPlanByIdQueryHandler(IPlanQueries queries)
{
  public async Task<Result<PlanDto>> HandleAsync(GetSubscriptionPlanByIdQuery query, CancellationToken cancellationToken = default)
  {
    return await queries.GetPlanByIdAsync(query.SubscriptionPlanId, cancellationToken);
  }
}
