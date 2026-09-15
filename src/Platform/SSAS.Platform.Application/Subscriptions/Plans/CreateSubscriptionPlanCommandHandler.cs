using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class CreateSubscriptionPlanCommandHandler(
  ISubscriptionPlanRepository planRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(CreateSubscriptionPlanCommand command, CancellationToken cancellationToken = default)
  {
    var code = PlanCode.Create(command.PlanCode);
    if (code.IsFailure) return Result.Failure<Guid>(code.Error);

    var name = PlanName.Create(command.PlanName);
    if (name.IsFailure) return Result.Failure<Guid>(name.Error);

    if (await planRepository.NormalizedCodeExistsAsync(code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(SubscriptionErrors.InvalidPlanCode);
    }

    var plan = SubscriptionPlan.Create(code.Value, name.Value, (currentUser.UserId ?? string.Empty), clock.UtcNow);
    if (plan.IsFailure) return Result.Failure<Guid>(plan.Error);

    await planRepository.AddAsync(plan.Value, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure) return Result.Failure<Guid>(saveResult.Error);

    return Result.Success(plan.Value.SubscriptionPlanId);
  }
}
