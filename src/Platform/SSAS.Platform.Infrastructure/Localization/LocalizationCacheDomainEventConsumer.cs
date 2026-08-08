using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Domain.Localization.Events;

namespace SSAS.Platform.Infrastructure.Localization;

public sealed class LocalizationCacheDomainEventConsumer(ILocalizationTenantCache cache) : IDomainEventConsumer
{
  public Task HandleAsync(
    DomainEvent domainEvent,
    DomainEventDispatchMetadata metadata,
    CancellationToken cancellationToken = default)
  {
    var tenantId = domainEvent switch
    {
      TenantLocalizationOverrideCreated created => created.TenantId,
      TenantLocalizationOverrideUpdated updated => updated.TenantId,
      TenantLocalizationOverrideUndone undone => undone.TenantId,
      TenantLocalizationOverrideRestoredDefault restored => restored.TenantId,
      _ => (Guid?)null
    };
    if (tenantId.HasValue)
    {
      cache.EvictTenant(tenantId.Value);
    }

    return Task.CompletedTask;
  }
}
