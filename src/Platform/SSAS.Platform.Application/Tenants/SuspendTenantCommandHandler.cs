using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Tenants;

public sealed class SuspendTenantCommandHandler(
  ITenantRepository tenantRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(SuspendTenantCommand command, CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure(actor.Error);
    }

    var tenant = await tenantRepository.GetByIdAsync(command.TenantId, cancellationToken);
    if (tenant is null)
    {
      return Result.Failure(TenantLifecycleErrors.NotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(tenant.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var transition = tenant.Suspend(command.Reason, actor.Value, Guid.NewGuid(), clock.UtcNow);
    return transition.IsFailure ? transition : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
