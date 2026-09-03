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

  // ⚠ CITES `AC-LOC-0014`'s SECOND CLAUSE — *"A later PUT requires expected rowversion and REACTIVATES THE
  // SAME AGGREGATE IDENTITY."* The arrangement is the criterion's: create, restore to default (inactive),
  // then update — *a later PUT after a restore* — and the aggregate comes back active with its history
  // continued rather than restarted.
  //
  // ⚠⚠ NOT THE FIRST CLAUSE. *Requires expected rowversion* is a handler concern —
  // `UpdateTenantLocalizationOverrideCommand` carries `ExpectedRowVersion` and the domain `Update` below
  // takes none — so nothing here can observe it.
  //
  // ⚠⚠⚠ AND THE ASSERTION THAT LOOKS LIKE THE IDENTITY CLAIM IS THE ONE THAT CANNOT FAIL.
  // `Assert.Equal(id, aggregate.Id)` compares a field of ONE INSTANCE with itself a few lines later; only
  // `Update` reassigning its own `Id` could break it, and nothing would. **It reads as the load-bearing
  // line and is the weakest in the test.**
  //
  // What actually carries *the same aggregate* is the pair below it: `IsActive` true (it was inactive after
  // the restore, so this is the REACTIVATION) and **`Versions.Count == 3` with `CurrentVersionNumber == 3`
  // — the update APPENDED to the existing lineage.** An implementation that abandoned the restored
  // aggregate and began a fresh one would produce an active override with `CurrentValue` `"v3"` and a
  // version count of ONE, passing every other assertion here.
  [Fact]
  [Trait("Criterion", "AC-LOC-0014")]
  public void Update_reactivates_same_identity_and_appends_history()
  {
    var aggregate = CreateOverride("v1");
    var definition = GetDefinition("platform.common.actions.save");
    var v1 = aggregate.Versions.Single().ToSnapshot();
    var originalVersionId = aggregate.Versions.Single().Id;
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

    // ⚠ ADDED, NOT SUBSTITUTED — the weak `Assert.Equal(id, aggregate.Id)` above is left where it is.
    //
    // `originalVersionId` is the ROW IDENTITY of the version written by `Create`, captured before the
    // restore. **Its survival is what *the same aggregate* means**, and it is the one fact a restarted
    // lineage cannot fake: a fresh aggregate would renumber from 1 and so would still satisfy any
    // assertion about version NUMBERS, but its rows carry new identities.
    //
    // ⚠⚠ I FIRST WROTE THIS AS `VersionNumber.Value == 1` AND IT WAS THE SAME DEFECT AS THE LINE IT WAS
    // MEANT TO REPAIR — a restarted aggregate has a version numbered 1 too. **The number is shared by both
    // implementations; only the identity separates them.**
    Assert.Contains(aggregate.Versions, version => version.Id == originalVersionId);
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
  // token being refused.
  //
  // ⚠⚠⚠ AND *ONLY X PLUS Y SUCCEEDS* MAKES THE REFUSALS COUNTABLE, so the residual is a fraction rather
  // than a hand-off: **the refusal population is THREE** — wrong predecessor with a good rowversion, good
  // predecessor with a stale one, and both wrong. **THIS TEST COVERS THE FIRST. 1 OF 3.**
  //
  // The second is *owned* by `AC-LOC-0035`, which names the exact codes for the stale case — ⚠ that is a
  // claim about WHERE IT BELONGS, not that it is covered, and I have not opened those tests. The third is
  // unexercised by anything I have read. **Saying *`AC-LOC-0035` owns it* would read as delegation and
  // delegation reads as coverage; the count does not.**
  //
  // ⚠ AND THE ORDERING IS WHY THE SECOND CANNOT BE REACHED FROM HERE: the target check fires first, so an
  // input that trips it NEVER EVALUATES the rowversion. Testing the second condition needs an input that
  // PASSES the first — a different input, not a stronger assertion.
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

  // ⚠ CITES `AC-LOC-0009`'s SECOND CLAUSE — *"All protected causes retain one generic code/ResourceKey and
  // CANNOT BE TENANT-OVERRIDDEN."* An authentication resource is handed to `Create` and the refusal is
  // `SecuritySensitive`.
  //
  // ⚠⚠ NOT THE FIRST CLAUSE. *Retain one generic code/ResourceKey* is a property of the auth error surface,
  // not of this refusal, and it is asserted in `LocalizationCatalogTests.Authentication_resources_are_non_
  // overridable_and_generic`.
  //
  // **That test and this one are the two halves of *cannot be overridden* and neither replaces the other**:
  // it asserts the catalog FLAG (`TenantOverridable` is false) and this asserts the BEHAVIOUR (the factory
  // refuses). A flag nothing consults would satisfy the first; a hard-coded refusal that ignored the flag
  // would satisfy this one.
  //
  // ⚠ ANTI-VACUITY, AND IT IS ALREADY IN THIS FILE RATHER THAN ADDED: a `Create` that refused every
  // resource would satisfy this test completely. `Create_appends_version_one_and_safe_event` and the
  // `CreateOverride` helper used by most tests here are the positives — an ordinary resource is overridden
  // successfully several lines above, so the refusal is known to be about THIS resource and not about
  // creation.
  [Fact]
  [Trait("Criterion", "AC-LOC-0009")]
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
