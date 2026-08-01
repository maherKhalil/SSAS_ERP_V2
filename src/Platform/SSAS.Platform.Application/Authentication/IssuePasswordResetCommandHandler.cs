using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Authentication;

public sealed class IssuePasswordResetCommandHandler(
  IAuthenticationAccountRepository authenticationAccountRepository,
  IAccountActionTokenRepository actionTokenRepository,
  IPlatformUnitOfWork unitOfWork,
  IActionTokenService actionTokenService,
  AuthenticationPolicy policy,
  IDateTimeProvider clock)
{
  public async Task<Result<PasswordResetIssuanceResult>> HandleAsync(
    IssuePasswordResetCommand command,
    CancellationToken cancellationToken = default)
  {
    var loginEmail = LoginEmail.Create(command.LoginEmail);
    if (loginEmail.IsFailure)
    {
      return Result.Success(PasswordResetIssuanceResult.Accepted());
    }

    var account = await authenticationAccountRepository.GetByNormalizedLoginEmailAsync(
      loginEmail.Value.NormalizedValue,
      cancellationToken);
    if (account is null || !account.CanIssuePasswordReset)
    {
      return Result.Success(PasswordResetIssuanceResult.Accepted());
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var now = clock.UtcNow;
    var priorToken = await actionTokenRepository.GetActivePasswordResetAsync(account.Id, now, cancellationToken);
    if (priorToken is not null)
    {
      var revokeResult = priorToken.Revoke(null, "Replaced", Guid.NewGuid(), now);
      if (revokeResult.IsFailure)
      {
        return Result.Failure<PasswordResetIssuanceResult>(revokeResult.Error);
      }
    }

    var generated = actionTokenService.Generate(AccountActionTokenPurpose.PasswordReset);
    var actionToken = AccountActionToken.CreatePasswordReset(
      generated.PublicId,
      generated.SecretHash,
      account.IdentityId,
      account.Id,
      now,
      now.Add(policy.PasswordResetLifetime),
      Guid.NewGuid());
    await actionTokenRepository.AddAsync(actionToken, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return Result.Failure<PasswordResetIssuanceResult>(saveResult.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(PasswordResetIssuanceResult.Accepted(generated.SensitiveToken));
  }
}
