using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class TenantLocalizationHistoryReadService(PlatformDbContext dbContext)
  : ITenantLocalizationHistoryReadService
{
  public async Task<LocalizationHistoryResult?> GetAsync(
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    int pageNumber = 1,
    int pageSize = 50,
    CancellationToken cancellationToken = default)
  {
    var current = await dbContext.TenantLocalizationOverrides.AsNoTracking()
      .Where(candidate =>
        candidate.TenantId == tenantId &&
        candidate.ResourceKey == resourceKey &&
        candidate.Culture == culture)
      .Select(candidate => new
      {
        candidate.Id,
        candidate.IsActive,
        candidate.CurrentVersionNumber,
        candidate.RowVersion
      })
      .SingleOrDefaultAsync(cancellationToken);
    if (current is null)
    {
      return null;
    }

    var query = dbContext.TenantLocalizationOverrideVersions.AsNoTracking()
      .Where(version => version.TenantId == tenantId && version.TenantLocalizationOverrideId == current.Id)
      .OrderByDescending(version => version.VersionNumber);
    var totalCount = await query.CountAsync(cancellationToken);
    var versions = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
      .Select(version => new
      {
        VersionNumber = version.VersionNumber.Value,
        version.Value,
        version.IsActive,
        version.ChangeType,
        PriorLogicalVersionNumber = version.PriorLogicalVersionNumber.HasValue
          ? version.PriorLogicalVersionNumber.Value.Value
          : (long?)null,
        UndoTargetVersionNumber = version.UndoTargetVersionNumber.HasValue
          ? version.UndoTargetVersionNumber.Value.Value
          : (long?)null,
        CatalogVersion = version.CatalogVersion.Value,
        ResourceVersion = version.ResourceVersion.Value,
        version.ActorId,
        version.OccurredUtc
      })
      .ToArrayAsync(cancellationToken);
    var entries = versions.Select(version => new LocalizationHistoryEntry(
      version.VersionNumber,
      version.Value,
      version.IsActive,
      version.ChangeType.ToString(),
      version.PriorLogicalVersionNumber,
      version.UndoTargetVersionNumber,
      version.CatalogVersion,
      version.ResourceVersion,
      version.ActorId,
      version.OccurredUtc)).ToArray();
    var eligibleTarget = await dbContext.TenantLocalizationOverrideVersions.AsNoTracking()
      .Where(version => version.TenantLocalizationOverrideId == current.Id && version.VersionNumber == current.CurrentVersionNumber)
      .Select(version => version.PriorLogicalVersionNumber.HasValue ? version.PriorLogicalVersionNumber.Value.Value : (long?)null)
      .SingleOrDefaultAsync(cancellationToken);
    return new LocalizationHistoryResult(
      current.Id,
      resourceKey.Value,
      culture.Value,
      current.IsActive,
      current.CurrentVersionNumber.Value,
      eligibleTarget,
      [.. current.RowVersion],
      entries,
      pageNumber,
      pageSize,
      totalCount);
  }
}
