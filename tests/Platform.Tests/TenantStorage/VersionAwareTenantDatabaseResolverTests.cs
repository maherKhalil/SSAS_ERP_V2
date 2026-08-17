using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// ROUTINGVERSION IS THE CORRECTNESS MECHANISM (ADR-020 "Resolver cache", TS-Storage Phase E2).
//
// Every test here exists to pin one property: a cached route is served ONLY when the authoritative version
// still matches it. Not when a TTL has not expired, not when no invalidation arrived, not when the entry
// looks recent. The negative cases are the load-bearing ones — a cache that is merely usually right is what
// writes a tenant's data into the database it was just moved off.
//
// THE AUTHORITATIVE SOURCE IS ONE OBJECT in these tests, exactly as it is one Platform database in
// production: the version reader and the inner resolver both read `StubRegistry`. A stub that let the two
// disagree could make convergence look proven when the production shape had not been exercised at all.
public sealed class VersionAwareTenantDatabaseResolverTests
{
  private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

  // ---- A. Cache miss: version read, authoritative resolve, entry populated.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_cache_miss_reads_the_version_resolves_authoritatively_and_caches()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4, databaseName: "SSAS_Shared_01");

    var result = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(result.IsSuccess);
    Assert.Equal("SSAS_Shared_01", result.Value.DatabaseName);
    Assert.Equal(4, result.Value.RoutingVersion);
    Assert.Equal(1, world.Versions.CallCount);
    Assert.Equal(1, world.Registry.CallCount);
    Assert.Equal(1, world.Cache.Count);
  }

  // ---- B. Cache hit at the same version: the version is STILL read, the wide resolve is skipped.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_cache_hit_at_the_same_version_still_reads_the_version_but_skips_the_resolver()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4);

    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    var second = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(second.IsSuccess);
    Assert.Equal(4, second.Value.RoutingVersion);

    // The version check is UNCONDITIONAL — it happens on the hit path too. A cache that skipped it when an
    // entry existed would be a cache that can never notice a cutover.
    Assert.Equal(2, world.Versions.CallCount);

    // ...and the saving is real: the wide registry read did not happen a second time.
    Assert.Equal(1, world.Registry.CallCount);
  }

  // ---- C. Cached N, authoritative N+1: the entry is rejected and replaced.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_moved_routing_version_rejects_the_cached_route_and_reloads()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 7, databaseName: "SSAS_Shared_01");
    Assert.Equal("SSAS_Shared_01", (await world.Resolver.ResolveAsync(TenantA)).Value.DatabaseName);

    // The cutover, as the registry sees it: a new dedicated database at the next routing version.
    world.Register(
      TenantA, routingVersion: 8, databaseName: "SSAS_Dedicated_A",
      storageMode: TenantDatabaseStorageMode.Dedicated);

    var second = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(second.IsSuccess);
    Assert.Equal("SSAS_Dedicated_A", second.Value.DatabaseName);
    Assert.Equal(8, second.Value.RoutingVersion);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, second.Value.StorageMode);
    Assert.Equal(2, world.Registry.CallCount);

    // The replacement is in the cache, not merely in the returned value.
    Assert.True(world.Cache.TryGet(TenantA, out var cached));
    Assert.Equal(8, cached.RoutingVersion);
    Assert.Equal("SSAS_Dedicated_A", cached.Route.DatabaseName);
  }

  // ---- D. THE CASE THE WHOLE DESIGN EXISTS FOR. The version cannot be read and a valid-looking cached
  // route is sitting right there. It is not served.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_version_read_failure_refuses_to_route_rather_than_serving_the_cached_route()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4, databaseName: "SSAS_Shared_01");
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    var registryReadsBefore = world.Registry.CallCount;

    world.Versions.Fails = true;
    var result = await world.Resolver.ResolveAsync(TenantA);

    // FAIL CLOSED. "Platform unavailable, so use the cached shared route" is precisely the reasoning that
    // sends a tenant's writes to the pre-cutover database.
    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.RoutingVersionUnavailable.Code, result.Error.Code);

    // And it is not reported as a tenant configuration problem, which would send an operator to the wrong place.
    Assert.NotEqual(TenantStorageErrors.ActiveAssignmentMissing.Code, result.Error.Code);
    Assert.NotEqual(TenantStorageErrors.TenantContextMissing.Code, result.Error.Code);

    // The inner resolver is not consulted either: without a version there is nothing to validate against.
    Assert.Equal(registryReadsBefore, world.Registry.CallCount);
  }

  // ---- E. A transient version-read failure is NOT evidence that routing changed, so it does not evict.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_version_read_failure_leaves_an_otherwise_valid_cache_entry_intact()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4);
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    Assert.Equal(1, world.Registry.CallCount);

    world.Versions.Fails = true;
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsFailure);
    Assert.Equal(1, world.Cache.Count);

    // The outage ends. The same version is still current, so the surviving entry is usable and no wide read
    // is needed — a blunt "evict on any failure" would have turned a blip into a reload storm.
    world.Versions.Fails = false;
    var recovered = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(recovered.IsSuccess);
    Assert.Equal(4, recovered.Value.RoutingVersion);
    Assert.Equal(1, world.Registry.CallCount);
  }

  // ---- F. The lifetime is freshness hygiene: an aged entry is reloaded even though its version matches.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task An_expired_entry_reloads_authoritatively_even_at_an_unchanged_version()
  {
    var world = new World(lifetime: TimeSpan.FromSeconds(30));
    world.Register(TenantA, routingVersion: 4);
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);

    world.Clock.UtcNow = world.Clock.UtcNow.AddSeconds(31);
    var result = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(result.IsSuccess);
    Assert.Equal(4, result.Value.RoutingVersion);
    Assert.Equal(2, world.Registry.CallCount);

    // Reloading refreshes the entry rather than leaving it aged, so the next call is a hit again.
    Assert.True(world.Cache.TryGet(TenantA, out var cached));
    Assert.Equal(world.Clock.UtcNow, cached.CachedUtc);
  }

  // Expiry can only ever cause MORE authoritative reading, never less. A one-second-old entry still faces
  // the version comparison, which is what keeps the lifetime out of the correctness argument entirely.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_barely_aged_entry_is_still_version_checked()
  {
    var world = new World(lifetime: TimeSpan.FromMinutes(10));
    world.Register(TenantA, routingVersion: 4, databaseName: "SSAS_Shared_01");
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);

    world.Clock.UtcNow = world.Clock.UtcNow.AddSeconds(1);
    world.Register(
      TenantA, routingVersion: 5, databaseName: "SSAS_Dedicated_A",
      storageMode: TenantDatabaseStorageMode.Dedicated);

    var result = await world.Resolver.ResolveAsync(TenantA);

    Assert.Equal("SSAS_Dedicated_A", result.Value.DatabaseName);
    Assert.Equal(5, result.Value.RoutingVersion);
  }

  // ---- G. Invalidation is a propagation aid, and it works: the next resolve reloads.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Invalidating_a_tenant_forces_the_next_resolution_to_reload()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4);
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    Assert.Equal(1, world.Registry.CallCount);

    // Invalidation is exercised through the NARROW interface a routing flip would hold — the one that can
    // evict but cannot write cache entries.
    ((ITenantRoutingCacheInvalidator)world.Cache).Invalidate(TenantA);
    Assert.Equal(0, world.Cache.Count);

    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    Assert.Equal(2, world.Registry.CallCount);

    // IDEMPOTENT, and never an error: a flip that retries its invalidation must not fail because the entry
    // was already gone.
    // Invalidation is exercised through the NARROW interface a routing flip would hold — the one that can
    // evict but cannot write cache entries.
    ((ITenantRoutingCacheInvalidator)world.Cache).Invalidate(TenantA);
    // Invalidation is exercised through the NARROW interface a routing flip would hold — the one that can
    // evict but cannot write cache entries.
    ((ITenantRoutingCacheInvalidator)world.Cache).Invalidate(Guid.NewGuid());
  }

  // ---- H. Exact tenant only. One tenant's cutover is not a fleet-wide reload.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task Invalidating_one_tenant_leaves_every_other_tenant_cached()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4, databaseName: "SSAS_A");
    world.Register(TenantB, routingVersion: 4, databaseName: "SSAS_B");
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    Assert.True((await world.Resolver.ResolveAsync(TenantB)).IsSuccess);
    Assert.Equal(2, world.Registry.CallCount);

    // Invalidation is exercised through the NARROW interface a routing flip would hold — the one that can
    // evict but cannot write cache entries.
    ((ITenantRoutingCacheInvalidator)world.Cache).Invalidate(TenantA);

    Assert.Equal(1, world.Cache.Count);
    Assert.False(world.Cache.TryGet(TenantA, out _));
    Assert.True(world.Cache.TryGet(TenantB, out var cachedB));
    Assert.Equal("SSAS_B", cachedB.Route.DatabaseName);

    // Tenant B is served from cache; only tenant A pays for the reload.
    Assert.Equal("SSAS_B", (await world.Resolver.ResolveAsync(TenantB)).Value.DatabaseName);
    Assert.Equal(2, world.Registry.CallCount);
  }

  // ---- I. THE CROSS-INSTANCE PROPERTY, which is why no broadcast transport is required.
  //
  // Two processes, two caches, one authoritative registry. Instance A is told nothing about the cutover and
  // still converges, because the version check is on its path and cannot be missed.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task An_instance_that_receives_no_invalidation_still_converges_on_the_new_route()
  {
    var world = new World();
    var instanceA = world.NewInstance();
    var instanceB = world.NewInstance();

    world.Register(TenantA, routingVersion: 9, databaseName: "SSAS_Shared_01");
    Assert.Equal("SSAS_Shared_01", (await instanceA.ResolveAsync(TenantA)).Value.DatabaseName);
    Assert.Equal("SSAS_Shared_01", (await instanceB.ResolveAsync(TenantA)).Value.DatabaseName);

    // The cutover commits on some other node. NOTHING is delivered to instance A.
    world.Register(
      TenantA, routingVersion: 10, databaseName: "SSAS_Dedicated_A",
      storageMode: TenantDatabaseStorageMode.Dedicated);

    var converged = await instanceA.ResolveAsync(TenantA);

    Assert.True(converged.IsSuccess);
    Assert.Equal("SSAS_Dedicated_A", converged.Value.DatabaseName);
    Assert.Equal(10, converged.Value.RoutingVersion);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, converged.Value.StorageMode);
  }

  // ---- J. CustomerManaged refusal survives the decorator, and nothing about it is remembered.
  [Fact]
  [Trait("Decision", "ADR-021")]
  public async Task A_customer_managed_placement_is_refused_and_never_cached()
  {
    var world = new World();
    world.Register(
      TenantA, routingVersion: 4,
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated);

    var result = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, result.Error.Code);

    // The refusal happens before a route exists, so there is nothing to cache — and no path by which a
    // customer-owned endpoint could end up in a process-local dictionary.
    Assert.Equal(0, world.Cache.Count);
  }

  // A tenant that stops routing must not keep a usable entry behind it.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public async Task A_route_that_stops_resolving_evicts_its_cached_entry()
  {
    var world = new World();
    world.Register(TenantA, routingVersion: 4);
    Assert.True((await world.Resolver.ResolveAsync(TenantA)).IsSuccess);
    Assert.Equal(1, world.Cache.Count);

    // The version still reads, but the assignment no longer produces a route (the database left Ready).
    world.Register(
      TenantA, routingVersion: 5, provisioningStatus: TenantDatabaseProvisioningStatus.Disabled);

    var result = await world.Resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseNotReady.Code, result.Error.Code);
    Assert.Equal(0, world.Cache.Count);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task An_empty_tenant_id_fails_closed_without_touching_the_registry()
  {
    var world = new World();

    var result = await world.Resolver.ResolveAsync(Guid.Empty);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantContextMissing.Code, result.Error.Code);
    Assert.Equal(0, world.Versions.CallCount);
    Assert.Equal(0, world.Registry.CallCount);
  }

  // ONE authoritative registry, N resolver instances over it — the production shape in miniature.
  private sealed class World
  {
    private readonly TimeSpan lifetime;

    public World(TimeSpan? lifetime = null)
    {
      this.lifetime = lifetime ?? TimeSpan.FromSeconds(30);
      Cache = new TenantRoutingMemoryCache();
      Versions = new RegistryVersionReader(Registry);
      Resolver = Compose(Cache, Versions);
    }

    public StubRegistry Registry { get; } = new();

    public TenantRoutingMemoryCache Cache { get; }

    public RegistryVersionReader Versions { get; }

    public VersionAwareTenantDatabaseResolver Resolver { get; }

    public MutableClock Clock { get; } = new();

    // A separate process: its own cache and its own reader, over the SAME registry.
    public VersionAwareTenantDatabaseResolver NewInstance() =>
      Compose(new TenantRoutingMemoryCache(), new RegistryVersionReader(Registry));

    private VersionAwareTenantDatabaseResolver Compose(
      ITenantRoutingCache cache, ITenantRoutingVersionReader versions) =>
      new(
        new TenantDatabaseResolver(Registry),
        versions,
        cache,
        new TenantRoutingCacheOptions { Lifetime = lifetime },
        Clock);

    public void Register(
      Guid tenantId,
      long routingVersion = 1,
      long tenantDatabaseId = 25,
      string databaseName = "SSAS_Shared_01",
      TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
      TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
      TenantDatabaseProvisioningStatus provisioningStatus = TenantDatabaseProvisioningStatus.Ready) =>
      Registry.RecordsByTenant[tenantId] = new TenantDatabaseAssignmentRecord(
        tenantId, tenantDatabaseId, routingVersion, "PrimarySqlServer", databaseName,
        hostingMode, storageMode, provisioningStatus,
        TenantDatabaseConnectivityStatus.Healthy, Checked,
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, Checked,
        TenantDatabaseMigrationExecutionStatus.Idle,
        TenantDatabaseMigrationManagementMode.AutomaticByPlatform, null, null);

    private static readonly DateTimeOffset Checked = new(2026, 8, 16, 11, 0, 0, TimeSpan.Zero);
  }

  private sealed class StubRegistry : ITenantDatabaseRegistryReadRepository
  {
    public Dictionary<Guid, TenantDatabaseAssignmentRecord> RecordsByTenant { get; } = [];

    public int CallCount { get; private set; }

    public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      CallCount++;
      return Task.FromResult(RecordsByTenant.GetValueOrDefault(tenantId));
    }

    public Task<IReadOnlyList<TenantDatabaseDescriptor>> ListPhysicalDatabasesAsync(
      long afterId, int take, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantDatabaseDescriptor>>([]);
  }

  // Reads the SAME store the inner resolver reads, which is what makes these tests about the decorator's
  // logic rather than about two stubs that were arranged to disagree.
  private sealed class RegistryVersionReader(StubRegistry registry) : ITenantRoutingVersionReader
  {
    public bool Fails { get; set; }

    public int CallCount { get; private set; }

    public Task<Result<long>> ReadCurrentRoutingVersionAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      CallCount++;
      if (Fails)
      {
        return Task.FromResult(Result.Failure<long>(TenantStorageErrors.RoutingVersionUnavailable));
      }

      var record = registry.RecordsByTenant.GetValueOrDefault(tenantId);
      return Task.FromResult(record is null
        ? Result.Failure<long>(TenantStorageErrors.ActiveAssignmentMissing)
        : Result.Success(record.RoutingVersion));
    }
  }

  private sealed class MutableClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
  }
}
