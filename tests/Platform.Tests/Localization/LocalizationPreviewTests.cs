using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationPreviewTests
{
  [Fact]
  public async Task Preview_reuses_catalog_text_and_placeholder_validation_without_persistence()
  {
    var handler = CreateHandler(TenantStatus.Active);

    var accepted = await handler.HandleAsync(new("platform.common.validation.required", "en", "Enter {fieldName}."));
    var rejected = await handler.HandleAsync(new("platform.common.validation.required", "en", "Enter {field}."));

    Assert.True(accepted.IsSuccess);
    Assert.Equal("ltr", accepted.Value.Direction);
    Assert.Equal(["fieldName"], accepted.Value.Placeholders);
    Assert.True(rejected.IsFailure);
    Assert.Equal("localization.placeholder_mismatch", rejected.Error.Code);
  }

  [Fact]
  public async Task Preview_requires_a_live_tenant_but_does_not_need_mutation_infrastructure()
  {
    var handler = CreateHandler(TenantStatus.Suspended);

    var result = await handler.HandleAsync(new("platform.common.actions.save", "ar", "حفظ"));

    Assert.True(result.IsFailure);
    Assert.Equal("localization.tenant_ineligible", result.Error.Code);
  }

  private static PreviewTenantLocalizationOverrideCommandHandler CreateHandler(TenantStatus status) => new(
    GeneratedLocalizationCatalog.Instance,
    new Eligibility(status),
    new Tenant(Guid.NewGuid()),
    new User());

  private sealed class Eligibility(TenantStatus status) : IRequestTenantEligibility
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, status));
  }

  private sealed class Tenant(Guid tenantId) : ICurrentTenant { public Guid? TenantId { get; } = tenantId; }

  private sealed class User : ICurrentUser
  {
    public string? UserId => "preview-user";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }
}
