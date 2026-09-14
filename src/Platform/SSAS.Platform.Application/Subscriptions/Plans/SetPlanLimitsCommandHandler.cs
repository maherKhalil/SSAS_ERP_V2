using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class SetPlanLimitsCommandHandler(
  ISubscriptionPlanRepository planRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(SetPlanLimitsCommand command, CancellationToken cancellationToken = default)
  {
    var plan = await planRepository.GetByIdAsync(command.SubscriptionPlanId, cancellationToken);
    if (plan is null) return Result.Failure(SubscriptionErrors.InvalidPlanCode);

    var limits = command.Limits?.Select(l => (l.LimitKey, l.LimitValue)).ToList() ?? [];

    var result = plan.ReplaceLimits(limits, (currentUser.UserId ?? string.Empty), clock.UtcNow);
    if (result.IsFailure) return result;

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
