using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.PlatformSupport;

// Startup runner for genesis/recovery bootstrap (ADR-016 / DEC-TEN-0019), mirroring the localization
// catalog activation hosted service. It resolves a scope and delegates one convergence pass to the
// bootstrap orchestrator. In non-Production without a configured Platform connection string it is a no-op,
// so test hosts and connectionless dev hosts never touch the database on startup.
public sealed class PlatformSupportBootstrapHostedService(
  IServiceScopeFactory scopeFactory,
  IConfiguration configuration,
  IHostEnvironment environment,
  ILogger<PlatformSupportBootstrapHostedService> logger) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    if (!environment.IsProduction() &&
      string.IsNullOrWhiteSpace(configuration.GetConnectionString(PlatformPersistenceConstants.ConnectionStringName)))
    {
      LogDevelopmentMissingConnection(logger, null);
      return;
    }

    await using var scope = scopeFactory.CreateAsyncScope();
    var bootstrap = scope.ServiceProvider.GetRequiredService<IPlatformSupportBootstrapService>();
    var outcome = await bootstrap.RunAsync(cancellationToken);
    LogBootstrapOutcome(logger, outcome.ToString(), null);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  private static readonly Action<ILogger, string, Exception?> LogBootstrapOutcome =
    LoggerMessage.Define<string>(
      LogLevel.Information,
      new EventId(4201, nameof(PlatformSupportBootstrapHostedService)),
      "Platform-support bootstrap completed with outcome {Outcome}.");

  private static readonly Action<ILogger, Exception?> LogDevelopmentMissingConnection =
    LoggerMessage.Define(
      LogLevel.Warning,
      new EventId(4202, nameof(PlatformSupportBootstrapHostedService)),
      "Development platform-support bootstrap was skipped because the Platform connection string is not configured.");
}
