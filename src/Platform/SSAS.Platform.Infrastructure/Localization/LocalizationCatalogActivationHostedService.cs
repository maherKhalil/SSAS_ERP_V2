using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationCatalogActivationHostedService(
  IServiceScopeFactory scopeFactory,
  IConfiguration configuration,
  IHostEnvironment environment,
  ILogger<LocalizationCatalogActivationHostedService> logger) : IHostedService
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
    var activation = scope.ServiceProvider.GetRequiredService<ILocalizationCatalogActivationService>();
    var result = await activation.ActivateAsync(environment.IsProduction(), cancellationToken);
    if (result.Outcome == LocalizationCatalogActivationOutcome.DevelopmentLowerVersionWarning)
    {
      LogDevelopmentLowerVersion(logger, result.LocalCatalogVersion, result.HighestActivatedCatalogVersion, null);
    }
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  private static readonly Action<ILogger, long, long, Exception?> LogDevelopmentLowerVersion =
    LoggerMessage.Define<long, long>(
      LogLevel.Warning,
      new EventId(4101, nameof(LocalizationCatalogActivationHostedService)),
      "Development localization catalog version {LocalCatalogVersion} is below activated database version {HighestActivatedCatalogVersion}; the database value was not lowered.");

  private static readonly Action<ILogger, Exception?> LogDevelopmentMissingConnection =
    LoggerMessage.Define(
      LogLevel.Warning,
      new EventId(4102, nameof(LocalizationCatalogActivationHostedService)),
      "Development localization catalog preflight was skipped because the Platform connection string is not configured.");
}
