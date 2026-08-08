namespace SSAS.Platform.Application.Abstractions.Localization;

public enum TenantLocalizationCacheTrust
{
  Trusted,
  Grace,
  Degraded
}

public sealed record TenantLocalizationVersionState(long Version, TenantLocalizationCacheTrust Trust);

public interface ILocalizationTenantCache
{
  Task<TenantLocalizationVersionState> GetVersionStateAsync(
    Guid tenantId,
    ITenantLocalizationVersionReader versionReader,
    CancellationToken cancellationToken = default);

  Task<IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?>> GetOrCreateAsync(
    Guid tenantId,
    string culture,
    long catalogVersion,
    long tenantLocalizationVersion,
    IReadOnlyCollection<string> resourceKeys,
    Func<CancellationToken, Task<IReadOnlyList<TenantLocalizationOverrideReadModel>>> factory,
    CancellationToken cancellationToken = default);

  void EvictTenant(Guid tenantId);
}
