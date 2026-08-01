using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Tenants;

public sealed class CreateTenantCommandHandler(
  ITenantRepository tenantRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<Guid>(actor.Error);
    }

    var code = TenantCode.Create(command.TenantCode);
    if (code.IsFailure)
    {
      return Result.Failure<Guid>(code.Error);
    }

    var name = TenantName.Create(command.TenantName);
    if (name.IsFailure)
    {
      return Result.Failure<Guid>(name.Error);
    }

    if (await tenantRepository.NormalizedCodeExistsAsync(code.Value.NormalizedValue, cancellationToken))
    {
      return Result.Failure<Guid>(TenantLifecycleErrors.CodeExists);
    }

    var tenant = Tenant.Create(code.Value, name.Value, actor.Value, Guid.NewGuid(), clock.UtcNow);
    if (tenant.IsFailure)
    {
      return Result.Failure<Guid>(tenant.Error);
    }

    await tenantRepository.AddAsync(tenant.Value, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return saveResult.Error == IdentityAccessErrors.UniqueConstraintViolation
        ? Result.Failure<Guid>(TenantLifecycleErrors.CodeExists)
        : Result.Failure<Guid>(saveResult.Error);
    }

    return Result.Success(tenant.Value.TenantId);
  }
}
