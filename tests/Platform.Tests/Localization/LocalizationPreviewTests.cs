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
  // ⚠ CITES *VALIDATES FULLY* FROM `AC-LOC-0036` — *"Manage-only Preview VALIDATES FULLY yet
  // writes/caches/emits/logs nothing and returns encoded text only."*
  //
  // The validation half is asserted on both sides: a matching placeholder set is ACCEPTED and returns the
  // direction and the parsed placeholders, and a mismatched one is REJECTED with
  // `localization.placeholder_mismatch`. A preview that validated nothing would pass an accept-only test.
  //
  // ---- ⚠⚠ WHAT IS **NOT** ASSERTED HERE, AND THE DISTINCTION MATTERS MORE THAN THE CITATION.
  //
  // *Writes/caches nothing* is TRUE BY CONSTRUCTION rather than by assertion: `CreateHandler` supplies a
  // catalog, an eligibility check, a tenant and a user — **there is no repository and no unit of work to
  // call.** That is the same claim `LocalizationArchitectureTests.Preview_handler_has_no_infrastructure_or_
  // persistence_dependency` makes at the assembly level, one layer down at the constructor. Neither is an
  // observation that nothing was written.
  //
  // ⚠⚠⚠ AND *EMITS NOTHING* AND *LOGS NOTHING* ARE CARRIED BY NEITHER TEST — for the same reason the
  // audit-gate pair could not carry its own two: **THERE IS NO OBSERVABLE.** No event counter and no log
  // capture exists in this fixture, so *emits nothing* is not a missing `Assert` but a MISSING INSTRUMENT,
  // and the remedy is fixture work rather than another line here.
  //
  // ⚠ BOUND: eight other test files mention preview and were NOT examined for this criterion, so this is
  // *not carried by the two tests examined*, not *uncovered*. The search that settles it is those files.
  [Fact]
  [Trait("Criterion", "AC-LOC-0036")]
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
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }
}
