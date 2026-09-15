using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;
using SSAS.Platform.Application.Subscriptions.Plans;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

internal sealed class PlanQueries(PlatformDbContext dbContext) : IPlanQueries
{
  public async Task<Result<IEnumerable<PlanDto>>> GetPlansAsync(CancellationToken cancellationToken = default)
  {
    var plans = await dbContext.Set<SubscriptionPlan>()
      .AsNoTracking()
      .Include(p => p.ModuleGrants)
      .Include(p => p.Limits)
      .Include(p => p.Prices)
      .OrderBy(p => p.NormalizedPlanCode)
      .ToListAsync(cancellationToken);

    return Result.Success<IEnumerable<PlanDto>>(plans.Select(Map).ToList());
  }

  public async Task<Result<PlanDto>> GetPlanByIdAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
  {
    var plan = await dbContext.Set<SubscriptionPlan>()
      .AsNoTracking()
      .Include(p => p.ModuleGrants)
      .Include(p => p.Limits)
      .Include(p => p.Prices)
      .SingleOrDefaultAsync(p => p.Id == subscriptionPlanId, cancellationToken);

    if (plan is null) return Result.Failure<PlanDto>(SubscriptionErrors.InvalidPlanCode); // Or PlanNotFound

    return Result.Success(Map(plan));
  }

  private static PlanDto Map(SubscriptionPlan plan) => new(
    plan.SubscriptionPlanId,
    plan.PlanCode.Value,
    plan.PlanName.Value,
    plan.Status,
    plan.ModuleGrants.Select(g => new PlanModuleGrantDto(g.ModuleKey.Value)).ToList(),
    plan.Limits.Select(l => new PlanLimitDto(l.LimitKey, l.LimitValue)).ToList(),
    plan.Prices.Select(p => new PlanPriceDto(p.CurrencyCode, p.BillingPeriod, p.Amount)).ToList()
  );
}
