using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationCatalogActivationService(
  PlatformDbContext dbContext,
  ILocalizationCatalog localCatalog) : ILocalizationCatalogActivationService
{
  public async Task<LocalizationCatalogActivationResult> ActivateAsync(
    bool isProduction,
    CancellationToken cancellationToken = default)
  {
    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    var state = await dbContext.LocalizationCatalogStates
      .FromSqlInterpolated($"SELECT * FROM [platform].[LocalizationCatalogStates] WITH (UPDLOCK, HOLDLOCK) WHERE [LocalizationCatalogStateId] = {LocalizationCatalogState.SingletonId}")
      .SingleOrDefaultAsync(cancellationToken) ??
      throw new LocalizationCatalogActivationException("The localization catalog-state singleton is missing. Apply the reviewed Platform migration before startup.");
    var localVersion = localCatalog.CatalogVersion.Value;
    var storedVersion = state.HighestActivatedCatalogVersion.Value;
    if (localVersion < storedVersion)
    {
      if (isProduction)
      {
        throw new LocalizationCatalogActivationException(
          $"Local localization CatalogVersion {localVersion} is lower than the highest activated version {storedVersion}.");
      }

      await transaction.RollbackAsync(cancellationToken);
      return new LocalizationCatalogActivationResult(
        LocalizationCatalogActivationOutcome.DevelopmentLowerVersionWarning,
        localVersion,
        storedVersion);
    }

    if (localVersion == storedVersion)
    {
      await transaction.CommitAsync(cancellationToken);
      return new LocalizationCatalogActivationResult(
        LocalizationCatalogActivationOutcome.Equal,
        localVersion,
        storedVersion);
    }

    state.Activate(localCatalog.CatalogSchemaVersion, localCatalog.CatalogVersion);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new LocalizationCatalogActivationResult(
      LocalizationCatalogActivationOutcome.Activated,
      localVersion,
      localVersion);
  }
}
