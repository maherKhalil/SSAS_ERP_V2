using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class TenantSeatUsageSample : Entity<Guid>, IAppendOnlyEntity
{
  public TenantSeatUsageSample(Guid id, Guid tenantId, Guid tenantSubscriptionId, int observedSeatCount, DateTimeOffset observedAtUtc)
    : base(id)
  {
    TenantId = tenantId;
    TenantSubscriptionId = tenantSubscriptionId;
    ObservedSeatCount = observedSeatCount;
    ObservedAtUtc = observedAtUtc;
  }

  public Guid TenantId { get; }

  public Guid TenantSubscriptionId { get; }

  public int ObservedSeatCount { get; }

  public DateTimeOffset ObservedAtUtc { get; }
}
