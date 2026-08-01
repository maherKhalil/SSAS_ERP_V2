using Microsoft.Extensions.Logging;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AuthenticationDiagnostics(ILogger<AuthenticationDiagnostics> logger) : IAuthenticationDiagnostics
{
  private static readonly Action<ILogger, int, Exception?> RetryMessage = LoggerMessage.Define<int>(
    LogLevel.Debug,
    new EventId(2101, nameof(FailedAttemptConcurrencyRetry)),
    "Retrying a concurrent failed-credential update. Retry {RetryNumber}.");
  private static readonly Action<ILogger, Exception?> RetriesExhaustedMessage = LoggerMessage.Define(
    LogLevel.Warning,
    new EventId(2102, nameof(FailedAttemptConcurrencyRetriesExhausted)),
    "Concurrent failed-credential update retries were exhausted.");

  public void FailedAttemptConcurrencyRetry(int retryNumber)
  {
    RetryMessage(logger, retryNumber, null);
  }

  public void FailedAttemptConcurrencyRetriesExhausted()
  {
    RetriesExhaustedMessage(logger, null);
  }
}
