using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.TenantUsers;

public sealed class UpdateTenantUserProfileCommandHandler(
  ITenantUserRepository tenantUserRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(UpdateTenantUserProfileCommand command, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure(execution.Error);
    }

    var tenantUser = await tenantUserRepository.GetByIdAsync(command.TenantUserId, cancellationToken);
    if (tenantUser is null)
    {
      return Result.Failure(IdentityAccessErrors.NotFound);
    }

    if (!ApplicationExecutionContext.MatchesExpectedVersion(tenantUser.RowVersion, command.ExpectedRowVersion))
    {
      return Result.Failure(IdentityAccessErrors.ConcurrencyConflict);
    }

    var email = EmailAddress.Create(command.Email);
    var displayName = UserDisplayName.Create(command.DisplayName);
    if (email.IsFailure || displayName.IsFailure)
    {
      return Result.Failure(email.IsFailure ? email.Error : displayName.Error);
    }

    if (await tenantUserRepository.EmailExistsAsync(email.Value.NormalizedEmail, tenantUser.Id, cancellationToken))
    {
      return Result.Failure(new Error("TenantUser.EmailExists", "The email already exists in this tenant."));
    }

    var updateResult = tenantUser.UpdateProfile(email.Value, displayName.Value);
    return updateResult.IsFailure ? updateResult : await PersistenceResult.SaveAsync(unitOfWork, cancellationToken);
  }
}
