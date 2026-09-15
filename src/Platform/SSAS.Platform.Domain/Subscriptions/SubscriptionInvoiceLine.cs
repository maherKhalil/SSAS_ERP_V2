using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class SubscriptionInvoiceLine : Entity<Guid>
{
  public SubscriptionInvoiceLine(Guid id, Guid subscriptionInvoiceId, Guid tenantSubscriptionId, decimal amount, string description)
    : base(id)
  {
    SubscriptionInvoiceId = subscriptionInvoiceId;
    TenantSubscriptionId = tenantSubscriptionId;
    Amount = amount;
    Description = description;
  }

  public Guid SubscriptionInvoiceId { get; }

  public Guid TenantSubscriptionId { get; }

  public decimal Amount { get; }

  public string Description { get; }
}
