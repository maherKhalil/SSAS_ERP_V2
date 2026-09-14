using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class SetPlanPricesCommandHandler(
  ISubscriptionPlanRepository planRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(SetPlanPricesCommand command, CancellationToken cancellationToken = default)
  {
    var plan = await planRepository.GetByIdAsync(command.SubscriptionPlanId, cancellationToken);
    if (plan is null) return Result.Failure(SubscriptionErrors.InvalidPlanCode);

    var prices = command.Prices?.Select(p => (p.CurrencyCode, p.BillingPeriod, p.Amount)).ToList() ?? [];

    var result = plan.ReplacePrices(prices, (currentUser.UserId ?? string.Empty), clock.UtcNow);
    if (result.IsFailure) return result;

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
