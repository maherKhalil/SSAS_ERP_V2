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
  IReadOnlyList<LocalizationHistoryEntry> Entries,
  int PageNumber = 1,
  int PageSize = 50,
  int TotalCount = 0)
{
  public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
