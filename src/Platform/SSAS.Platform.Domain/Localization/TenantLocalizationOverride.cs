using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Domain.Localization.Events;

namespace SSAS.Platform.Domain.Localization;

public sealed class TenantLocalizationOverride : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
{
  public const int ActorMaximumLength = 256;
  private readonly List<TenantLocalizationOverrideVersion> versions = [];

  private TenantLocalizationOverride(
    Guid id,
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture)
    : base(id)
  {
    TenantId = tenantId;
    ResourceKey = resourceKey;
    Culture = culture;
    PlaceholderFingerprint = null!;
    CompatibilityFingerprint = null!;
  }

  private TenantLocalizationOverride()
    : base(Guid.Empty)
  {
    ResourceKey = null!;
    Culture = null!;
    PlaceholderFingerprint = null!;
    CompatibilityFingerprint = null!;
  }

  public Guid TenantId { get; private set; }

  public ResourceKey ResourceKey { get; private set; }

  public LocalizationCulture Culture { get; private set; }

  public LocalizationTextFormat TextFormat { get; private set; }

  public string? CurrentPlainTextValue { get; private set; }

  public string? CurrentMultilineTextValue { get; private set; }

  public string? CurrentValue => TextFormat == LocalizationTextFormat.PlainText
    ? CurrentPlainTextValue
    : CurrentMultilineTextValue;

  public bool IsActive { get; private set; }

  public TenantOverrideVersion CurrentVersionNumber { get; private set; }

  public CatalogVersion CatalogVersion { get; private set; }

  public ResourceVersion ResourceVersion { get; private set; }

  public PlaceholderFingerprint PlaceholderFingerprint { get; private set; }

  public CompatibilityFingerprint CompatibilityFingerprint { get; private set; }

  public IReadOnlyCollection<TenantLocalizationOverrideVersion> Versions => versions.AsReadOnly();

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public static Result<TenantLocalizationOverride> Create(
    Guid tenantId,
    LocalizationCulture culture,
    LocalizationResourceDefinition definition,
    LocalizationText value,
    CatalogVersion catalogVersion,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    TenantLocalizationVersion tenantLocalizationVersion)
  {
    if (tenantId == Guid.Empty || culture is null || definition is null || value is null)
    {
      return Result.Failure<TenantLocalizationOverride>(LocalizationErrors.OverrideMissing);
    }

    var validation = ValidateMutation(definition, value, actor);
    if (validation.IsFailure)
    {
      return Result.Failure<TenantLocalizationOverride>(validation.Error);
    }

    var aggregate = new TenantLocalizationOverride(Guid.NewGuid(), tenantId, definition.ResourceKey, culture);
    var version = TenantOverrideVersion.Create(1).Value;
    aggregate.ApplyState(value.Value, true, definition, catalogVersion);
    aggregate.CurrentVersionNumber = version;
    aggregate.versions.Add(TenantLocalizationOverrideVersion.Create(
      aggregate.Id,
      tenantId,
      definition.ResourceKey,
      culture,
      version,
      definition.TextFormat,
      value.Value,
      true,
      LocalizationChangeType.Created,
      null,
      null,
      catalogVersion,
      definition.ResourceVersion,
      definition.PlaceholderFingerprint,
      definition.CompatibilityFingerprint,
      actor,
      occurredUtc));
    aggregate.RaiseDomainEvent(new TenantLocalizationOverrideCreated(
      eventId,
      occurredUtc,
      tenantId,
      aggregate.Id,
      definition.ResourceKey.Value,
      culture.Value,
      version.Value,
      tenantLocalizationVersion.Value,
      catalogVersion.Value));
    return Result.Success(aggregate);
  }

  public Result Update(
    LocalizationResourceDefinition definition,
    LocalizationText value,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    TenantLocalizationVersion tenantLocalizationVersion,
    CatalogVersion catalogVersion)
  {
    var validation = ValidateMutation(definition, value, actor);
    if (validation.IsFailure)
    {
      return validation;
    }

    var next = CurrentVersionNumber.Increment();
    if (next.IsFailure)
    {
      return Result.Failure(next.Error);
    }

    var prior = CurrentVersionNumber;
    ApplyState(value.Value, true, definition, catalogVersion);
    CurrentVersionNumber = next.Value;
    versions.Add(TenantLocalizationOverrideVersion.Create(
      Id,
      TenantId,
      ResourceKey,
      Culture,
      next.Value,
      definition.TextFormat,
      value.Value,
      true,
      LocalizationChangeType.Updated,
      prior,
      null,
      catalogVersion,
      definition.ResourceVersion,
      definition.PlaceholderFingerprint,
      definition.CompatibilityFingerprint,
      actor,
      occurredUtc));
    RaiseDomainEvent(new TenantLocalizationOverrideUpdated(
      eventId,
      occurredUtc,
      TenantId,
      Id,
      ResourceKey.Value,
      Culture.Value,
      prior.Value,
      next.Value.Value,
      tenantLocalizationVersion.Value,
      catalogVersion.Value));
    return Result.Success();
  }

  public Result Undo(
    LocalizationVersionSnapshot current,
    LocalizationVersionSnapshot target,
    TenantOverrideVersion advertisedTarget,
    LocalizationResourceDefinition definition,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    TenantLocalizationVersion tenantLocalizationVersion,
    CatalogVersion catalogVersion)
  {
    if (!IsValidActor(actor) || current.VersionNumber != CurrentVersionNumber)
    {
      return Result.Failure(LocalizationErrors.UndoTargetInvalid);
    }

    var lineage = LocalizationUndoLineage.ValidateTarget(current, target, advertisedTarget, definition.CompatibilityFingerprint);
    if (lineage.IsFailure)
    {
      return lineage;
    }

    LocalizationText? targetText = null;
    if (target.IsActive)
    {
      var parsed = LocalizationText.Create(target.Value, definition.TextFormat);
      if (parsed.IsFailure || !parsed.Value.Placeholders.Matches(definition.Placeholders))
      {
        return Result.Failure(LocalizationErrors.UndoTargetIncompatible);
      }

      targetText = parsed.Value;
    }

    var next = CurrentVersionNumber.Increment();
    if (next.IsFailure)
    {
      return Result.Failure(next.Error);
    }

    var prior = CurrentVersionNumber;
    ApplyState(targetText?.Value, target.IsActive, definition, catalogVersion);
    CurrentVersionNumber = next.Value;
    versions.Add(TenantLocalizationOverrideVersion.Create(
      Id,
      TenantId,
      ResourceKey,
      Culture,
      next.Value,
      definition.TextFormat,
      targetText?.Value,
      target.IsActive,
      LocalizationChangeType.Undone,
      target.PriorLogicalVersionNumber,
      target.VersionNumber,
      catalogVersion,
      definition.ResourceVersion,
      definition.PlaceholderFingerprint,
      definition.CompatibilityFingerprint,
      actor,
      occurredUtc));
    RaiseDomainEvent(new TenantLocalizationOverrideUndone(
      eventId,
      occurredUtc,
      TenantId,
      Id,
      ResourceKey.Value,
      Culture.Value,
      prior.Value,
      next.Value.Value,
      target.VersionNumber.Value,
      tenantLocalizationVersion.Value,
      catalogVersion.Value));
    return Result.Success();
  }

  public Result RestoreDefault(
    LocalizationVersionSnapshot current,
    LocalizationResourceDefinition definition,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc,
    TenantLocalizationVersion tenantLocalizationVersion,
    CatalogVersion catalogVersion)
  {
    if (!IsActive)
    {
      return Result.Failure(LocalizationErrors.OverrideAlreadyDefault);
    }

    if (!IsValidActor(actor) || current.VersionNumber != CurrentVersionNumber)
    {
      return Result.Failure(LocalizationErrors.UndoTargetInvalid);
    }

    var next = CurrentVersionNumber.Increment();
    if (next.IsFailure)
    {
      return Result.Failure(next.Error);
    }

    var prior = CurrentVersionNumber;
    ApplyState(null, false, definition, catalogVersion);
    CurrentVersionNumber = next.Value;
    versions.Add(TenantLocalizationOverrideVersion.Create(
      Id,
      TenantId,
      ResourceKey,
      Culture,
      next.Value,
      definition.TextFormat,
      null,
      false,
      LocalizationChangeType.RestoredDefault,
      prior,
      null,
      catalogVersion,
      definition.ResourceVersion,
      definition.PlaceholderFingerprint,
      definition.CompatibilityFingerprint,
      actor,
      occurredUtc));
    RaiseDomainEvent(new TenantLocalizationOverrideRestoredDefault(
      eventId,
      occurredUtc,
      TenantId,
      Id,
      ResourceKey.Value,
      Culture.Value,
      prior.Value,
      next.Value.Value,
      tenantLocalizationVersion.Value,
      catalogVersion.Value));
    return Result.Success();
  }

  private static Result ValidateMutation(LocalizationResourceDefinition definition, LocalizationText value, string actor)
  {
    if (definition.Lifecycle != LocalizationResourceLifecycle.Active)
    {
      return Result.Failure(LocalizationErrors.ResourceRetired);
    }

    if (definition.SecurityClassification == LocalizationSecurityClassification.SecuritySensitiveNonOverridable)
    {
      return Result.Failure(LocalizationErrors.SecuritySensitive);
    }

    if (!definition.TenantOverridable)
    {
      return Result.Failure(LocalizationErrors.ResourceNotOverridable);
    }

    if (value.Format != definition.TextFormat || !value.Placeholders.Matches(definition.Placeholders))
    {
      return Result.Failure(SSAS.BuildingBlocks.Localization.LocalizationErrors.PlaceholderMismatch);
    }

    return IsValidActor(actor) ? Result.Success() : Result.Failure(LocalizationErrors.InvalidActor);
  }

  private void ApplyState(
    string? value,
    bool isActive,
    LocalizationResourceDefinition definition,
    CatalogVersion catalogVersion)
  {
    TextFormat = definition.TextFormat;
    IsActive = isActive;
    CurrentPlainTextValue = isActive && TextFormat == LocalizationTextFormat.PlainText ? value : null;
    CurrentMultilineTextValue = isActive && TextFormat == LocalizationTextFormat.MultilineText ? value : null;
    CatalogVersion = catalogVersion;
    ResourceVersion = definition.ResourceVersion;
    PlaceholderFingerprint = definition.PlaceholderFingerprint;
    CompatibilityFingerprint = definition.CompatibilityFingerprint;
  }

  private static bool IsValidActor(string? actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Length <= ActorMaximumLength;

  Guid ITenantOwnedEntity.TenantId { get => TenantId; set => TenantId = value; }
  DateTimeOffset IAuditableEntity.CreatedUtc { get => CreatedUtc; set => CreatedUtc = value; }
  DateTimeOffset IAuditableEntity.ModifiedUtc { get => ModifiedUtc; set => ModifiedUtc = value; }
  string? IAuditableEntity.CreatedBy { get => CreatedBy; set => CreatedBy = value; }
  string? IAuditableEntity.ModifiedBy { get => ModifiedBy; set => ModifiedBy = value; }
}
