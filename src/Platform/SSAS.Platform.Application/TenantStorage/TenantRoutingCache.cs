using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.TenantStorage;

// A PROCESS-LOCAL route cache (ADR-017 binding lifetime rules, ADR-020 "Resolver cache").
//
// IT IS NOT AUTHORITATIVE, AND CANNOT BECOME SO. Nothing reads an entry without first confirming the
// authoritative `RoutingVersion` still matches it, so the cache can only ever save a wider read — never
// decide where a tenant's data lives. A cached route whose version has moved is discarded, not served.
//
// NO SECRETS. `TenantDatabaseRoute` carries a ServerKey rather than any connection string, credential or
// endpoint, and the connection factory resolves that key against trusted configuration at use time. A
// CustomerManaged tenant never reaches this cache at all: the resolver refuses it before a route exists.
public interface ITenantRoutingCache
{
  bool TryGet(Guid tenantId, out TenantRoutingCacheEntry entry);

  void Store(Guid tenantId, TenantRoutingCacheEntry entry);

  // EXACT TENANT ONLY. Idempotent, and never a sweep: evicting every tenant because one moved would turn
  // a routine cutover into a fleet-wide reload.
  void Remove(Guid tenantId);

  int Count { get; }
}

// Eviction on demand. Deliberately a SEPARATE interface from the cache: the future routing flip needs to
// invalidate after commit, and it should not receive the ability to write cache entries to do it.
//
// THIS IS AN OPTIMISATION, NOT A CORRECTNESS MECHANISM. The current implementation evicts THIS PROCESS
// only. Cross-instance correctness comes from version validation, which is why no external transport —
// Redis, Service Bus, a broker — is required or introduced here.
public interface ITenantRoutingCacheInvalidator
{
  void Invalidate(Guid tenantId);
}

// What is remembered about a tenant's route, and when.
//
// `CachedUtc` supports a bounded lifetime, which is a FRESHNESS aid for the operational health carried on
// the route — not a correctness bound. Correctness is the version comparison, and it happens on every
// resolution regardless of how recently the entry was written.
public sealed record TenantRoutingCacheEntry(TenantDatabaseRoute Route, DateTimeOffset CachedUtc)
{
  public Guid TenantId => Route.TenantId;

  public long TenantDatabaseId => Route.TenantDatabaseId;

  public TenantDatabaseHostingMode HostingMode => Route.HostingMode;

  public TenantDatabaseStorageMode StorageMode => Route.StorageMode;

  public long RoutingVersion => Route.RoutingVersion;
}
