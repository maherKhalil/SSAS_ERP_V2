using SSAS.BuildingBlocks.Localization;

namespace SSAS.Platform.Application.Localization;

public sealed record LocalizationResolutionRequest(
  string ResourceKey,
  string RequestedCulture,
  IReadOnlyDictionary<string, string>? PlaceholderValues = null,
  FormattingContext? FormattingContext = null);

public sealed record LocalizationExplicitBatchRequest(
  IReadOnlyCollection<string> ResourceKeys,
  string RequestedCulture,
  IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? PlaceholderValuesByResource = null,
  FormattingContext? FormattingContext = null);

public sealed record LocalizationGroupBatchRequest(
  string Module,
  string Group,
  string RequestedCulture,
  IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? PlaceholderValuesByResource = null,
  FormattingContext? FormattingContext = null);
