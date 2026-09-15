using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class SubscriptionPaymentAttempt : Entity<Guid>
{
  public SubscriptionPaymentAttempt(Guid id, Guid subscriptionInvoiceId, DateTimeOffset attemptedUtc, string outcome, string providerReference)
    : base(id)
  {
    SubscriptionInvoiceId = subscriptionInvoiceId;
    AttemptedUtc = attemptedUtc;
    Outcome = outcome;
    ProviderReference = providerReference;
  }

  public Guid SubscriptionInvoiceId { get; }

  public DateTimeOffset AttemptedUtc { get; }

  public string Outcome { get; }

  public string ProviderReference { get; }
}
