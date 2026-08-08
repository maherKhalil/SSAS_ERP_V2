namespace SSAS.Platform.Application.Abstractions.Localization;

public enum LocalizationCatalogActivationOutcome
{
  Equal,
  Activated,
  DevelopmentLowerVersionWarning
}

public sealed record LocalizationCatalogActivationResult(
  LocalizationCatalogActivationOutcome Outcome,
  long LocalCatalogVersion,
  long HighestActivatedCatalogVersion);

public interface ILocalizationCatalogActivationService
{
  Task<LocalizationCatalogActivationResult> ActivateAsync(
    bool isProduction,
    CancellationToken cancellationToken = default);
}
