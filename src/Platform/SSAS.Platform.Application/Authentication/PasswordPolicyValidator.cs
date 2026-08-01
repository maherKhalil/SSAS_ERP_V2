using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Authentication;

public sealed class PasswordPolicyValidator(
  AuthenticationPolicy policy,
  ICompromisedPasswordChecker compromisedPasswordChecker) : IPasswordPolicyValidator
{
  public async Task<Result> ValidateAsync(string password, CancellationToken cancellationToken = default)
  {
    if (password is null || password.Length < policy.MinimumPasswordLength || password.Length > policy.MaximumPasswordLength)
    {
      return Result.Failure(AuthenticationErrors.InvalidPassword);
    }

    var compromisedResult = await compromisedPasswordChecker.CheckAsync(password, cancellationToken);
    return compromisedResult switch
    {
      CompromisedPasswordCheckOutcome.Safe => Result.Success(),
      CompromisedPasswordCheckOutcome.Compromised => Result.Failure(AuthenticationErrors.CompromisedPassword),
      _ => Result.Failure(AuthenticationErrors.CompromisedPasswordCheckUnavailable)
    };
  }
}
