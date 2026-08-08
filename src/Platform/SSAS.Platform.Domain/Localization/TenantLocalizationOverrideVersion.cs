using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Domain.Localization;

public sealed class TenantLocalizationOverrideVersion : Entity<Guid>, ITenantOwnedEntity
{
  private TenantLocalizationOverrideVersion(
    Guid id,
    Guid overrideId,
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    TenantOverrideVersion versionNumber,
    LocalizationTextFormat textFormat,
    string? value,
    bool isActive,
    LocalizationChangeType changeType,
    TenantOverrideVersion? priorLogicalVersionNumber,
    TenantOverrideVersion? undoTargetVersionNumber,
    CatalogVersion catalogVersion,
    ResourceVersion resourceVersion,
    PlaceholderFingerprint placeholderFingerprint,
    CompatibilityFingerprint compatibilityFingerprint,
    string actorId,
    DateTimeOffset occurredUtc)
    : base(id)
  {
    TenantLocalizationOverrideId = overrideId;
    TenantId = tenantId;
    ResourceKey = resourceKey;
    Culture = culture;
    VersionNumber = versionNumber;
    TextFormat = textFormat;
    IsActive = isActive;
    SetValue(value, textFormat, isActive);
    ChangeType = changeType;
    PriorLogicalVersionNumber = priorLogicalVersionNumber;
    UndoTargetVersionNumber = undoTargetVersionNumber;
    CatalogVersion = catalogVersion;
    ResourceVersion = resourceVersion;
    PlaceholderFingerprint = placeholderFingerprint;
    CompatibilityFingerprint = compatibilityFingerprint;
    ActorId = actorId;
    OccurredUtc = occurredUtc.ToUniversalTime();
  }

  private TenantLocalizationOverrideVersion()
    : base(Guid.Empty)
  {
    ResourceKey = null!;
    Culture = null!;
    PlaceholderFingerprint = null!;
    CompatibilityFingerprint = null!;
    ActorId = string.Empty;
  }

  public Guid TenantLocalizationOverrideId { get; private set; }

  public Guid TenantId { get; private set; }

  public ResourceKey ResourceKey { get; private set; }

  public LocalizationCulture Culture { get; private set; }

  public TenantOverrideVersion VersionNumber { get; private set; }

  public LocalizationTextFormat TextFormat { get; private set; }

  public string? PlainTextValue { get; private set; }

  public string? MultilineTextValue { get; private set; }

  public string? Value => TextFormat == LocalizationTextFormat.PlainText ? PlainTextValue : MultilineTextValue;

  public bool IsActive { get; private set; }

  public LocalizationChangeType ChangeType { get; private set; }

  public TenantOverrideVersion? PriorLogicalVersionNumber { get; private set; }

  public TenantOverrideVersion? UndoTargetVersionNumber { get; private set; }

  public CatalogVersion CatalogVersion { get; private set; }

  public ResourceVersion ResourceVersion { get; private set; }

  public PlaceholderFingerprint PlaceholderFingerprint { get; private set; }

  public CompatibilityFingerprint CompatibilityFingerprint { get; private set; }

  public string ActorId { get; private set; }

  public DateTimeOffset OccurredUtc { get; private set; }

  internal static TenantLocalizationOverrideVersion Create(
    Guid overrideId,
    Guid tenantId,
    ResourceKey resourceKey,
    LocalizationCulture culture,
    TenantOverrideVersion versionNumber,
    LocalizationTextFormat textFormat,
    string? value,
    bool isActive,
    LocalizationChangeType changeType,
    TenantOverrideVersion? priorLogicalVersionNumber,
    TenantOverrideVersion? undoTargetVersionNumber,
    CatalogVersion catalogVersion,
    ResourceVersion resourceVersion,
    PlaceholderFingerprint placeholderFingerprint,
    CompatibilityFingerprint compatibilityFingerprint,
    string actorId,
    DateTimeOffset occurredUtc) => new(
      Guid.NewGuid(),
      overrideId,
      tenantId,
      resourceKey,
      culture,
      versionNumber,
      textFormat,
      value,
      isActive,
      changeType,
      priorLogicalVersionNumber,
      undoTargetVersionNumber,
      catalogVersion,
      resourceVersion,
      placeholderFingerprint,
      compatibilityFingerprint,
      actorId,
      occurredUtc);

  public LocalizationVersionSnapshot ToSnapshot() => new(
    VersionNumber,
    Value,
    IsActive,
    TextFormat,
    PriorLogicalVersionNumber,
    UndoTargetVersionNumber,
    CatalogVersion,
    ResourceVersion,
    PlaceholderFingerprint,
    CompatibilityFingerprint);

  Guid ITenantOwnedEntity.TenantId { get => TenantId; set => TenantId = value; }

  private void SetValue(string? value, LocalizationTextFormat format, bool isActive)
  {
    PlainTextValue = isActive && format == LocalizationTextFormat.PlainText ? value : null;
    MultilineTextValue = isActive && format == LocalizationTextFormat.MultilineText ? value : null;
  }
}
