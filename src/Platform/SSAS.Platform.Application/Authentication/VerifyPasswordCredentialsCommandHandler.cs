using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Authentication;

public sealed class VerifyPasswordCredentialsCommandHandler(
  IAuthenticationAccountRepository authenticationAccountRepository,
  IPlatformUnitOfWork unitOfWork,
  IPasswordHashingService passwordHashingService,
  IAuthenticationDiagnostics diagnostics,
  AuthenticationPolicy policy,
  IDateTimeProvider clock)
{
  public async Task<Result<CredentialVerificationResult>> HandleAsync(
    VerifyPasswordCredentialsCommand command,
    CancellationToken cancellationToken = default)
  {
    var providedPassword = command.Password ?? string.Empty;
    var loginEmail = LoginEmail.Create(command.LoginEmail);
    if (loginEmail.IsFailure)
    {
      passwordHashingService.PerformDummyVerification(providedPassword);
      return GenericFailure();
    }

    var account = await authenticationAccountRepository.GetByNormalizedLoginEmailAsync(
      loginEmail.Value.NormalizedValue,
      cancellationToken);
    var now = clock.UtcNow;
    if (account is null)
    {
      passwordHashingService.PerformDummyVerification(providedPassword);
      return GenericFailure();
    }

    if (!account.CanVerifyPassword(now) || account.PasswordHash is null)
    {
      if (account.PasswordHash is { } existingHash)
      {
        _ = passwordHashingService.VerifyPassword(existingHash, providedPassword);
      }
      else
      {
        passwordHashingService.PerformDummyVerification(providedPassword);
      }

      return GenericFailure();
    }

    var verification = passwordHashingService.VerifyPassword(account.PasswordHash, providedPassword);
    if (verification == PasswordVerificationOutcome.Failed)
    {
      await PersistFailedAttemptAsync(account, cancellationToken);
      return GenericFailure();
    }

    account.RecordSuccessfulVerification();
    if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
    {
      var rehashResult = account.ReplaceHashAfterSuccessfulVerification(
        passwordHashingService.HashPassword(providedPassword),
        Guid.NewGuid(),
        now);
      if (rehashResult.IsFailure)
      {
        return GenericFailure();
      }
    }

    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saveResult.IsFailure
      ? GenericFailure()
      : Result.Success(new CredentialVerificationResult(new VerifiedIdentity(account.IdentityId, account.SecurityVersion)));
  }

  private async Task PersistFailedAttemptAsync(AuthenticationAccount account, CancellationToken cancellationToken)
  {
    for (var retry = 0; retry <= policy.FailedAttemptConcurrencyRetries; retry++)
    {
      var failureResult = account.RecordFailedAttempt(
        policy.FailedAttemptThreshold,
        policy.LockoutDuration,
        Guid.NewGuid(),
        clock.UtcNow);
      if (failureResult.IsFailure)
      {
        return;
      }

      var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (saveResult.IsSuccess || saveResult.Error != IdentityAccessErrors.ConcurrencyConflict)
      {
        return;
      }

      account.ClearDomainEvents();
      if (retry == policy.FailedAttemptConcurrencyRetries)
      {
        diagnostics.FailedAttemptConcurrencyRetriesExhausted();
        return;
      }

      diagnostics.FailedAttemptConcurrencyRetry(retry + 1);
      await authenticationAccountRepository.ReloadAsync(account, cancellationToken);
    }
  }

  private static Result<CredentialVerificationResult> GenericFailure() =>
    Result.Failure<CredentialVerificationResult>(AuthenticationErrors.GenericCredentialFailure);
}
