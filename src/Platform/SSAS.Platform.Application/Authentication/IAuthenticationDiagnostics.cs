namespace SSAS.Platform.Application.Authentication;

public interface IAuthenticationDiagnostics
{
  void FailedAttemptConcurrencyRetry(int retryNumber);

  void FailedAttemptConcurrencyRetriesExhausted();
}
