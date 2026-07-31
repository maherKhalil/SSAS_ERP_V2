using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.TenantUsers;

public sealed class CreateTenantUserMembershipCommandHandler(
  IIdentityRepository identityRepository,
  ITenantUserRepository tenantUserRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<long>> HandleAsync(CreateTenantUserMembershipCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<long>(execution.Error);
    }

    var email = EmailAddress.Create(command.Email);
    var displayName = UserDisplayName.Create(command.DisplayName);
    if (email.IsFailure || displayName.IsFailure)
    {
      return Result.Failure<long>(email.IsFailure ? email.Error : displayName.Error);
    }

    if (await identityRepository.GetByIdAsync(command.IdentityId, cancellationToken) is null)
    {
      return Result.Failure<long>(IdentityAccessErrors.NotFound);
    }

    if (await tenantUserRepository.MembershipExistsAsync(command.IdentityId, cancellationToken))
    {
      return Result.Failure<long>(new Error("TenantUser.MembershipExists", "The identity already has a membership in this tenant."));
    }

    if (await tenantUserRepository.EmailExistsAsync(email.Value.NormalizedEmail, cancellationToken: cancellationToken))
    {
      return Result.Failure<long>(new Error("TenantUser.EmailExists", "The email already exists in this tenant."));
    }

    var tenantUser = TenantUser.CreateActive(
      command.IdentityId,
      execution.Value.TenantId,
      email.Value,
      displayName.Value,
      Guid.NewGuid(),
      clock.UtcNow);
    await tenantUserRepository.AddAsync(tenantUser, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saveResult.IsFailure ? Result.Failure<long>(saveResult.Error) : Result.Success(tenantUser.Id);
  }
}
