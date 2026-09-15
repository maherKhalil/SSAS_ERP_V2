using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantEntitlementGrantRepository(PlatformDbContext context) : ITenantEntitlementGrantRepository
{
  public async Task AddAsync(TenantEntitlementGrant grant, CancellationToken cancellationToken)
  {
    await context.TenantEntitlementGrants.AddAsync(grant, cancellationToken);
  }
}
