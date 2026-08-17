using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// ROUTINGVERSION IS THE CORRECTNESS MECHANISM (ADR-020 "Resolver cache").
//
// A cached route is valid ONLY for the version it was resolved under. Not for a TTL that has not expired,
// not because no invalidation arrived, not because an entry exists. Every resolution therefore reads the
// authoritative version first and compares; a mismatch discards the entry and reloads.
//
// WHY THIS RATHER THAN BROADCAST INVALIDATION. An earlier draft of ADR-020 permitted either mechanism, and
// that is unsafe: broadcast is best-effort, so a node that is starting, partitioned or restarting can miss
// the signal and serve stale routing indefinitely — writing tenant data to the pre-cutover database. The
// version check cannot be missed, because it is on the path. Invalidation only makes convergence faster,
// which is why nothing here depends on a transport that does not yet exist.
//
// FAIL CLOSED WHEN THE VERSION CANNOT BE READ. If the Platform database cannot answer, this refuses to
// route rather than serving what it happens to remember. "Platform unavailable, so use the cached shared
// route" is exactly the reasoning that writes a tenant's data to the wrong database during a cutover, and
// temporary unavailability is the better failure.
//
// IT DECORATES the existing resolver rather than reimplementing it: authoritative resolution, its
// CustomerManaged refusal and its fail-closed paths all stay in one place.
public sealed class VersionAwareTenantDatabaseResolver(
  ITenantDatabaseResolver inner,
  ITenantRoutingVersionReader versionReader,
  ITenantRoutingCache cache,
  TenantRoutingCacheOptions options,
  IDateTimeProvider clock) : ITenantDatabaseResolver
{
  public async Task<Result<TenantDatabaseRoute>> ResolveAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantDatabaseRoute>(TenantStorageErrors.TenantContextMissing);
    }

    // ---- THE AUTHORITATIVE VERSION, FIRST AND ALWAYS.
    var currentVersion = await versionReader.ReadCurrentRoutingVersionAsync(tenantId, cancellationToken);
    if (currentVersion.IsFailure)
    {
      // The cached route is deliberately NOT consulted here, and the entry is deliberately NOT evicted:
      // a transient Platform outage is not evidence that routing changed.
      return Result.Failure<TenantDatabaseRoute>(currentVersion.Error);
    }

    if (cache.TryGet(tenantId, out var cached) &&
      cached.RoutingVersion == currentVersion.Value &&
      IsFresh(cached))
    {
      return Result.Success(cached.Route);
    }

    // ---- MISS, STALE VERSION, OR AGED ENTRY: the authoritative route wins.
    var resolved = await inner.ResolveAsync(tenantId, cancellationToken);
    if (resolved.IsFailure)
    {
      // A tenant that no longer routes must not keep a usable entry behind it.
      cache.Remove(tenantId);
      return resolved;
    }

    cache.Store(tenantId, new TenantRoutingCacheEntry(resolved.Value, clock.UtcNow));
    return resolved;
  }

  // A BOUNDED LIFETIME, AND NOT A CORRECTNESS BOUND. The route carries cached ADR-018 health, which the
  // traffic gate evaluates for staleness; refreshing periodically keeps that observation reasonable. Where
  // routing itself is concerned this adds nothing the version comparison has not already decided.
  private bool IsFresh(TenantRoutingCacheEntry entry)
  {
    var lifetime = options.Lifetime;
    return lifetime > TimeSpan.Zero && clock.UtcNow - entry.CachedUtc <= lifetime;
  }
}

// How long a version-matched entry may be reused before the full route is re-read.
//
// NOTHING HERE IS A SAFETY CONTROL: there is no setting that skips the version check or permits a stale
// route. Raising it trades health freshness for fewer wide reads; it can never trade away correctness.
public sealed class TenantRoutingCacheOptions
{
  public const string SectionName = "TenantStorage:Routing";

  public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(30);
}
