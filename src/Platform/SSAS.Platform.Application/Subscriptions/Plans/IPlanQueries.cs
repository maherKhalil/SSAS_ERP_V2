using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public interface IPlanQueries
{
  Task<Result<IEnumerable<PlanDto>>> GetPlansAsync(CancellationToken cancellationToken = default);
  Task<Result<PlanDto>> GetPlanByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default);
}
