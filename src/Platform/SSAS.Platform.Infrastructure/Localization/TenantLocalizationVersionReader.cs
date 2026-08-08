using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class TenantLocalizationVersionReader(PlatformDbContext dbContext) : ITenantLocalizationVersionReader
{
  public async Task<long> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var version = await dbContext.TenantLocalizationSettings.AsNoTracking()
      .Where(settings => settings.TenantId == tenantId)
      .Select(settings => (long?)settings.TenantLocalizationVersion.Value)
      .SingleOrDefaultAsync(cancellationToken);
    return version ?? 1;
  }
}
