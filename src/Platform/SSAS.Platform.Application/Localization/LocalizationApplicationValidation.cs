using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.BuildingBlocks.Localization.Catalog;
using SSAS.Platform.Domain.Localization;
using LocalizationErrors = SSAS.Platform.Domain.Localization.LocalizationErrors;

namespace SSAS.Platform.Application.Localization;

internal static class LocalizationApplicationValidation
{
  public static Result<(ResourceKey ResourceKey, LocalizationCulture Culture, LocalizationResourceDefinition Definition)>
    GetEditableDefinition(ILocalizationCatalog catalog, string resourceKeyValue, string cultureValue)
  {
    var resourceKey = ResourceKey.Create(resourceKeyValue);
    if (resourceKey.IsFailure)
    {
      return Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(resourceKey.Error);
    }

    var culture = LocalizationCulture.Create(cultureValue);
    if (culture.IsFailure)
    {
      return Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(culture.Error);
    }

    if (!catalog.TryGet(resourceKey.Value, out var definition))
    {
      return Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(LocalizationErrors.ResourceNotFound);
    }

    if (definition.Lifecycle != LocalizationResourceLifecycle.Active)
    {
      return Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(LocalizationErrors.ResourceRetired);
    }

    if (definition.SecurityClassification == LocalizationSecurityClassification.SecuritySensitiveNonOverridable)
    {
      return Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(LocalizationErrors.SecuritySensitive);
    }

    return definition.TenantOverridable
      ? Result.Success((resourceKey.Value, culture.Value, definition))
      : Result.Failure<(ResourceKey, LocalizationCulture, LocalizationResourceDefinition)>(LocalizationErrors.ResourceNotOverridable);
  }

  public static Result<LocalizationText> GetText(string value, LocalizationResourceDefinition definition)
  {
    var text = LocalizationText.Create(value, definition.TextFormat);
    if (text.IsFailure)
    {
      return text;
    }

    return text.Value.Placeholders.Matches(definition.Placeholders)
      ? text
      : Result.Failure<LocalizationText>(SSAS.BuildingBlocks.Localization.LocalizationErrors.PlaceholderMismatch);
  }
}
