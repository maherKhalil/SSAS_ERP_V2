using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Localization;

public static class LocalizationResolutionErrors
{
  public static readonly Error ExplicitBatchTooLarge = new(
    "localization.explicit_batch_too_large",
    "An explicit localization batch may contain at most 100 unique resource keys.");
  public static readonly Error GroupBatchTooLarge = new(
    "localization.group_batch_too_large",
    "A localization resource group may contain at most 250 active resources.");
  public static readonly Error InvalidGroup = new(
    "localization.group_invalid",
    "A localization resource group requires an exact module and group name.");
}
