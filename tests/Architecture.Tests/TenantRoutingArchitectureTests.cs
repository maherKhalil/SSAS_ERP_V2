using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF VERSION-AWARE ROUTING (ADR-020 "Resolver cache", TS-Storage Phase E2).
//
// A route cache is the component most likely to become quietly authoritative. It sits on every request, it
// is fast, and every shortcut past the version check looks like a performance win right up to the cutover
// that writes a tenant's data into the database it was just moved off. These guards make the shortcuts
// inexpressible rather than merely discouraged.
public sealed class TenantRoutingArchitectureTests
{
  private static readonly Assembly ApplicationAssembly = typeof(VersionAwareTenantDatabaseResolver).Assembly;
  private static readonly Assembly InfrastructureAssembly = typeof(TenantCutoverWriteFence).Assembly;

  // The cache implementation is internal to Infrastructure — deliberately, since consumers depend on the
  // interfaces — so it is reached by name here rather than by making it public for a test's convenience.
  private static readonly Type MemoryCache = InfrastructureAssembly.GetType(
    "SSAS.Platform.Infrastructure.TenantStorage.TenantRoutingMemoryCache")!;

  // THE CENTRAL STRUCTURAL PROPERTY: nothing can read the cache without also holding the version reader.
  // This is what makes "serve the cached route without checking" impossible to write rather than merely
  // wrong — a component that wanted to would have to take the reader and then not call it.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_only_consumer_of_the_route_cache_also_consumes_the_authoritative_version_reader()
  {
    var consumers = ProductionTypes()
      .Where(type => Dependencies(type).Contains(typeof(ITenantRoutingCache)))
      .ToArray();

    Assert.Equal([typeof(VersionAwareTenantDatabaseResolver)], consumers);
    Assert.Contains(
      typeof(SSAS.Platform.Application.Abstractions.Persistence.ITenantRoutingVersionReader),
      Dependencies(typeof(VersionAwareTenantDatabaseResolver)));
  }

  // ONE ROUTING MECHANISM. The bare resolver still exists — the decorator wraps it — but no OTHER
  // implementation of the consumer interface may appear. A second one would win or lose by registration
  // order, and the one that skipped the version check would be the one that misroutes.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Exactly_two_resolvers_exist_and_only_the_version_aware_one_caches()
  {
    var resolvers = ProductionTypes()
      .Where(type => typeof(ITenantDatabaseResolver).IsAssignableFrom(type) && !type.IsInterface)
      .ToArray();

    Assert.Equal(
      [typeof(TenantDatabaseResolver), typeof(VersionAwareTenantDatabaseResolver)],
      resolvers.OrderBy(type => type.Name, StringComparer.Ordinal).ToArray());

    // The inner resolver reads the registry every time and knows nothing about caching, which is what keeps
    // "authoritative" and "remembered" from being the same code path.
    Assert.DoesNotContain(typeof(ITenantRoutingCache), Dependencies(typeof(TenantDatabaseResolver)));
    Assert.DoesNotContain(
      typeof(SSAS.Platform.Application.Abstractions.Persistence.ITenantRoutingVersionReader),
      Dependencies(typeof(TenantDatabaseResolver)));
  }

  // NO STALE-ROUTE FALLBACK IS EXPRESSIBLE. The cache exposes no bulk or predicate read, so there is no way
  // to ask it for "whatever we last knew" independently of a specific tenant and version.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_cache_surface_admits_no_bulk_read_and_no_fleet_wide_eviction()
  {
    var members = typeof(ITenantRoutingCache).GetMembers()
      .Concat(typeof(ITenantRoutingCacheInvalidator).GetMembers())
      .Select(member => member.Name)
      .ToArray();

    // Exact tenant only, in both directions.
    foreach (var forbidden in new[] { "Clear", "All", "Entries", "Keys", "Where", "Prefix", "Find" })
    {
      Assert.DoesNotContain(members, name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
    }

    // Every read and every eviction is keyed by a single tenant.
    foreach (var method in typeof(ITenantRoutingCache).GetMethods()
      .Concat(typeof(ITenantRoutingCacheInvalidator).GetMethods())
      .Where(method => !method.IsSpecialName))
    {
      Assert.Equal(typeof(Guid), method.GetParameters()[0].ParameterType);
    }
  }

  // NO EXTERNAL TRANSPORT. ADR-020's earlier draft permitted broadcast invalidation; V1 deliberately does
  // not, because correctness must not depend on a message that a starting, partitioned or restarting node
  // can miss. Convergence comes from the version check, so no broker is required — and none is present.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void No_external_invalidation_transport_is_referenced()
  {
    string[] brokers =
    [
      "StackExchange.Redis", "Microsoft.Extensions.Caching.StackExchangeRedis",
      "Azure.Messaging.ServiceBus", "RabbitMQ", "Confluent.Kafka", "MassTransit", "NServiceBus"
    ];

    foreach (var assembly in new[] { ApplicationAssembly, InfrastructureAssembly })
    {
      var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray();
      foreach (var broker in brokers)
      {
        Assert.DoesNotContain(referenced, name => name.StartsWith(broker, StringComparison.OrdinalIgnoreCase));
      }
    }

    // ...and the invalidator reaches nothing off-process: no HTTP, no sockets, no transport abstraction.
    Assert.NotNull(MemoryCache);
    foreach (var parameter in Dependencies(MemoryCache))
    {
      Assert.DoesNotContain("Microsoft.AspNetCore", parameter.Namespace ?? string.Empty, StringComparison.Ordinal);
      Assert.DoesNotContain("System.Net", parameter.Namespace ?? string.Empty, StringComparison.Ordinal);
    }
  }

  // E2 IS READ-SIDE ONLY. No routing flip, no copy engine: the routing types cannot reach the aggregates a
  // flip would have to mutate, so acquiring that ability is a visible change rather than a quiet one.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_routing_slice_neither_flips_routing_nor_copies_data()
  {
    foreach (var type in RoutingTypes())
    {
      Assert.DoesNotContain(typeof(TenantDatabaseAssignment), Dependencies(type));
      Assert.DoesNotContain(typeof(TenantDatabase), Dependencies(type));
    }

    // The copy engine and the destructive cleanup remain absent from the estate entirely.
    var unexpected = InfrastructureAssembly.GetTypes()
      .Where(type => !type.IsNested && type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true)
      .Where(type => DeferredComponents
        .Any(term => type.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(unexpected);
  }

  // NO SCHEMA CHANGE. Convergence is a read-time comparison against RoutingVersion, which the assignment
  // already carries — nothing about the cache is persisted, and a cache that had a table would be a second
  // authority rather than an optimisation.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Version_aware_routing_persists_nothing()
  {
    using var context = ModelOnlyPlatformContext();

    Assert.Null(context.Model.FindEntityType(typeof(TenantRoutingCacheEntry)));
    Assert.DoesNotContain(
      context.Model.GetEntityTypes(),
      entity => entity.ClrType.Name.Contains("RoutingCache", StringComparison.Ordinal));

    // The version this design turns on is a column that already exists on the assignment.
    var assignment = context.Model.FindEntityType(typeof(TenantDatabaseAssignment));
    Assert.NotNull(assignment);
    Assert.NotNull(assignment!.FindProperty(nameof(TenantDatabaseAssignment.RoutingVersion)));
  }

  // E1 IS NOT WEAKENED BY E2. The write freeze still sits on the persistence boundary; a routing change that
  // quietly removed it would leave the copy window unprotected.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_cutover_write_freeze_boundary_is_still_in_place()
  {
    var context = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext");
    Assert.NotNull(context);
    Assert.Contains(
      context!.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
      parameter => parameter.ParameterType == typeof(ITenantWriteFence));

    Assert.True(typeof(ITenantWriteFence).IsAssignableFrom(typeof(TenantCutoverWriteFence)));

    // The routed context factory still composes the fence, so a context obtained through version-aware
    // routing is still fenced.
    var factory = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContextFactory");
    Assert.NotNull(factory);
    Assert.Contains(Dependencies(factory!), parameter => parameter == typeof(ITenantWriteFence));
    Assert.Contains(Dependencies(factory!), parameter => parameter == typeof(ITenantDatabaseResolver));
  }

  // The cutover components E2 still does not deliver. Each is its own decision.
  private static readonly string[] DeferredComponents = ["Copy", "Cleanup", "Convergence"];

  private static IEnumerable<Type> RoutingTypes() =>
    ProductionTypes().Where(type => type.Name.Contains("Routing", StringComparison.Ordinal));

  private static IEnumerable<Type> ProductionTypes() =>
    new[] { ApplicationAssembly, InfrastructureAssembly }
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => !type.IsNested && type.Namespace?.Contains("TenantStorage", StringComparison.Ordinal) == true);

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  // Model metadata only — EF builds the model without opening the connection.
  private static PlatformDbContext ModelOnlyPlatformContext()
  {
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;
    return new PlatformDbContext(
      options, ModelOnlyUser.Instance, ModelOnlyTenant.Instance, ModelOnlyClock.Instance);
  }

  private sealed class ModelOnlyUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public static readonly ModelOnlyUser Instance = new();
    public string? UserId => "architecture-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public static readonly ModelOnlyTenant Instance = new();
    public Guid? TenantId => null;
  }

  private sealed class ModelOnlyClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public static readonly ModelOnlyClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
