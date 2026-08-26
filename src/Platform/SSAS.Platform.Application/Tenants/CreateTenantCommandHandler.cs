using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Tenants;

// A TENANT IS CREATED WITH ITS TRIAL, IN ONE TRANSACTION (FP-014, `DEC-L-034`, T-041).
//
// T-040 made the entitlement resolver real: **a tenant holding no subscription record reaches no gated
// module.** So creating a tenant and issuing its subscription are not two steps that usually both happen —
// a tenant created without one is locked out of the entire product, and `DEC-L-033` bounds that to modules
// only because the platform plane stays reachable, not because it is harmless.
//
// The issuer therefore adds to this unit of work and the save below commits both. There is no window
// between the two, no compensating write and no background job that could be behind: **the state "tenant
// exists, trial does not" is unrepresentable rather than unlikely.**
public sealed class CreateTenantCommandHandler(
  ITenantRepository tenantRepository,
  ITrialSubscriptionIssuer trialSubscriptionIssuer,
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

    // Issued before the save, into the same unit of work. A failure here abandons the tenant too, which is
    // the correct outcome: a tenant that cannot be given its trial is one that would be created locked out.
    var trial = await trialSubscriptionIssuer.IssueAsync(tenant.Value.TenantId, cancellationToken);
    if (trial.IsFailure)
    {
      return Result.Failure<Guid>(trial.Error);
    }

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
