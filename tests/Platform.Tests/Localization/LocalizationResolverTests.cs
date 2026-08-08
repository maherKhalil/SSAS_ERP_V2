using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Localization.Events;
using SSAS.Platform.Infrastructure.Localization;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationResolverTests
{
  private static readonly Guid TenantId = Guid.Parse("9b7fc347-a31f-4724-8bf1-3dc83fac6c85");
  private static readonly DateTimeOffset InitialTime = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Anonymous_resolution_uses_defaults_formats_literally_and_hides_missing_key()
  {
    var fixture = new ResolverFixture(null, TenantStatus.Active);

    var required = await fixture.CreateResolver().ResolveAsync(new(
      "platform.common.validation.required",
      "en",
      new Dictionary<string, string>(StringComparer.Ordinal) { ["fieldName"] = "Email {{work}}" }));
    var missing = await fixture.CreateResolver().ResolveAsync(new("platform.unknown.key", "ar"));

    Assert.True(required.IsSuccess);
    Assert.Equal("Email {{work}} is required.", required.Value.Text);
    Assert.Equal(LocalizationResolutionSource.SystemDefault, required.Value.ResolutionSource);
    Assert.Equal(TextDirection.Ltr, required.Value.TextDirection);
    Assert.True(missing.IsSuccess);
    Assert.Equal(LocalizationResolutionSource.KeyFallback, missing.Value.ResolutionSource);
    Assert.DoesNotContain("platform.unknown.key", missing.Value.Text, StringComparison.Ordinal);
    Assert.Equal(TextDirection.Rtl, missing.Value.TextDirection);
    Assert.Equal(["platform.unknown.key"], fixture.Diagnostics.MissingKeys);
    Assert.Equal(0, fixture.OverrideReader.Calls);
  }

  [Fact]
  public async Task Active_tenant_batch_uses_one_override_query_and_never_overrides_sensitive_text()
  {
    var fixture = new ResolverFixture(TenantId, TenantStatus.Active);
    fixture.OverrideReader.Items =
    [
      CreateOverride("platform.common.actions.cancel", "Stop"),
      CreateOverride("platform.authentication.errors.authentication_failed", "Sensitive replacement")
    ];
    var resolver = fixture.CreateResolver();

    var result = await resolver.ResolveExplicitBatchAsync(new(
      [
        "platform.common.actions.cancel",
        "platform.authentication.errors.authentication_failed",
        "platform.common.actions.cancel"
      ],
      "en"));

    Assert.True(result.IsSuccess);
    Assert.Equal(2, result.Value.Count);
    Assert.Equal(
      ["platform.authentication.errors.authentication_failed", "platform.common.actions.cancel"],
      result.Value.Select(item => item.ResourceKey.Value));
    Assert.Equal(LocalizationResolutionSource.SystemDefault, result.Value[0].ResolutionSource);
    Assert.Equal(LocalizationResolutionSource.TenantOverride, result.Value[1].ResolutionSource);
    Assert.Equal("Stop", result.Value[1].Text);
    Assert.All(result.Value, item =>
    {
      Assert.Equal(GeneratedLocalizationCatalog.Instance.CatalogVersion, item.CatalogVersion);
      Assert.Equal(1, item.TenantLocalizationVersion!.Value.Value);
    });
    Assert.Equal(1, fixture.OverrideReader.Calls);
    Assert.Equal(1, fixture.Eligibility.Calls);
    Assert.Equal(1, fixture.VersionReader.Calls);
  }

  [Fact]
  public async Task Cached_override_is_bypassed_immediately_when_tenant_is_suspended()
  {
    var fixture = new ResolverFixture(TenantId, TenantStatus.Active);
    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.save", "Store")];
    var active = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal(LocalizationResolutionSource.TenantOverride, active.Value.ResolutionSource);

    fixture.Eligibility.Status = TenantStatus.Suspended;
    var suspended = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));

    Assert.Equal(LocalizationResolutionSource.SystemDefault, suspended.Value.ResolutionSource);
    Assert.Equal("Save", suspended.Value.Text);
    Assert.Equal(1, fixture.OverrideReader.Calls);
  }

  [Fact]
  public async Task Cache_population_is_single_flight_and_locks_are_reusable()
  {
    var clock = new FakeClock(InitialTime);
    using var cache = new LocalizationMemoryCache(clock);
    var calls = 0;
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var item = CreateOverride("platform.common.actions.save", "Store");

    async Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> Factory(CancellationToken cancellationToken)
    {
      Interlocked.Increment(ref calls);
      await release.Task.WaitAsync(cancellationToken);
      return [item];
    }

    var first = cache.GetOrCreateAsync(TenantId, "en", 1, 1, [item.ResourceKey], Factory);
    var second = cache.GetOrCreateAsync(TenantId, "en", 1, 1, [item.ResourceKey], Factory);
    release.SetResult();
    await Task.WhenAll(first, second);

    Assert.Equal(1, calls);
    cache.EvictTenant(TenantId);
    await cache.GetOrCreateAsync(TenantId, "en", 1, 1, [item.ResourceKey], Factory);
    Assert.Equal(2, calls);
  }

  [Fact]
  public async Task Version_revalidation_expiry_failure_grace_and_recovery_use_fake_time()
  {
    var fixture = new ResolverFixture(TenantId, TenantStatus.Active);
    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.save", "Store v1")];
    var first = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal("Store v1", first.Value.Text);
    Assert.Equal(1, fixture.VersionReader.Calls);

    fixture.Clock.Advance(TimeSpan.FromSeconds(14));
    var beforeDue = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal("Store v1", beforeDue.Value.Text);
    Assert.Equal(1, fixture.VersionReader.Calls);

    fixture.Clock.Advance(TimeSpan.FromSeconds(2));
    fixture.VersionReader.Version = 2;
    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.save", "Store v2", 2)];
    var changed = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal("Store v2", changed.Value.Text);
    Assert.Equal(2, fixture.VersionReader.Calls);
    Assert.Equal(2, fixture.OverrideReader.Calls);

    fixture.Clock.Advance(TimeSpan.FromMinutes(5));
    var expired = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal("Store v2", expired.Value.Text);
    Assert.Equal(3, fixture.OverrideReader.Calls);

    fixture.Clock.Advance(TimeSpan.FromSeconds(16));
    fixture.VersionReader.Fail = true;
    var grace = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal(LocalizationResolutionSource.TenantOverride, grace.Value.ResolutionSource);

    fixture.Clock.Advance(TimeSpan.FromSeconds(45));
    var degraded = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal(LocalizationResolutionSource.SystemDefault, degraded.Value.ResolutionSource);
    Assert.Contains(TenantId, fixture.Diagnostics.DegradedTenants);

    fixture.VersionReader.Fail = false;
    fixture.VersionReader.Version = 3;
    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.save", "Store v3", 3)];
    var recovered = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal("Store v3", recovered.Value.Text);
    Assert.Equal(LocalizationResolutionSource.TenantOverride, recovered.Value.ResolutionSource);
  }

  [Fact]
  public async Task Post_commit_domain_event_evicts_the_tenant_generation()
  {
    var fixture = new ResolverFixture(TenantId, TenantStatus.Active);
    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.cancel", "Stop v1")];
    var first = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.cancel", "en"));
    Assert.Equal("Stop v1", first.Value.Text);

    fixture.OverrideReader.Items = [CreateOverride("platform.common.actions.cancel", "Stop v2", 2)];
    fixture.VersionReader.Version = 2;
    var consumer = new LocalizationCacheDomainEventConsumer(fixture.Cache);
    await consumer.HandleAsync(
      new TenantLocalizationOverrideUpdated(
        Guid.NewGuid(), fixture.Clock.UtcNow, TenantId, Guid.NewGuid(),
        "platform.common.actions.cancel", "en", 1, 2, 2, 1),
      new DomainEventDispatchMetadata(string.Empty, null, null, null));
    var second = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.cancel", "en"));

    Assert.Equal("Stop v2", second.Value.Text);
    Assert.Equal(2, fixture.OverrideReader.Calls);
  }

  [Fact]
  public async Task Cache_keys_are_tenant_complete_and_incompatible_overrides_fall_back()
  {
    var clock = new FakeClock(InitialTime);
    using var cache = new LocalizationMemoryCache(clock);
    var firstTenant = Guid.NewGuid();
    var secondTenant = Guid.NewGuid();
    var firstItem = CreateOverride("platform.common.actions.save", "Tenant one");
    var secondItem = CreateOverride("platform.common.actions.save", "Tenant two");
    var first = await cache.GetOrCreateAsync(
      firstTenant, "en", 1, 1, [firstItem.ResourceKey], _ => Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([firstItem]));
    var second = await cache.GetOrCreateAsync(
      secondTenant, "en", 1, 1, [secondItem.ResourceKey], _ => Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([secondItem]));
    Assert.Equal("Tenant one", first[firstItem.ResourceKey]!.Value);
    Assert.Equal("Tenant two", second[secondItem.ResourceKey]!.Value);

    var fixture = new ResolverFixture(TenantId, TenantStatus.Active);
    var incompatible = CreateOverride("platform.common.actions.save", "Unsafe stale value") with
    {
      CompatibilityFingerprint = new byte[32]
    };
    fixture.OverrideReader.Items = [incompatible];
    var resolved = await fixture.CreateResolver().ResolveAsync(new("platform.common.actions.save", "en"));
    Assert.Equal(LocalizationResolutionSource.SystemDefault, resolved.Value.ResolutionSource);
    Assert.False(resolved.Value.OverrideCompatible);
  }

  [Fact]
  public async Task Group_batch_is_active_bounded_and_ordinally_ordered()
  {
    var fixture = new ResolverFixture(null, TenantStatus.Active);
    var result = await fixture.CreateResolver().ResolveGroupAsync(new("platform", "common.actions", "ar"));

    Assert.True(result.IsSuccess);
    Assert.Equal(
      ["platform.common.actions.cancel", "platform.common.actions.save"],
      result.Value.Select(item => item.ResourceKey.Value));
    Assert.All(result.Value, item => Assert.Equal(TextDirection.Rtl, item.TextDirection));
  }

  [Fact]
  public async Task Batch_limits_and_placeholder_contracts_are_enforced()
  {
    var fixture = new ResolverFixture(null, TenantStatus.Active);
    var tooMany = Enumerable.Range(0, 101).Select(index => $"platform.test.key_{index}").ToArray();

    var oversized = await fixture.CreateResolver().ResolveExplicitBatchAsync(new(tooMany, "en"));
    var missingPlaceholder = await fixture.CreateResolver().ResolveAsync(new(
      "platform.common.validation.required",
      "en"));
    var unknownPlaceholder = await fixture.CreateResolver().ResolveAsync(new(
      "platform.common.validation.required",
      "en",
      new Dictionary<string, string>(StringComparer.Ordinal)
      {
        ["fieldName"] = "Name",
        ["other"] = "Unexpected"
      }));

    Assert.Equal(LocalizationResolutionErrors.ExplicitBatchTooLarge, oversized.Error);
    Assert.Equal(SSAS.BuildingBlocks.Localization.LocalizationErrors.PlaceholderMismatch, missingPlaceholder.Error);
    Assert.Equal(SSAS.BuildingBlocks.Localization.LocalizationErrors.PlaceholderMismatch, unknownPlaceholder.Error);
  }

  [Fact]
  public void Runtime_effective_batch_limits_are_locked_to_the_approved_bounds()
  {
    Assert.Equal(100, LocalizationTextResolver.MaximumExplicitBatchSize);
    Assert.Equal(250, LocalizationTextResolver.MaximumGroupBatchSize);
  }

  private static TenantLocalizationOverrideReadModel CreateOverride(string key, string value, long version = 1)
  {
    Assert.True(GeneratedLocalizationCatalog.Instance.TryGet(ResourceKey.Create(key).Value, out var definition));
    return new TenantLocalizationOverrideReadModel(
      key,
      "en",
      definition.TextFormat,
      value,
      true,
      version,
      1,
      definition.ResourceVersion.Value,
      [.. definition.PlaceholderFingerprint.Bytes],
      [.. definition.CompatibilityFingerprint.Bytes]);
  }

  private sealed class ResolverFixture : IDisposable
  {
    public ResolverFixture(Guid? tenantId, TenantStatus status)
    {
      Clock = new FakeClock(InitialTime);
      Cache = new LocalizationMemoryCache(Clock);
      CurrentTenant = new FakeCurrentTenant(tenantId);
      Eligibility = new FakeEligibility(status);
    }

    public FakeClock Clock { get; }
    public LocalizationMemoryCache Cache { get; }
    public FakeOverrideReader OverrideReader { get; } = new();
    public FakeVersionReader VersionReader { get; } = new();
    public FakeDiagnostics Diagnostics { get; } = new();
    public FakeEligibility Eligibility { get; }
    public FakeCurrentTenant CurrentTenant { get; }

    public LocalizationTextResolver CreateResolver() => new(
      GeneratedLocalizationCatalog.Instance,
      OverrideReader,
      VersionReader,
      Cache,
      new RequestTenantEligibility(Eligibility),
      Diagnostics,
      CurrentTenant);

    public void Dispose() => Cache.Dispose();
  }

  private sealed class FakeOverrideReader : ITenantLocalizationOverrideReadService
  {
    public int Calls { get; private set; }
    public IReadOnlyList<TenantLocalizationOverrideReadModel> Items { get; set; } = [];

    public Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> ReadAsync(
      Guid tenantId,
      LocalizationCulture culture,
      IReadOnlyCollection<ResourceKey> resourceKeys,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>(
        Items.Where(item => resourceKeys.Any(key => string.Equals(key.Value, item.ResourceKey, StringComparison.Ordinal)))
          .ToArray());
    }
  }

  private sealed class FakeVersionReader : ITenantLocalizationVersionReader
  {
    public int Calls { get; private set; }
    public long Version { get; set; } = 1;
    public bool Fail { get; set; }

    public Task<long> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      Calls++;
      return Fail
        ? Task.FromException<long>(new InvalidOperationException("Simulated SQL validation failure."))
        : Task.FromResult(Version);
    }
  }

  private sealed class FakeEligibility(TenantStatus status) : ITenantAuthenticationEligibilityReadService
  {
    public TenantStatus Status { get; set; } = status;
    public int Calls { get; private set; }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, Status));
    }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) => GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class FakeDiagnostics : ILocalizationDiagnostics
  {
    public List<string> MissingKeys { get; } = [];
    public List<Guid> DegradedTenants { get; } = [];
    public void RecordMissingResource(string resourceKey) => MissingKeys.Add(resourceKey);
    public void RecordDegradedTenant(Guid tenantId) => DegradedTenants.Add(tenantId);
  }

  private sealed class FakeCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class FakeClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
    public void Advance(TimeSpan elapsed) => UtcNow = UtcNow.Add(elapsed);
  }
}
