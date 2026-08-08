namespace SSAS.BuildingBlocks.Localization;

public sealed record FormattingContext(
  string? TimeZone = null,
  string? DateCulture = null,
  string? NumberCulture = null,
  string? CurrencyCulture = null,
  string? CurrencyCode = null);
