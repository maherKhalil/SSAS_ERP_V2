namespace SSAS.Platform.Application.Localization;

public sealed record LocalizationMutationResult(
  Guid OverrideId,
  long CurrentVersionNumber,
  long TenantLocalizationVersion,
  byte[] RowVersion,
  string? Value,
  bool IsActive);
