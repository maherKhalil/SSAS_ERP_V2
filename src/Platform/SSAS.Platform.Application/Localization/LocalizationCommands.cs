namespace SSAS.Platform.Application.Localization;

public sealed record CreateTenantLocalizationOverrideCommand(string ResourceKey, string Culture, string Value);

public sealed record UpdateTenantLocalizationOverrideCommand(
  string ResourceKey,
  string Culture,
  string Value,
  byte[] ExpectedRowVersion);

public sealed record UndoTenantLocalizationOverrideCommand(
  string ResourceKey,
  string Culture,
  long AdvertisedTargetVersion,
  byte[] ExpectedRowVersion);

public sealed record RestoreTenantLocalizationDefaultCommand(
  string ResourceKey,
  string Culture,
  byte[] ExpectedRowVersion);

public sealed record GetTenantLocalizationHistoryQuery(string ResourceKey, string Culture, int PageNumber = 1, int PageSize = 50);
