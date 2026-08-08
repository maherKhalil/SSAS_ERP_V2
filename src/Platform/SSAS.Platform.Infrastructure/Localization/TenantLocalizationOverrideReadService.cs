using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class TenantLocalizationOverrideReadService(PlatformDbContext dbContext)
  : ITenantLocalizationOverrideReadService
{
  public async Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> ReadAsync(
    Guid tenantId,
    LocalizationCulture culture,
    IReadOnlyCollection<ResourceKey> resourceKeys,
    CancellationToken cancellationToken = default)
  {
    var keys = resourceKeys.Select(key => key.Value).Distinct(StringComparer.Ordinal).ToArray();
    var keysJson = JsonSerializer.Serialize(keys);
    var entities = await dbContext.TenantLocalizationOverrides
      .FromSqlInterpolated(
        $"""
        SELECT localizationOverride.*
        FROM [platform].[TenantLocalizationOverrides] localizationOverride
        INNER JOIN OPENJSON({keysJson}) WITH ([ResourceKey] nvarchar(200) '$') requested
          ON requested.[ResourceKey] = localizationOverride.[ResourceKey]
        WHERE localizationOverride.[TenantId] = {tenantId}
          AND localizationOverride.[Culture] = {culture.Value}
        """)
      .AsNoTracking()
      .ToArrayAsync(cancellationToken);
    return entities
      .OrderBy(localizationOverride => localizationOverride.ResourceKey.Value, StringComparer.Ordinal)
      .Select(localizationOverride => new TenantLocalizationOverrideReadModel(
        localizationOverride.ResourceKey.Value,
        localizationOverride.Culture.Value,
        localizationOverride.TextFormat,
        localizationOverride.CurrentValue,
        localizationOverride.IsActive,
        localizationOverride.CurrentVersionNumber.Value,
        localizationOverride.CatalogVersion.Value,
        localizationOverride.ResourceVersion.Value,
        [.. localizationOverride.PlaceholderFingerprint.Bytes],
        [.. localizationOverride.CompatibilityFingerprint.Bytes]))
      .ToArray();
  }
}
