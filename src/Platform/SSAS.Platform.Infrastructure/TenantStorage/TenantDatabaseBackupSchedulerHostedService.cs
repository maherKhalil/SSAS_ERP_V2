using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The fleet backup loop (ADR-022 §13, TS-Backup Phase C).
//
// The FIRST background loop in tenant storage. Migration orchestration is explicitly invoked; this one runs
// unattended, which is why almost everything interesting about it is a bound: how often, how many at once,
// how long before the first pass, and what happens when something throws.
//
// It owns repetition and nothing else. One sweep's worth of decisions lives in ITenantDatabaseBackupScheduler,
// so the scheduling rules can be tested without a host, a timer or a clock skew.
public sealed class TenantDatabaseBackupSchedulerHostedService(
  IServiceScopeFactory scopeFactory,
  TenantDatabaseBackupSchedulerOptions options,
  ILogger<TenantDatabaseBackupSchedulerHostedService> logger) : BackgroundService
{
  // Process-local health. Deliberately not persisted: it describes THIS instance's loop, and a row would
  // invite two instances to overwrite each other's idea of when the fleet was last swept.
  public DateTimeOffset? LastSuccessfulSweepUtc { get; private set; }

  public DateTimeOffset? LastSchedulerErrorUtc { get; private set; }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!options.Enabled)
    {
      // Registered but idle. Said once, at startup, so an operator wondering why no backups are happening
      // finds the answer in the log rather than in configuration archaeology.
      LogSchedulerDisabled(logger, null);
      return;
    }

    LogSchedulerStarted(
      logger, options.SweepInterval.TotalSeconds, options.MaxConcurrentBackups,
      options.MaxConcurrentPerServer, null);

    try
    {
      // Startup delay plus jitter. Several instances deployed together would otherwise begin their first
      // sweep in the same second and stay in lockstep indefinitely.
      await Task.Delay(WithJitter(options.StartupDelay), stoppingToken);

      while (!stoppingToken.IsCancellationRequested)
      {
        await RunOneSweepAsync(stoppingToken);
        await Task.Delay(WithJitter(options.SweepInterval), stoppingToken);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // Normal shutdown.
    }

    LogSchedulerStopped(logger, null);
  }

  private async Task RunOneSweepAsync(CancellationToken stoppingToken)
  {
    try
    {
      // A SCOPE PER SWEEP. The hosted service is a singleton; holding a DbContext across iterations would
      // accumulate tracked entities for the lifetime of the process and serve progressively staler reads.
      await using var scope = scopeFactory.CreateAsyncScope();
      var scheduler = scope.ServiceProvider.GetRequiredService<ITenantDatabaseBackupScheduler>();

      await scheduler.RunSweepAsync(stoppingToken);
      LastSuccessfulSweepUtc = DateTimeOffset.UtcNow;
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      throw;
    }
#pragma warning disable CA1031 // A failed sweep must not end the service.
    catch (Exception exception)
#pragma warning restore CA1031
    {
      // OUTER-LOOP RECOVERY. A sweep can fail for reasons that have nothing to do with any one database —
      // the Platform database being briefly unreachable, for instance. Ending the background service would
      // silently stop protecting the entire estate until someone restarted the host, which is a far worse
      // outcome than waiting one interval and trying again.
      LastSchedulerErrorUtc = DateTimeOffset.UtcNow;
      LogSweepFailed(logger, exception.GetType().Name, exception);
    }
  }

  // Bounded, non-negative, and applied to both the startup delay and every interval.
  private TimeSpan WithJitter(TimeSpan interval)
  {
    if (options.MaximumJitter <= TimeSpan.Zero)
    {
      return interval;
    }

    var jitterMilliseconds = Random.Shared.NextDouble() * options.MaximumJitter.TotalMilliseconds;
    return interval + TimeSpan.FromMilliseconds(jitterMilliseconds);
  }

  private static readonly Action<ILogger, Exception?> LogSchedulerDisabled =
    LoggerMessage.Define(
      LogLevel.Information,
      new EventId(4315, nameof(LogSchedulerDisabled)),
      "Tenant backup fleet scheduler is disabled; no backups will be scheduled by this instance.");

  private static readonly Action<ILogger, double, int, int, Exception?> LogSchedulerStarted =
    LoggerMessage.Define<double, int, int>(
      LogLevel.Information,
      new EventId(4316, nameof(LogSchedulerStarted)),
      "Tenant backup fleet scheduler started: sweeping every {SweepIntervalSeconds}s, " +
      "at most {MaxConcurrentBackups} concurrent backups and {MaxConcurrentPerServer} per server.");

  private static readonly Action<ILogger, string, Exception?> LogSweepFailed =
    LoggerMessage.Define<string>(
      LogLevel.Error,
      new EventId(4317, nameof(LogSweepFailed)),
      "Tenant backup sweep failed with {ExceptionType}; the scheduler will retry on the next interval.");

  private static readonly Action<ILogger, Exception?> LogSchedulerStopped =
    LoggerMessage.Define(
      LogLevel.Information,
      new EventId(4318, nameof(LogSchedulerStopped)),
      "Tenant backup fleet scheduler stopped.");
}
