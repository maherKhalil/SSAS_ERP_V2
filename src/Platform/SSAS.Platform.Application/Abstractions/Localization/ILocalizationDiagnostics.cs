namespace SSAS.Platform.Application.Abstractions.Localization;

public interface ILocalizationDiagnostics
{
  void RecordMissingResource(string resourceKey);

  void RecordDegradedTenant(Guid tenantId);
}
