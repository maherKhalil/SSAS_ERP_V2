using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public sealed class CompletePasswordResetCommandHandler(
  IAuthenticationAccountRepository authenticationAccountRepository,
  IAccountActionTokenRepository actionTokenRepository,
  IPlatformUnitOfWork unitOfWork,
  IActionTokenService actionTokenService,
  IPasswordHashingService passwordHashingService,
  IPasswordPolicyValidator passwordPolicyValidator,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(CompletePasswordResetCommand command, CancellationToken cancellationToken = default)
  {
    if (!actionTokenService.TryReadPublicId(command.ActionToken, out var publicId))
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var actionToken = await actionTokenRepository.GetByPublicIdAsync(publicId, cancellationToken);
    var now = clock.UtcNow;
    if (actionToken is null ||
      actionToken.ValidateForUse(AccountActionTokenPurpose.PasswordReset, now).IsFailure ||
      !actionTokenService.Verify(actionToken, command.ActionToken))
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var account = await authenticationAccountRepository.GetByIdAsync(actionToken.AuthenticationAccountId, cancellationToken);
    if (account is null || account.IdentityId != actionToken.IdentityId || !account.CanIssuePasswordReset)
    {
      return Result.Failure(AuthenticationErrors.InvalidActionToken);
    }

    var policyResult = await passwordPolicyValidator.ValidateAsync(command.NewPassword, cancellationToken);
    if (policyResult.IsFailure)
    {
      return policyResult;
    }

    var resetResult = account.ResetPassword(
      passwordHashingService.HashPassword(command.NewPassword),
      Guid.NewGuid(),
      now);
    var consumeResult = actionToken.Consume(Guid.NewGuid(), now);
    if (resetResult.IsFailure || consumeResult.IsFailure)
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
