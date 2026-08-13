using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Startup runner for the tenant-storage registry baseline (ADR-017, TS-1B), mirroring the platform-support
// and localization bootstrap hosted services. It resolves a scope and delegates one convergence pass.
//
// In non-Production without a configured Platform connection string it is a no-op, so test hosts and
// connectionless dev hosts never touch the database on startup.
//
// Failures propagate: an unusable registry baseline stops the host rather than leaving routing metadata
// half-established. Nothing consumes the registry yet, so this is deliberately conservative — matching the
// existing critical bootstrap services rather than swallowing database exceptions.
public sealed class TenantStorageBootstrapHostedService(
  IServiceScopeFactory scopeFactory,
  IConfiguration configuration,
  IHostEnvironment environment,
  ILogger<TenantStorageBootstrapHostedService> logger) : IHostedService
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
    var bootstrap = scope.ServiceProvider.GetRequiredService<ITenantStorageBootstrapService>();
    var outcome = await bootstrap.RunAsync(cancellationToken);
    LogBootstrapOutcome(
      logger, outcome.TenantDatabaseId, outcome.TenantDatabaseCreated, outcome.AssignmentsCreated, outcome.TenantsAlreadyAssigned, null);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  private static readonly Action<ILogger, long, bool, int, int, Exception?> LogBootstrapOutcome =
    LoggerMessage.Define<long, bool, int, int>(
      LogLevel.Information,
      new EventId(4301, nameof(TenantStorageBootstrapHostedService)),
      "Tenant storage bootstrap completed for tenant database {TenantDatabaseId} (created {TenantDatabaseCreated}); " +
      "{AssignmentsCreated} assignments created, {TenantsAlreadyAssigned} tenants already assigned.");

  private static readonly Action<ILogger, Exception?> LogDevelopmentMissingConnection =
    LoggerMessage.Define(
      LogLevel.Warning,
      new EventId(4302, nameof(TenantStorageBootstrapHostedService)),
      "Development tenant storage bootstrap was skipped because the Platform connection string is not configured.");
}
