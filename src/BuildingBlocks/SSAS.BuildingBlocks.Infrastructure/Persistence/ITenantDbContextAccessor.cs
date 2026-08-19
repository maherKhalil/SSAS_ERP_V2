using Microsoft.EntityFrameworkCore;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

// THE TENANT ERP CONTEXT, AS A MODULE SEES IT (FP-006C3, ADR-012, ADR-017).
//
// A module's repositories persist through the SAME context and the SAME unit of work Platform uses — one
// context per scope, so reads and writes in a request cannot straddle two databases (ADR-017). But the
// concrete context type belongs to Platform, and a module may not reference it.
//
// So a module receives it as a plain DbContext and reaches its own entities through `Set<TEntity>()`. That
// is deliberately all it gets: the routing, the write fence, the tenant/company/branch boundaries and the
// contributor set are all on the instance and all still apply, but none of them is addressable from here.
//
// ---- IT FAILS LOUD, NOT SOFT.
//
// A routing failure returns no context at all rather than null or an empty result. A read that returned
// nothing on a routing failure would be indistinguishable from "this tenant has no data", which is the
// misrouting asymmetry ADR-017 calls out: the wrong answer looks exactly like a legitimate one.
public interface ITenantDbContextAccessor
{
  Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default);
}
