using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// THE COMPOSED GRAPH, NOT THE INTENDED ONE (ADR-020 "Resolver cache", TS-Storage Phase E2).
//
// Version-aware routing is only correct if the version-aware resolver is the one consumers actually receive
// and if the invalidator and the cache are the SAME object. Both of those are properties of the container,
// not of any type, so asserting them anywhere else would prove nothing: a graph that registered the two
// cache interfaces independently would compile, start, serve traffic and silently never invalidate.
public sealed class TenantRoutingRegistrationTests
{
  // ---- K. Every consumer of routing receives the version-aware boundary.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_registered_resolver_and_every_consumer_of_it_are_version_aware()
  {
    using var provider = BuildProvider();
    using var scope = provider.CreateScope();

    var resolver = scope.ServiceProvider.GetRequiredService<ITenantDatabaseResolver>();
    Assert.IsType<VersionAwareTenantDatabaseResolver>(resolver);

    // The tenant context factory is the path every routed read and write travels. If it held the bare
    // resolver, the cache would be bypassed for correctness and consulted for nothing.
    var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
    Assert.IsType<VersionAwareTenantDatabaseResolver>(ResolverHeldBy(factory));

    // ...and so is the request-path adapter.
    var routeProvider = scope.ServiceProvider.GetRequiredService<CurrentTenantDatabaseRouteProvider>();
    Assert.IsType<VersionAwareTenantDatabaseResolver>(ResolverHeldBy(routeProvider));
  }

  // NO SECOND ROUTING MECHANISM. The bare resolver stays reachable by its concrete type — the decorator
  // needs it — but nothing can obtain it through the interface consumers depend on.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_uncached_resolver_is_not_reachable_through_the_consumer_interface()
  {
    var services = Services();

    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(TenantDatabaseResolver) &&
      descriptor.Lifetime == ServiceLifetime.Scoped);

    // Exactly one ITenantDatabaseResolver registration, and it is not the bare one. A second registration
    // would silently win by being last, and the winner would be the one that skips the version check.
    var resolverRegistrations = services
      .Where(descriptor => descriptor.ServiceType == typeof(ITenantDatabaseResolver))
      .ToArray();
    Assert.Single(resolverRegistrations);
    Assert.Null(resolverRegistrations[0].ImplementationType);

    using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    Assert.IsNotType<TenantDatabaseResolver>(
      scope.ServiceProvider.GetRequiredService<ITenantDatabaseResolver>());
  }

  // ---- ONE CACHE, TWO FACES. Reading and invalidating must land on the same dictionary.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_route_cache_and_its_invalidator_are_the_same_singleton_instance()
  {
    using var provider = BuildProvider();

    var cache = provider.GetRequiredService<ITenantRoutingCache>();
    var invalidator = provider.GetRequiredService<ITenantRoutingCacheInvalidator>();

    Assert.Same(cache, invalidator);
    Assert.IsType<TenantRoutingMemoryCache>(cache);

    // Same instance across scopes, or an entry would never outlive the request that wrote it.
    using (var first = provider.CreateScope())
    using (var second = provider.CreateScope())
    {
      Assert.Same(cache, first.ServiceProvider.GetRequiredService<ITenantRoutingCache>());
      Assert.Same(cache, second.ServiceProvider.GetRequiredService<ITenantRoutingCache>());
    }

    // Proven behaviourally rather than by lifetime metadata alone: an eviction through the invalidator is
    // observable through the cache.
    cache.Store(
      TestTenant,
      new TenantRoutingCacheEntry(
        new TenantDatabaseRoute(
          TestTenant, 1, "PrimarySqlServer", "SSAS_Shared_01",
          SSAS.Platform.Domain.Enums.TenantDatabaseHostingMode.PlatformManaged,
          SSAS.Platform.Domain.Enums.TenantDatabaseStorageMode.Shared,
          1,
          new TenantDatabaseHealth(
            SSAS.Platform.Domain.Enums.TenantDatabaseConnectivityStatus.Healthy, null,
            SSAS.Platform.Domain.Enums.TenantDatabaseSchemaCompatibilityStatus.UpToDate, null,
            SSAS.Platform.Domain.Enums.TenantDatabaseMigrationExecutionStatus.Idle,
            SSAS.Platform.Domain.Enums.TenantDatabaseMigrationManagementMode.AutomaticByPlatform, null, null)),
        DateTimeOffset.UnixEpoch));
    Assert.Equal(1, cache.Count);

    invalidator.Invalidate(TestTenant);
    Assert.Equal(0, cache.Count);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_cache_is_a_singleton_and_the_version_reader_is_scoped()
  {
    var services = Services();

    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(ITenantRoutingCache) &&
      descriptor.Lifetime == ServiceLifetime.Singleton);
    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(ITenantRoutingCacheInvalidator) &&
      descriptor.Lifetime == ServiceLifetime.Singleton);

    // The reader follows the scoped PlatformDbContext it reads through; a singleton reader would capture a
    // disposed context, and a scoped cache would never survive to be read.
    Assert.Contains(services, descriptor =>
      descriptor.ServiceType == typeof(ITenantRoutingVersionReader) &&
      descriptor.Lifetime == ServiceLifetime.Scoped);
  }

  private static readonly Guid TestTenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

  private static object? ResolverHeldBy(object service) =>
    service.GetType()
      .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
      .Single(field => field.FieldType == typeof(ITenantDatabaseResolver))
      .GetValue(service);

  private static ServiceCollection Services()
  {
    var services = new ServiceCollection();
    services.AddSingleton<ICurrentUser, TestRequestContext>();
    services.AddSingleton<ICurrentTenant, TestRequestContext>();
    services.AddSingleton<ICorrelationContext, TestRequestContext>();
    services.AddSingleton<IRequestMetadata, TestRequestContext>();
    services.AddSingleton<IDateTimeProvider, TestRequestContext>();
    services.AddPlatformInfrastructure(new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:Platform"] =
          "Server=localhost;Database=not-opened;Integrated Security=True;TrustServerCertificate=True;Encrypt=False"
      })
      .Build());
    return services;
  }

  // Nothing here opens a connection: only the composition is under test.
  private static ServiceProvider BuildProvider() => Services().BuildServiceProvider();

  private sealed class TestRequestContext :
    ICurrentUser,
    ICurrentTenant,
    ICorrelationContext,
    IRequestMetadata,
    IDateTimeProvider
  {
    public string? UserId => "routing-registration-tests";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
    public Guid? TenantId => null;
    public string CorrelationId => "correlation";
    public string? RequestId => "request";
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
