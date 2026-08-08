using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class TenantLocalizationAdministrationReadService(PlatformDbContext dbContext)
  : ITenantLocalizationAdministrationReadService
{
  public async Task<IReadOnlyList<TenantLocalizationOverrideAdministrationReadModel>> ReadAsync(
    Guid tenantId,
    LocalizationCulture culture,
    IReadOnlyCollection<ResourceKey> resourceKeys,
    CancellationToken cancellationToken = default)
  {
    if (resourceKeys.Count == 0) return [];
    var keysJson = JsonSerializer.Serialize(resourceKeys.Select(item => item.Value).Distinct(StringComparer.Ordinal).ToArray());
    var entities = await dbContext.TenantLocalizationOverrides.FromSqlInterpolated(
      $"""
      SELECT localizationOverride.*
      FROM [platform].[TenantLocalizationOverrides] localizationOverride
      INNER JOIN OPENJSON({keysJson}) WITH ([ResourceKey] nvarchar(200) '$') requested
        ON requested.[ResourceKey] = localizationOverride.[ResourceKey]
      WHERE localizationOverride.[TenantId] = {tenantId}
        AND localizationOverride.[Culture] = {culture.Value}
      """).AsNoTracking().ToArrayAsync(cancellationToken);
    if (entities.Length == 0) return [];
    var overrideIds = entities.Select(entity => entity.Id).ToArray();
    var versions = await (
      from version in dbContext.TenantLocalizationOverrideVersions.AsNoTracking()
      join current in dbContext.TenantLocalizationOverrides.AsNoTracking()
        on new { OverrideId = version.TenantLocalizationOverrideId, VersionNumber = version.VersionNumber }
        equals new { OverrideId = current.Id, VersionNumber = current.CurrentVersionNumber }
      where current.TenantId == tenantId && overrideIds.Contains(current.Id)
      select new
      {
        version.TenantLocalizationOverrideId,
        VersionNumber = version.VersionNumber.Value,
        PriorLogicalVersionNumber = version.PriorLogicalVersionNumber.HasValue
          ? version.PriorLogicalVersionNumber.Value.Value
          : (long?)null
      })
      .ToArrayAsync(cancellationToken);
    var eligibleUndoTargets = versions.ToDictionary(
      version => (version.TenantLocalizationOverrideId, version.VersionNumber),
      version => version.PriorLogicalVersionNumber);
    return entities.Select(item => new TenantLocalizationOverrideAdministrationReadModel(
      item.ResourceKey.Value, item.CurrentValue, item.IsActive, item.CurrentVersionNumber.Value, item.CurrentVersionNumber.Value,
      item.CatalogVersion.Value, item.ResourceVersion.Value, [.. item.PlaceholderFingerprint.Bytes], [.. item.CompatibilityFingerprint.Bytes],
      [.. item.RowVersion], item.ModifiedUtc,
      eligibleUndoTargets.GetValueOrDefault((item.Id, item.CurrentVersionNumber.Value)))).OrderBy(item => item.ResourceKey, StringComparer.Ordinal).ToArray();
  }
}
