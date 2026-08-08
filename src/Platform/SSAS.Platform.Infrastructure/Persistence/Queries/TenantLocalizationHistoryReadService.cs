using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

public sealed class TenantLocalizationHistoryReadService(PlatformDbContext dbContext)
  : ITenantLocalizationHistoryReadService
{
  private const int MaximumHistoryEntries = 100;

  public async Task<LocalizationHistoryResult?> GetAsync(
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
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
        CurrentVersionNumber = candidate.CurrentVersionNumber.Value,
        candidate.RowVersion
      })
      .SingleOrDefaultAsync(cancellationToken);
    if (current is null)
    {
      return null;
    }

    var versions = await dbContext.TenantLocalizationOverrideVersions.AsNoTracking()
      .Where(version => version.TenantId == tenantId && version.TenantLocalizationOverrideId == current.Id)
      .OrderByDescending(version => version.VersionNumber)
      .Take(MaximumHistoryEntries)
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
    var eligibleTarget = versions.FirstOrDefault(version => version.VersionNumber == current.CurrentVersionNumber)
      ?.PriorLogicalVersionNumber;
    return new LocalizationHistoryResult(
      current.Id,
      resourceKey.Value,
      culture.Value,
      current.IsActive,
      current.CurrentVersionNumber,
      eligibleTarget,
      [.. current.RowVersion],
      entries);
  }
}
