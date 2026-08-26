using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Subscriptions;

namespace SSAS.Platform.API.Subscriptions;

// ==================================================================================================
// THE REAL ENTITLEMENT RESOLVER. IT REPLACES THE TRANSITIONAL ONE (FP-014, T-040).
// ==================================================================================================
//
// ---- WHY THIS TYPE IS IN `Platform.API` AND NOT IN `Platform.Infrastructure`.
//
// The contract is `ITenantModuleEntitlement`, which lives in `SSAS.BuildingBlocks.Api` because the
// endpoint convention that resolves it lives there and that project **references nothing**. The read
// lives in `Platform.Infrastructure`, which does not reference `BuildingBlocks.Api` and should not
// start: an infrastructure project taking a dependency on a transport contract is the layering
// inversion the dependency guards exist to catch.
//
// `Platform.API` already references **both** `BuildingBlocks.Api` and `Platform.Application`, so it is
// the one place the two sides legitimately meet. This type is an adapter and holds no logic of its own:
// the facts come from the reader, the decision is the snapshot's, and the clock is injected.
//
// ---- WHAT IT DOES NOT DO, AND WHY THAT IS THE POINT OF CRITERION 3.
//
// **It cannot deny authentication.** It answers exactly one question — *is this module enabled for the
// current tenant* — and it is reachable only from `RequireModule`, which is applied to module route
// groups and to nothing else. The platform plane carries no module key (`REQ-SUB-0013`), so the
// authentication, tenant-selection, refresh, logout, support, localization, identity and company
// surfaces never consult this at all.
//
// **So `DEC-L-033` needed no special case here**, and that is a fact about `REQ-SUB-0013` doing its job
// rather than about this code being careful. Had the platform plane been gated, an expired tenant would
// have been locked out of the surface it renews from, and no amount of care in this file could have
// fixed it.
public sealed class TenantModuleEntitlement(
  ICurrentTenant currentTenant,
  ITenantEntitlementReader reader,
  ITenantEntitlementCache cache,
  IDateTimeProvider clock) : ITenantModuleEntitlement
{
  public async ValueTask<bool> IsEnabledAsync(string moduleKey, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

    // ---- NO TENANT MEANS NO ENTITLEMENT, AND IT IS NOT AN ERROR.
    //
    // A gated route reached without a resolved tenant is refused rather than admitted. Throwing would
    // turn a commercial state into a 500, which the contract explicitly forbids; admitting would make
    // the gate depend on tenancy resolution having succeeded, which is the wrong direction to fail.
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return false;
    }

    if (!cache.TryGet(tenantId, out var snapshot))
    {
      snapshot = await reader.ReadAsync(tenantId, cancellationToken);
      cache.Store(snapshot);
    }

    // ---- THE CLOCK IS READ HERE, ON EVERY REQUEST, AND THAT IS WHAT MAKES EXPIRY CORRECT.
    //
    // The snapshot may have been cached before the term lapsed. Because it carries the term rather than
    // a decision, evaluating it now gives the right answer without any write having occurred and
    // without anything having evicted the entry — which is the only way to satisfy both `BR-SUB-0012`
    // and `BR-SUB-0013` at once.
    return snapshot.IsModuleEnabledAt(moduleKey, clock.UtcNow);
  }
}
