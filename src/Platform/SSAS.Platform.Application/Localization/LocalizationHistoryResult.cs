namespace SSAS.Platform.Application.Localization;

public sealed record LocalizationHistoryEntry(
  long VersionNumber,
  string? Value,
  bool IsActive,
  string ChangeType,
  long? PriorLogicalVersionNumber,
  long? UndoTargetVersionNumber,
  long CatalogVersion,
  int ResourceVersion,
  string ActorId,
  DateTimeOffset OccurredUtc);

public sealed record LocalizationHistoryResult(
  Guid OverrideId,
  string ResourceKey,
  string Culture,
  bool IsActive,
  long CurrentVersionNumber,
  long? EligibleUndoTargetVersion,
  byte[] RowVersion,
  IReadOnlyList<LocalizationHistoryEntry> Entries);
