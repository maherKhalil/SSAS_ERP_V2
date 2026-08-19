using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// The module-facing view of the scoped tenant ERP context (FP-006C3, ADR-012).
//
// IT ADDS NOTHING AND HIDES NOTHING IT SHOULD NOT. The instance handed back is the very context Platform's
// own repositories use — same scope, same routing, same write fence, same tenant, company and branch
// boundaries — presented as a plain DbContext because a module may not name its type.
//
// Delegating to ITenantDbContextProvider rather than resolving independently is what keeps a module and
// Platform in ONE unit of work: a second context would silently discard whatever the other tracked.
internal sealed class TenantDbContextAccessor(ITenantDbContextProvider provider) : ITenantDbContextAccessor
{
  public async Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
    await provider.GetRequiredAsync(cancellationToken);
}
