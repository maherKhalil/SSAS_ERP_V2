using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Subscriptions.EntitlementGrants;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Subscriptions;

public class TenantEntitlementGrantQueries(PlatformDbContext context) : ITenantEntitlementGrantQueries
{
  public async Task<Result<IReadOnlyList<TenantEntitlementGrantDto>>> GetTenantGrantsAsync(Guid tenantId, CancellationToken cancellationToken)
  {
    var grants = await context.TenantEntitlementGrants
      .AsNoTracking()
      .Where(g => g.TenantId == tenantId)
      .OrderByDescending(g => g.EffectiveFromUtc)
      .Select(g => new TenantEntitlementGrantDto(
        g.Id,
        g.TenantId,
        g.GrantKind.ToString(),
        g.ModuleKey != null ? g.ModuleKey.Value : null,
        g.LimitKey,
        g.LimitValue,
        g.EffectiveFromUtc,
        g.ExpiresUtc,
        g.CreatedUtc,
        g.GrantedBy,
        g.ReasonCode,
        g.ReasonText
      ))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<TenantEntitlementGrantDto>>(grants);
  }
}
