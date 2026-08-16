using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Repetition only. All discovery, admission, reconciliation and dispatch decisions remain in the testable
// D8/D9 scheduler component.
public sealed class TenantDatabaseRestoreVerificationHostedService(
  IServiceScopeFactory scopeFactory,
  IOptions<TenantDatabaseRestoreVerificationOptions> options,
  ILogger<TenantDatabaseRestoreVerificationHostedService> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!options.Value.Enabled)
    {
      LogDisabled(logger, null);
      return;
    }

    try
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        await using var scope = scopeFactory.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<ITenantDatabaseRestoreVerificationScheduler>();
        await scheduler.RunSweepAsync(stoppingToken);
        await Task.Delay(options.Value.SchedulerSweepInterval, stoppingToken);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // Normal host shutdown. D9 stops new discovery while already-started D7 execution has its own safe
      // CancellationToken.None boundary.
    }
    catch (Exception exception)
    {
      LogFaulted(logger, exception.GetType().Name, exception);
    }
  }

  private static readonly Action<ILogger, Exception?> LogDisabled =
    LoggerMessage.Define(LogLevel.Information,
      new EventId(4355, nameof(LogDisabled)),
      "Tenant restore-verification fleet scheduler is disabled; no restore verifications will be scheduled.");

  private static readonly Action<ILogger, string, Exception?> LogFaulted =
    LoggerMessage.Define<string>(LogLevel.Error,
      new EventId(4356, nameof(LogFaulted)),
      "Tenant restore-verification hosted service faulted with {ExceptionType}.");
}
