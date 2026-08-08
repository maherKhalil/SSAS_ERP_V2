using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Abstractions.Localization;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationMemoryCache : ILocalizationTenantCache, IDisposable
{
  public const int SizeLimit = 10_000;
  public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromMinutes(5);
  public static readonly TimeSpan VersionRevalidationInterval = TimeSpan.FromSeconds(15);
  public static readonly TimeSpan ValidationFailureGrace = TimeSpan.FromSeconds(60);
  private readonly ConcurrentDictionary<Guid, SemaphoreSlim> versionLocks = new();
  private readonly ConcurrentDictionary<PopulationKey, SemaphoreSlim> populationLocks = new();
  private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<OverrideCacheKey, byte>> tenantKeys = new();
  private readonly MemoryCache memoryCache;
  private readonly IDateTimeProvider clock;

  public LocalizationMemoryCache(IDateTimeProvider clock)
  {
    this.clock = clock;
    memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = SizeLimit });
  }

  public async Task<TenantLocalizationVersionState> GetVersionStateAsync(
    Guid tenantId,
    ITenantLocalizationVersionReader versionReader,
    CancellationToken cancellationToken = default)
  {
    var gate = versionLocks.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(cancellationToken);
    try
    {
      var now = clock.UtcNow;
      var key = new VersionCacheKey(tenantId);
      memoryCache.TryGetValue(key, out VersionValidationEntry? state);
      if (state is not null && now < state.NextValidationUtc)
      {
        return new TenantLocalizationVersionState(state.Version, TenantLocalizationCacheTrust.Trusted);
      }

      try
      {
        var version = await versionReader.ReadAsync(tenantId, cancellationToken);
        if (state is not null && state.Version != version)
        {
          EvictOverrideEntries(tenantId);
        }

        var refreshed = new VersionValidationEntry(version, now, now.Add(VersionRevalidationInterval));
        memoryCache.Set(key, refreshed, new MemoryCacheEntryOptions()
          .SetSize(1)
          .SetAbsoluteExpiration(AbsoluteLifetime));
        return new TenantLocalizationVersionState(version, TenantLocalizationCacheTrust.Trusted);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch
      {
        if (state is not null && now - state.LastSuccessfulValidationUtc <= ValidationFailureGrace)
        {
          return new TenantLocalizationVersionState(state.Version, TenantLocalizationCacheTrust.Grace);
        }

        EvictOverrideEntries(tenantId);
        return new TenantLocalizationVersionState(state?.Version ?? 1, TenantLocalizationCacheTrust.Degraded);
      }
    }
    finally
    {
      gate.Release();
      versionLocks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(tenantId, gate));
    }
  }

  public async Task<IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?>> GetOrCreateAsync(
    Guid tenantId,
    string culture,
    long catalogVersion,
    long tenantLocalizationVersion,
    IReadOnlyCollection<string> resourceKeys,
    Func<CancellationToken, Task<IReadOnlyList<TenantLocalizationOverrideReadModel>>> factory,
    CancellationToken cancellationToken = default)
  {
    var orderedKeys = resourceKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    var cached = ReadComplete(tenantId, culture, catalogVersion, tenantLocalizationVersion, orderedKeys);
    if (cached is not null)
    {
      return cached;
    }

    var populationKey = new PopulationKey(tenantId, culture, catalogVersion, string.Join('\n', orderedKeys));
    var gate = populationLocks.GetOrAdd(populationKey, static _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(cancellationToken);
    try
    {
      cached = ReadComplete(tenantId, culture, catalogVersion, tenantLocalizationVersion, orderedKeys);
      if (cached is not null)
      {
        return cached;
      }

      var loaded = await factory(cancellationToken);
      var byKey = loaded.ToDictionary(item => item.ResourceKey, StringComparer.Ordinal);
      var result = new Dictionary<string, TenantLocalizationOverrideReadModel?>(StringComparer.Ordinal);
      foreach (var resourceKey in orderedKeys)
      {
        byKey.TryGetValue(resourceKey, out var item);
        var cacheKey = new OverrideCacheKey(tenantId, culture, resourceKey, catalogVersion);
        var entry = new OverrideCacheEntry(Clone(item), tenantLocalizationVersion, clock.UtcNow);
        var options = new MemoryCacheEntryOptions()
          .SetSize(1)
          .SetAbsoluteExpiration(AbsoluteLifetime)
          .RegisterPostEvictionCallback((evictedKey, _, _, _) => RemoveIndexedKey((OverrideCacheKey)evictedKey));
        memoryCache.Set(cacheKey, entry, options);
        tenantKeys.GetOrAdd(tenantId, static _ => new ConcurrentDictionary<OverrideCacheKey, byte>())[cacheKey] = 0;
        result[resourceKey] = Clone(item);
      }

      return result;
    }
    finally
    {
      gate.Release();
      populationLocks.TryRemove(new KeyValuePair<PopulationKey, SemaphoreSlim>(populationKey, gate));
    }
  }

  public void EvictTenant(Guid tenantId)
  {
    EvictOverrideEntries(tenantId);
    memoryCache.Remove(new VersionCacheKey(tenantId));
  }

  public void Dispose() => memoryCache.Dispose();

  private Dictionary<string, TenantLocalizationOverrideReadModel?>? ReadComplete(
    Guid tenantId,
    string culture,
    long catalogVersion,
    long tenantLocalizationVersion,
    IReadOnlyCollection<string> resourceKeys)
  {
    var now = clock.UtcNow;
    var result = new Dictionary<string, TenantLocalizationOverrideReadModel?>(StringComparer.Ordinal);
    foreach (var resourceKey in resourceKeys)
    {
      var key = new OverrideCacheKey(tenantId, culture, resourceKey, catalogVersion);
      if (!memoryCache.TryGetValue(key, out OverrideCacheEntry? entry) ||
        entry is null ||
        entry.TenantLocalizationVersion != tenantLocalizationVersion ||
        now - entry.CreatedUtc >= AbsoluteLifetime)
      {
        memoryCache.Remove(key);
        return null;
      }

      result[resourceKey] = Clone(entry.Override);
    }

    return result;
  }

  private void EvictOverrideEntries(Guid tenantId)
  {
    if (!tenantKeys.TryRemove(tenantId, out var keys))
    {
      return;
    }

    foreach (var key in keys.Keys)
    {
      memoryCache.Remove(key);
    }
  }

  private void RemoveIndexedKey(OverrideCacheKey key)
  {
    if (!tenantKeys.TryGetValue(key.TenantId, out var keys))
    {
      return;
    }

    keys.TryRemove(key, out _);
    if (keys.IsEmpty)
    {
      tenantKeys.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<OverrideCacheKey, byte>>(key.TenantId, keys));
    }
  }

  private static TenantLocalizationOverrideReadModel? Clone(TenantLocalizationOverrideReadModel? item) => item is null
    ? null
    : item with
    {
      PlaceholderFingerprint = [.. item.PlaceholderFingerprint],
      CompatibilityFingerprint = [.. item.CompatibilityFingerprint]
    };

  private sealed record VersionCacheKey(Guid TenantId);
  private sealed record OverrideCacheKey(Guid TenantId, string Culture, string ResourceKey, long CatalogVersion);
  private sealed record PopulationKey(Guid TenantId, string Culture, long CatalogVersion, string ResourceKeys);
  private sealed record VersionValidationEntry(long Version, DateTimeOffset LastSuccessfulValidationUtc, DateTimeOffset NextValidationUtc);
  private sealed record OverrideCacheEntry(
    TenantLocalizationOverrideReadModel? Override,
    long TenantLocalizationVersion,
    DateTimeOffset CreatedUtc);
}
