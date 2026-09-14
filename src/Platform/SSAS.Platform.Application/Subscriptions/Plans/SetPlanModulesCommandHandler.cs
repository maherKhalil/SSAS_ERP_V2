using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Subscriptions.Plans;

public sealed class SetPlanModulesCommandHandler(
  ISubscriptionPlanRepository planRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(SetPlanModulesCommand command, CancellationToken cancellationToken = default)
  {
    var plan = await planRepository.GetByIdAsync(command.SubscriptionPlanId, cancellationToken);
    if (plan is null) return Result.Failure(SubscriptionErrors.InvalidPlanCode);

    var modules = new List<ModuleKey>();
    foreach(var key in command.Modules ?? [])
    {
      var mk = ModuleKey.Create(key);
      if (mk.IsFailure) return mk;
      modules.Add(mk.Value);
    }

    var result = plan.ReplaceModules(modules, (currentUser.UserId ?? string.Empty), clock.UtcNow);
    if (result.IsFailure) return result;

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
