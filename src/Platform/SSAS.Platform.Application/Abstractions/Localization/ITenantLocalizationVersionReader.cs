namespace SSAS.Platform.Application.Abstractions.Localization;

public interface ITenantLocalizationVersionReader
{
  Task<long> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
