using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class GetSubscriptionPlansQueryHandler(IPlanQueries queries)
{
  public async Task<Result<IEnumerable<PlanDto>>> HandleAsync(GetSubscriptionPlansQuery query, CancellationToken cancellationToken = default)
  {
    return await queries.GetPlansAsync(cancellationToken);
  }
}
