namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationCatalogActivationException(string message) : InvalidOperationException(message);
