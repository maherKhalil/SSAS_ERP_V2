using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// TS-1C resolver behaviour (ADR-017). Every negative case asserts a FAIL-CLOSED outcome: the resolver
// refuses to route rather than substituting another database.
public sealed class TenantDatabaseResolverTests
{
  private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Resolves_a_platform_managed_shared_assignment()
  {
    var resolver = ResolverFor(Record(TenantA, storageMode: TenantDatabaseStorageMode.Shared));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantA, result.Value.TenantId);
    Assert.Equal(25, result.Value.TenantDatabaseId);
    Assert.Equal("PrimarySqlServer", result.Value.ServerKey);
    Assert.Equal("SSAS_Shared_01", result.Value.DatabaseName);
    Assert.Equal(TenantDatabaseHostingMode.PlatformManaged, result.Value.HostingMode);
    Assert.Equal(TenantDatabaseStorageMode.Shared, result.Value.StorageMode);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Resolves_a_platform_managed_dedicated_assignment()
  {
    // Dedicated needs no distinct routing shape — ADR-017 already permits it and the metadata is identical.
    var resolver = ResolverFor(Record(TenantA, storageMode: TenantDatabaseStorageMode.Dedicated));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantDatabaseStorageMode.Dedicated, result.Value.StorageMode);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Route_carries_the_exact_routing_version()
  {
    // RoutingVersion is correctness metadata for future caching, not diagnostics — it must survive intact.
    var resolver = ResolverFor(Record(TenantA, routingVersion: 7));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsSuccess);
    Assert.Equal(7, result.Value.RoutingVersion);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_tenant_without_an_active_assignment_fails_closed()
  {
    var resolver = ResolverFor(null);

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ActiveAssignmentMissing.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task An_empty_tenant_id_fails_closed()
  {
    var resolver = ResolverFor(Record(TenantA));

    var result = await resolver.ResolveAsync(Guid.Empty);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantContextMissing.Code, result.Error.Code);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData(TenantDatabaseProvisioningStatus.Registered)]
  [InlineData(TenantDatabaseProvisioningStatus.Provisioning)]
  [InlineData(TenantDatabaseProvisioningStatus.Onboarding)]
  [InlineData(TenantDatabaseProvisioningStatus.Disabled)]
  public async Task A_database_that_is_not_ready_fails_closed(TenantDatabaseProvisioningStatus status)
  {
    var resolver = ResolverFor(Record(TenantA, provisioningStatus: status));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseNotReady.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-021")]
  public async Task Customer_managed_hosting_is_rejected_as_unsupported()
  {
    // Architecture-ready in the schema, deliberately absent at runtime. It must never be silently treated
    // as platform-managed or fall back to the Platform connection.
    var resolver = ResolverFor(Record(
      TenantA,
      hostingMode: TenantDatabaseHostingMode.CustomerManaged,
      storageMode: TenantDatabaseStorageMode.Dedicated));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.UnsupportedHostingMode.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_blank_server_key_fails_closed()
  {
    var resolver = ResolverFor(Record(TenantA, serverKey: "   "));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.ServerKeyNotConfigured.Code, result.Error.Code);
  }

  [Theory]
  [Trait("Decision", "ADR-017")]
  [InlineData("")]
  [InlineData("   ")]
  public async Task A_blank_database_name_fails_closed(string databaseName)
  {
    var resolver = ResolverFor(Record(TenantA, databaseName: databaseName));

    var result = await resolver.ResolveAsync(TenantA);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseNameInvalid.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task A_tenant_only_ever_resolves_its_own_assignment()
  {
    // The repository is keyed by the trusted tenant id; there is no path by which one tenant's call can
    // observe another's routing.
    var repository = new StubRepository
    {
      RecordsByTenant =
      {
        [TenantA] = Record(TenantA, tenantDatabaseId: 25, databaseName: "SSAS_A"),
        [TenantB] = Record(TenantB, tenantDatabaseId: 26, databaseName: "SSAS_B")
      }
    };
    var resolver = new TenantDatabaseResolver(repository);

    var routeA = await resolver.ResolveAsync(TenantA);
    var routeB = await resolver.ResolveAsync(TenantB);

    Assert.Equal("SSAS_A", routeA.Value.DatabaseName);
    Assert.Equal("SSAS_B", routeB.Value.DatabaseName);
    Assert.NotEqual(routeA.Value.TenantDatabaseId, routeB.Value.TenantDatabaseId);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task Each_call_reads_current_registry_state()
  {
    // No cache exists in TS-1C: a second call must observe changed state rather than a memoised route.
    var repository = new StubRepository
    {
      RecordsByTenant = { [TenantA] = Record(TenantA, databaseName: "SSAS_Before", routingVersion: 1) }
    };
    var resolver = new TenantDatabaseResolver(repository);

    var first = await resolver.ResolveAsync(TenantA);
    repository.RecordsByTenant[TenantA] = Record(TenantA, databaseName: "SSAS_After", routingVersion: 2);
    var second = await resolver.ResolveAsync(TenantA);

    Assert.Equal("SSAS_Before", first.Value.DatabaseName);
    Assert.Equal(1, first.Value.RoutingVersion);
    Assert.Equal("SSAS_After", second.Value.DatabaseName);
    Assert.Equal(2, second.Value.RoutingVersion);
    Assert.Equal(2, repository.CallCount);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task The_current_tenant_adapter_fails_closed_without_a_trusted_tenant()
  {
    var provider = new CurrentTenantDatabaseRouteProvider(
      new StubCurrentTenant(null), ResolverFor(Record(TenantA)));

    var result = await provider.ResolveCurrentAsync();

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantContextMissing.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task The_current_tenant_adapter_delegates_the_trusted_tenant()
  {
    var provider = new CurrentTenantDatabaseRouteProvider(
      new StubCurrentTenant(TenantA), ResolverFor(Record(TenantA)));

    var result = await provider.ResolveCurrentAsync();

    Assert.True(result.IsSuccess);
    Assert.Equal(TenantA, result.Value.TenantId);
  }

  private static TenantDatabaseResolver ResolverFor(TenantDatabaseAssignmentRecord? record)
  {
    var repository = new StubRepository();
    if (record is not null)
    {
      repository.RecordsByTenant[record.TenantId] = record;
    }

    return new TenantDatabaseResolver(repository);
  }

  private static TenantDatabaseAssignmentRecord Record(
    Guid tenantId,
    long tenantDatabaseId = 25,
    long routingVersion = 1,
    string serverKey = "PrimarySqlServer",
    string databaseName = "SSAS_Shared_01",
    TenantDatabaseHostingMode hostingMode = TenantDatabaseHostingMode.PlatformManaged,
    TenantDatabaseStorageMode storageMode = TenantDatabaseStorageMode.Shared,
    TenantDatabaseProvisioningStatus provisioningStatus = TenantDatabaseProvisioningStatus.Ready) =>
    new(tenantId, tenantDatabaseId, routingVersion, serverKey, databaseName, hostingMode, storageMode, provisioningStatus);

  private sealed class StubRepository : ITenantDatabaseRegistryReadRepository
  {
    public Dictionary<Guid, TenantDatabaseAssignmentRecord> RecordsByTenant { get; } = [];

    public int CallCount { get; private set; }

    public Task<TenantDatabaseAssignmentRecord?> FindActiveAssignmentAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      CallCount++;
      return Task.FromResult(RecordsByTenant.GetValueOrDefault(tenantId));
    }
  }

  private sealed class StubCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }
}
