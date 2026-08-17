using System.Collections.Concurrent;
using SSAS.Platform.Application.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The process-local route cache (ADR-020 "Resolver cache").
//
// A PLAIN CONCURRENT DICTIONARY, ON PURPOSE. Everything that would normally make a cache hard — expiry
// races, coherence between nodes, missed invalidations — stops being a correctness concern once the
// version is checked on every read, so the remaining job is a lookup. Adding a distributed cache here
// would introduce an external dependency to solve a problem the version check has already solved.
//
// SINGLETON, so entries actually outlive a request. Registered as such, and it holds only
// `TenantDatabaseRoute` values: a ServerKey and a database name, never a connection string or credential.
//
// UNBOUNDED BY DESIGN IN V1. One small entry per tenant with an active assignment is bounded by the size
// of the estate, and evicting by size would need a policy nobody has decided. If the fleet ever grows
// enough for that to matter, the entry count is observable through `Count`.
internal sealed class TenantRoutingMemoryCache : ITenantRoutingCache, ITenantRoutingCacheInvalidator
{
  private readonly ConcurrentDictionary<Guid, TenantRoutingCacheEntry> entries = new();

  public int Count => entries.Count;

  public bool TryGet(Guid tenantId, out TenantRoutingCacheEntry entry) =>
    entries.TryGetValue(tenantId, out entry!);

  public void Store(Guid tenantId, TenantRoutingCacheEntry entry)
  {
    ArgumentNullException.ThrowIfNull(entry);
    entries[tenantId] = entry;
  }

  // EXACT TENANT, IDEMPOTENT, NEVER A SWEEP. Removing an absent key is a no-op, and no other tenant's
  // entry is touched — one tenant's cutover is not a reason to reload the estate.
  public void Remove(Guid tenantId) => entries.TryRemove(tenantId, out _);

  // The invalidation contract. THIS PROCESS ONLY, and the type comment says so rather than implying a
  // reach it does not have: other instances converge through version validation, not through this call.
  public void Invalidate(Guid tenantId) => Remove(tenantId);
}
