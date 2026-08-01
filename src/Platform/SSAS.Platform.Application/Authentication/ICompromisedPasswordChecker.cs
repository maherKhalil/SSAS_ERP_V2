namespace SSAS.Platform.Application.Authentication;

public interface ICompromisedPasswordChecker
{
  Task<CompromisedPasswordCheckOutcome> CheckAsync(string password, CancellationToken cancellationToken = default);
}
