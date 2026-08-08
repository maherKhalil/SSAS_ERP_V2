using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantLocalizationSettingsRepository(PlatformDbContext dbContext)
  : ITenantLocalizationSettingsRepository
{
  private const string InitializationSavepoint = "LocalizationSettingsInit";

  public Task<TenantLocalizationSettings?> GetForUpdateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantLocalizationSettings
      .FromSqlInterpolated($"SELECT * FROM [platform].[TenantLocalizationSettings] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId}")
      .SingleOrDefaultAsync(cancellationToken);

  public async Task<TenantLocalizationSettings> GetOrCreateForUpdateAsync(
    Guid tenantId,
    LocalizationCulture defaultCulture,
    CancellationToken cancellationToken = default)
  {
    var existing = await GetForUpdateAsync(tenantId, cancellationToken);
    if (existing is not null)
    {
      return existing;
    }

    var transaction = dbContext.Database.CurrentTransaction ??
      throw new InvalidOperationException("Localization settings initialization requires an active transaction.");
    await transaction.CreateSavepointAsync(InitializationSavepoint, cancellationToken);
    var settings = TenantLocalizationSettings.Create(tenantId, defaultCulture);
    await dbContext.TenantLocalizationSettings.AddAsync(settings, cancellationToken);
    try
    {
      await dbContext.SaveChangesAsync(cancellationToken);
      await transaction.ReleaseSavepointAsync(InitializationSavepoint, cancellationToken);
      return settings;
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
    {
      await transaction.RollbackToSavepointAsync(InitializationSavepoint, cancellationToken);
      dbContext.Entry(settings).State = EntityState.Detached;
      var winner = await GetForUpdateAsync(tenantId, cancellationToken);
      return winner ?? throw new InvalidOperationException("The winning localization settings row could not be reloaded.", exception);
    }
  }
}
