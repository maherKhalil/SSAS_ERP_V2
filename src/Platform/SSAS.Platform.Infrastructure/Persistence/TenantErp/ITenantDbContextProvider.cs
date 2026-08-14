using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Supplies the TenantDbContext for the current unit of work (ADR-017).
//
// One context per scope, created on first use and disposed with the scope. That is precisely the ADR's
// "routing resolves per TenantDbContext creation / unit of work": a request resolves routing once and
// every repository in that request shares the resulting context, so reads and writes within one unit of
// work cannot straddle two databases. It is NOT a routing cache — nothing survives the scope, and the next
// request resolves again from current registry state.
public interface ITenantDbContextProvider
{
  // Fail-closed form for callers that can handle a routing failure as a Result.
  Task<Result<TenantDbContext>> ResolveAsync(CancellationToken cancellationToken = default);

  // Fail-LOUD form for repositories and read services. A read that returned null or an empty page on a
  // routing failure would be indistinguishable from "this tenant has no data" (ADR-017 misrouting
  // asymmetry), so this throws TenantStorageUnavailableException instead.
  Task<TenantDbContext> GetRequiredAsync(CancellationToken cancellationToken = default);
}
