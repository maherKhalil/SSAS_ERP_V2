using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantLocalizationOverrideRepository(PlatformDbContext dbContext)
  : ITenantLocalizationOverrideRepository
{
  public Task<TenantLocalizationOverride?> GetForUpdateAsync(
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantLocalizationOverrides
      .FromSqlInterpolated($"SELECT * FROM [platform].[TenantLocalizationOverrides] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantId] = {tenantId} AND [ResourceKey] = {resourceKey.Value} AND [Culture] = {culture.Value}")
      .SingleOrDefaultAsync(cancellationToken);

  public async Task<LocalizationVersionSnapshot?> GetVersionSnapshotAsync(
    Guid overrideId,
    TenantOverrideVersion versionNumber,
    CancellationToken cancellationToken = default)
  {
    var version = await dbContext.TenantLocalizationOverrideVersions.AsNoTracking()
      .SingleOrDefaultAsync(candidate =>
        candidate.TenantLocalizationOverrideId == overrideId && candidate.VersionNumber == versionNumber,
        cancellationToken);
    return version?.ToSnapshot();
  }

  public async Task AddAsync(
    TenantLocalizationOverride localizationOverride,
    CancellationToken cancellationToken = default) =>
    await dbContext.TenantLocalizationOverrides.AddAsync(localizationOverride, cancellationToken);
}
