using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.BuildingBlocks.Localization.Generated;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.Localization.Events;
using PlatformLocalizationErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Tests.Localization;

public sealed class LocalizationDomainTests
{
  private static readonly Guid TenantId = Guid.Parse("7425200a-ee04-4f99-9089-7456bb2815ec");
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public void Settings_start_at_one_and_increment_checked_version()
  {
    var settings = TenantLocalizationSettings.Create(TenantId, LocalizationCulture.English);

    Assert.Equal(1, settings.TenantLocalizationVersion.Value);
    Assert.True(settings.IncrementVersion().IsSuccess);
    Assert.Equal(2, settings.TenantLocalizationVersion.Value);
  }

  [Fact]
  public void Create_appends_version_one_and_safe_event()
  {
    var aggregate = CreateOverride("Tenant save");

    Assert.True(aggregate.IsActive);
    Assert.Equal("Tenant save", aggregate.CurrentValue);
    Assert.Equal(1, aggregate.CurrentVersionNumber.Value);
    Assert.Single(aggregate.Versions);
    Assert.Equal(LocalizationChangeType.Created, aggregate.Versions.Single().ChangeType);
    Assert.IsType<TenantLocalizationOverrideCreated>(aggregate.DomainEvents.Single());
  }

  [Fact]
  public void Update_reactivates_same_identity_and_appends_history()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    var v1 = aggregate.Versions.Single().ToSnapshot();
    Assert.True(aggregate.RestoreDefault(
      v1, definition, "actor", Guid.NewGuid(), Now.AddMinutes(1), TenantLocalizationVersion.Create(3).Value, CatalogVersion.Create(1).Value).IsSuccess);
    var id = aggregate.Id;

    var update = aggregate.Update(
      definition,
      LocalizationText.Create("v3", definition.TextFormat).Value,
      "actor",
      Guid.NewGuid(),
      Now.AddMinutes(2),
      TenantLocalizationVersion.Create(4).Value,
      CatalogVersion.Create(1).Value);

    Assert.True(update.IsSuccess);
    Assert.Equal(id, aggregate.Id);
    Assert.True(aggregate.IsActive);
    Assert.Equal("v3", aggregate.CurrentValue);
    Assert.Equal(3, aggregate.CurrentVersionNumber.Value);
    Assert.Equal(3, aggregate.Versions.Count);
  }

  // ⚠ CITES THE SECOND CLAUSE OF `AC-LOC-0012` — *"…repeated Undo WALKS EXPLICIT LINEAGE."*
  //
  // Two undos in sequence, and the lineage is asserted on the RECORDED PRIOR rather than on the resulting
  // value: undoing to v2 writes version 4 with `PriorLogicalVersionNumber = 1`, and undoing again writes
  // version 5 with a NULL prior. **A implementation that simply stepped back through version NUMBERS would
  // produce the same `CurrentValue` at both points and record the wrong lineage** — which is why the text
  // assertions alone would not carry this clause.
  [Fact]
  [Trait("Criterion", "AC-LOC-0012")]
  public void Repeated_undo_walks_explicit_lineage()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    Update(aggregate, definition, "v2", 3);
    Update(aggregate, definition, "v3", 4);
    var currentV3 = aggregate.Versions.Single(version => version.VersionNumber.Value == 3).ToSnapshot();
    var targetV2 = aggregate.Versions.Single(version => version.VersionNumber.Value == 2).ToSnapshot();

    Assert.True(aggregate.Undo(
      currentV3, targetV2, TenantOverrideVersion.Create(2).Value, definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(5).Value, CatalogVersion.Create(1).Value).IsSuccess);
    Assert.Equal("v2", aggregate.CurrentValue);
    var undoV4 = aggregate.Versions.Single(version => version.VersionNumber.Value == 4).ToSnapshot();
    Assert.Equal(1, undoV4.PriorLogicalVersionNumber?.Value);

    var targetV1 = aggregate.Versions.Single(version => version.VersionNumber.Value == 1).ToSnapshot();
    Assert.True(aggregate.Undo(
      undoV4, targetV1, TenantOverrideVersion.Create(1).Value, definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(6).Value, CatalogVersion.Create(1).Value).IsSuccess);
    Assert.Equal("v1", aggregate.CurrentValue);
    Assert.Null(aggregate.Versions.Single(version => version.VersionNumber.Value == 5).PriorLogicalVersionNumber);
  }

  // ⚠ CITES THE TARGET HALF OF `AC-LOC-0012`'s FIRST CLAUSE — *"ONLY the advertised compatible lineage
  // predecessor plus matching rowversion succeeds."* Current is v3, the caller names v1, and the skip is
  // refused with `UndoTargetInvalid` while `CurrentVersionNumber` stays at 3 — refused AND nothing moved.
  //
  // ⚠⚠ THE ROWVERSION HALF OF THAT CLAUSE IS NOT ASSERTED HERE. The expected version passed in is the one
  // the skip would need, and the failure comes from the TARGET check — so this says nothing about a stale
  // token being refused. `AC-LOC-0035` names the exact codes for the stale case and is where that lives.
  [Fact]
  [Trait("Criterion", "AC-LOC-0012")]
  public void Undo_rejects_arbitrary_and_incompatible_target_without_skipping()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    Update(aggregate, definition, "v2", 3);
    Update(aggregate, definition, "v3", 4);
    var current = aggregate.Versions.Single(version => version.VersionNumber.Value == 3).ToSnapshot();
    var v1 = aggregate.Versions.Single(version => version.VersionNumber.Value == 1).ToSnapshot();

    var wrong = aggregate.Undo(
      current, v1, TenantOverrideVersion.Create(1).Value, definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(5).Value, CatalogVersion.Create(1).Value);

    Assert.Equal(PlatformLocalizationErrors.UndoTargetInvalid, wrong.Error);
    Assert.Equal(3, aggregate.CurrentVersionNumber.Value);
  }

  [Fact]
  public void Restore_default_is_a_deterministic_no_op_when_already_inactive()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    var current = aggregate.Versions.Single().ToSnapshot();
    Assert.True(aggregate.RestoreDefault(
      current, definition, "actor", Guid.NewGuid(), Now, TenantLocalizationVersion.Create(3).Value, CatalogVersion.Create(1).Value).IsSuccess);
    var eventCount = aggregate.DomainEvents.Count;
    var versionCount = aggregate.Versions.Count;

    var repeated = aggregate.RestoreDefault(
      aggregate.Versions.Last().ToSnapshot(), definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(4).Value, CatalogVersion.Create(1).Value);

    Assert.Equal(PlatformLocalizationErrors.OverrideAlreadyDefault, repeated.Error);
    Assert.Equal(eventCount, aggregate.DomainEvents.Count);
    Assert.Equal(versionCount, aggregate.Versions.Count);
    Assert.False(aggregate.IsActive);
    Assert.Null(aggregate.CurrentValue);
  }

  [Fact]
  public void Reactivation_can_be_undone_back_to_inactive_state()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    Assert.True(aggregate.RestoreDefault(
      aggregate.Versions.Single().ToSnapshot(), definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(3).Value, CatalogVersion.Create(1).Value).IsSuccess);
    var inactive = aggregate.Versions.Single(version => version.VersionNumber.Value == 2).ToSnapshot();
    Update(aggregate, definition, "reactivated", 4);
    var active = aggregate.Versions.Single(version => version.VersionNumber.Value == 3).ToSnapshot();

    Assert.True(aggregate.Undo(
      active, inactive, TenantOverrideVersion.Create(2).Value, definition, "actor", Guid.NewGuid(), Now,
      TenantLocalizationVersion.Create(5).Value, CatalogVersion.Create(1).Value).IsSuccess);
    Assert.False(aggregate.IsActive);
    Assert.Null(aggregate.CurrentValue);
  }

  [Fact]
  public void Security_sensitive_resource_cannot_create_override()
  {
    var definition = GetDefinition("platform.authentication.errors.authentication_failed");
    var result = TenantLocalizationOverride.Create(
      TenantId,
      LocalizationCulture.English,
      definition,
      LocalizationText.Create("custom", definition.TextFormat).Value,
      CatalogVersion.Create(1).Value,
      "actor",
      Guid.NewGuid(),
      Now,
      TenantLocalizationVersion.Create(2).Value);

    Assert.Equal(PlatformLocalizationErrors.SecuritySensitive, result.Error);
  }

  [Fact]
  public void Domain_events_expose_no_localized_or_placeholder_values()
  {
    var prohibited = new[] { "Text", "Value", "Placeholder", "Claim", "Credential", "Secret" };
    var eventTypes = new[]
    {
      typeof(TenantLocalizationOverrideCreated),
      typeof(TenantLocalizationOverrideUpdated),
      typeof(TenantLocalizationOverrideUndone),
      typeof(TenantLocalizationOverrideRestoredDefault)
    };

    Assert.All(eventTypes, type => Assert.DoesNotContain(
      type.GetProperties(),
      property => prohibited.Any(word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase))));
  }

  private static TenantLocalizationOverride CreateOverride(string value)
  {
    var definition = GetDefinition("platform.common.actions.save");
    return TenantLocalizationOverride.Create(
      TenantId,
      LocalizationCulture.English,
      definition,
      LocalizationText.Create(value, definition.TextFormat).Value,
      CatalogVersion.Create(1).Value,
      "actor",
      Guid.NewGuid(),
      Now,
      TenantLocalizationVersion.Create(2).Value).Value;
  }

  private static void Update(
    TenantLocalizationOverride aggregate,
    LocalizationResourceDefinition definition,
    string value,
    long tenantVersion)
  {
    Assert.True(aggregate.Update(
      definition,
      LocalizationText.Create(value, definition.TextFormat).Value,
      "actor",
      Guid.NewGuid(),
      Now,
      TenantLocalizationVersion.Create(tenantVersion).Value,
      CatalogVersion.Create(1).Value).IsSuccess);
  }

  private static LocalizationResourceDefinition GetDefinition(string key) =>
    GeneratedLocalizationCatalog.Instance.Resources.Single(resource => resource.ResourceKey.Value == key);
}
