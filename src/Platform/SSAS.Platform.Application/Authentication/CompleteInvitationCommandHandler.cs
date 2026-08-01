using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public sealed class CompleteInvitationCommandHandler(
  IAuthenticationAccountRepository authenticationAccountRepository,
  IAccountActionTokenRepository actionTokenRepository,
  ITenantUserRepository tenantUserRepository,
  IPlatformUnitOfWork unitOfWork,
  IActionTokenService actionTokenService,
  IPasswordHashingService passwordHashingService,
  IPasswordPolicyValidator passwordPolicyValidator,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(CompleteInvitationCommand command, CancellationToken cancellationToken = default)
  {
    if (!actionTokenService.TryReadPublicId(command.ActionToken, out var publicId))
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var actionToken = await actionTokenRepository.GetByPublicIdAsync(publicId, cancellationToken);
    var now = clock.UtcNow;
    if (actionToken is null ||
      actionToken.ValidateForUse(AccountActionTokenPurpose.Invitation, now).IsFailure ||
      !actionTokenService.Verify(actionToken, command.ActionToken) ||
      actionToken.TenantId is not { } tenantId ||
      actionToken.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var account = await authenticationAccountRepository.GetByIdAsync(actionToken.AuthenticationAccountId, cancellationToken);
    var tenantUser = await tenantUserRepository.GetByTrustedInvitationBindingAsync(tenantId, tenantUserId, cancellationToken);
    if (account is null || tenantUser is null || account.IdentityId != actionToken.IdentityId || tenantUser.IdentityId != actionToken.IdentityId ||
      tenantUser.Status != TenantUserStatus.Pending)
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    if (account.Status == AuthenticationAccountStatus.PendingSetup)
    {
      if (command.InitialPassword is null)
      {
        return Result.Failure(AuthenticationErrors.PasswordRequired);
      }

      var policyResult = await passwordPolicyValidator.ValidateAsync(command.InitialPassword, cancellationToken);
      if (policyResult.IsFailure)
      {
        return policyResult;
      }

      var setupResult = account.CompleteInitialSetup(
        passwordHashingService.HashPassword(command.InitialPassword),
        Guid.NewGuid(),
        now);
      if (setupResult.IsFailure)
      {
        return setupResult;
      }
    }
    else if (account.Status == AuthenticationAccountStatus.Active)
    {
      if (command.InitialPassword is not null || !account.EmailVerifiedUtc.HasValue || !account.HasPassword)
      {
        return Result.Failure(command.InitialPassword is not null
          ? AuthenticationErrors.PasswordNotAllowed
          : AuthenticationErrors.InvalidActionToken);
      }
    }
    else
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var activationResult = tenantUser.Activate(Guid.NewGuid(), now);
    if (activationResult.IsFailure)
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var consumeResult = actionToken.Consume(Guid.NewGuid(), now);
    if (consumeResult.IsFailure)
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return Result.Failure(saveResult.Error == IdentityAccessErrors.ConcurrencyConflict
        ? AuthenticationErrors.InvalidActionToken
        : saveResult.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success();
  }
}
