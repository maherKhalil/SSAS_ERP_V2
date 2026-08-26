using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Subscriptions;

// THE READ, AGAINST THE PLATFORM DATABASE AND NOTHING ELSE (FP-014, T-040).
//
// ---- WHICH RECORD IS IN FORCE, AND WHY IT IS AN ORDERING RATHER THAN A FLAG.
//
// `OD-SUB-0008` ruled append-only history with exactly one record in force, and that "one" is derived,
// never stored: **the record with the greatest `EffectiveFromUtc <= now`.** There is no closing column
// and no in-force flag, which is why this is `OrderByDescending(...).FirstOrDefault()` and why the
// schema carries `UX_TenantSubscriptions_Tenant_EffectiveFromDesc` — the index makes it a seek to the
// first row rather than a scan and a sort.
//
// ---- THE INSTANT IS "NOW", AND THAT IS THE ONLY PLACE IT IS.
//
// A historical question — *what was this tenant entitled to last March* — is answered by passing a
// different instant to the snapshot, not by reading a different row set here. This read deliberately
// takes the records that have taken effect and lets the snapshot decide; a cap in force is a property
// of the record live at that moment, and baking "now" into the query would make the cached value
// unusable for any other instant.
public sealed class TenantEntitlementReader(PlatformDbContext context) : ITenantEntitlementReader
{
  public async Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken)
  {
    if (tenantId == Guid.Empty)
    {
      return TenantEntitlementSnapshot.None(tenantId);
    }

    // The greatest `EffectiveFromUtc` for this tenant. No upper bound on the instant: a record dated in
    // the future has not taken effect, and the snapshot's own evaluation is where that is decided —
    // except that "in force" is defined against the present for the purpose of caching, so the read
    // takes only records that have already begun.
    var now = DateTimeOffset.UtcNow;

    var inForce = await context.TenantSubscriptions
      .AsNoTracking()
      .Where(subscription => subscription.TenantId == tenantId && subscription.EffectiveFromUtc <= now)
      .OrderByDescending(subscription => subscription.EffectiveFromUtc)
      .FirstOrDefaultAsync(cancellationToken);

    if (inForce is null)
    {
      // No record: entitled to nothing, and that is an answer rather than a failure.
      return TenantEntitlementSnapshot.None(tenantId);
    }

    var plan = await context.SubscriptionPlans
      .AsNoTracking()
      .Include(item => item.ModuleGrants)
      .Include(item => item.Limits)
      .FirstOrDefaultAsync(item => item.Id == inForce.SubscriptionPlanId, cancellationToken);

    var modules = new HashSet<string>(StringComparer.Ordinal);
    var limits = new Dictionary<string, long>(StringComparer.Ordinal);

    if (plan is not null)
    {
      foreach (var granted in plan.ModuleGrants)
      {
        modules.Add(granted.ModuleKey.Value);
      }

      foreach (var limit in plan.Limits)
      {
        limits[limit.LimitKey] = limit.LimitValue;
      }
    }

    // Grants are taken whole and filtered on read. An expiring grant is not a mutation — it is a value
    // read at resolution time — so filtering here would bake an instant into the cache.
    var grants = await context.TenantEntitlementGrants
      .AsNoTracking()
      .Where(grant => grant.TenantId == tenantId)
      .Select(grant => new EntitlementGrantFact(
        grant.GrantKind,
        grant.ModuleKey == null ? null : grant.ModuleKey.Value,
        grant.LimitKey,
        grant.LimitValue,
        grant.EffectiveFromUtc,
        grant.ExpiresUtc))
      .ToListAsync(cancellationToken);

    return new TenantEntitlementSnapshot(
      tenantId, inForce.SubscriptionPlanId, inForce.Term, modules, limits, grants);
  }
}
