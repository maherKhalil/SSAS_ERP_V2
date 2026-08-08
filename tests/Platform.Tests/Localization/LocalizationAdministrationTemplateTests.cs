using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationAdministrationTemplateTests
{
  private static readonly Guid TenantId = Guid.Parse("bb401f10-3cdc-4c94-9245-1bcba39f61a7");

  [Fact]
  public async Task Administration_list_and_detail_preserve_effective_templates_without_placeholder_interpolation()
  {
    var currentTenant = new TestCurrentTenant(TenantId);
    var resolver = new LocalizationTextResolver(
      GeneratedLocalizationCatalog.Instance,
      EmptyOverrideReader.Instance,
      StaticVersionReader.Instance,
      PassthroughCache.Instance,
      ActiveEligibility.Instance,
      NoOpDiagnostics.Instance,
      currentTenant);
    var administration = EmptyAdministrationReadService.Instance;
    var listHandler = new ListTenantLocalizationResourcesQueryHandler(
      GeneratedLocalizationCatalog.Instance,
      administration,
      resolver,
      ActiveEligibility.Instance,
      currentTenant);
    var detailHandler = new GetTenantLocalizationResourceQueryHandler(
      GeneratedLocalizationCatalog.Instance,
      administration,
      resolver,
      ActiveEligibility.Instance,
      currentTenant);

    var list = await listHandler.HandleAsync(new ListTenantLocalizationResourcesQuery("en"));
    var detail = await detailHandler.HandleAsync(new GetTenantLocalizationResourceQuery(
      "platform.common.validation.required",
      "en"));

    Assert.True(list.IsSuccess);
    Assert.Equal(
      "{fieldName} is required.",
      list.Value.Items.Single(item => item.ResourceKey == "platform.common.validation.required").EffectiveValue);
    Assert.Equal(
      "Save",
      list.Value.Items.Single(item => item.ResourceKey == "platform.common.actions.save").EffectiveValue);
    Assert.True(detail.IsSuccess);
    Assert.Equal("{fieldName} is required.", detail.Value.Resource.EffectiveValue);
    Assert.Equal(["fieldName"], detail.Value.Resource.Placeholders);
  }

  private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class ActiveEligibility : IRequestTenantEligibility
  {
    public static ActiveEligibility Instance { get; } = new();

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
  }

  private sealed class EmptyAdministrationReadService : ITenantLocalizationAdministrationReadService
  {
    public static EmptyAdministrationReadService Instance { get; } = new();

    public Task<IReadOnlyList<TenantLocalizationOverrideAdministrationReadModel>> ReadAsync(
      Guid tenantId,
      LocalizationCulture culture,
      IReadOnlyCollection<ResourceKey> resourceKeys,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantLocalizationOverrideAdministrationReadModel>>([]);
  }

  private sealed class EmptyOverrideReader : ITenantLocalizationOverrideReadService
  {
    public static EmptyOverrideReader Instance { get; } = new();

    public Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> ReadAsync(
      Guid tenantId,
      LocalizationCulture culture,
      IReadOnlyCollection<ResourceKey> resourceKeys,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([]);
  }

  private sealed class StaticVersionReader : ITenantLocalizationVersionReader
  {
    public static StaticVersionReader Instance { get; } = new();

    public Task<long> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(1L);
  }

  private sealed class PassthroughCache : ILocalizationTenantCache
  {
    public static PassthroughCache Instance { get; } = new();

    public Task<TenantLocalizationVersionState> GetVersionStateAsync(
      Guid tenantId,
      ITenantLocalizationVersionReader versionReader,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new TenantLocalizationVersionState(1, TenantLocalizationCacheTrust.Trusted));

    public async Task<IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?>> GetOrCreateAsync(
      Guid tenantId,
      string culture,
      long catalogVersion,
      long tenantLocalizationVersion,
      IReadOnlyCollection<string> resourceKeys,
      Func<CancellationToken, Task<IReadOnlyList<TenantLocalizationOverrideReadModel>>> factory,
      CancellationToken cancellationToken = default)
    {
      var values = (await factory(cancellationToken)).ToDictionary(item => item.ResourceKey, StringComparer.Ordinal);
      return resourceKeys.ToDictionary(
        resourceKey => resourceKey,
        resourceKey => values.GetValueOrDefault(resourceKey),
        StringComparer.Ordinal);
    }

    public void EvictTenant(Guid tenantId)
    {
    }
  }

  private sealed class NoOpDiagnostics : ILocalizationDiagnostics
  {
    public static NoOpDiagnostics Instance { get; } = new();
    public void RecordMissingResource(string resourceKey)
    {
    }

    public void RecordDegradedTenant(Guid tenantId)
    {
    }
  }
}
