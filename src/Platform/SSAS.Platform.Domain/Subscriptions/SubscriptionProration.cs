using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class SubscriptionProration : ValueObject
{
  public SubscriptionProration(decimal originalAmount, decimal proratedAmount, decimal ratio)
  {
    OriginalAmount = originalAmount;
    ProratedAmount = proratedAmount;
    Ratio = ratio;
  }

  public decimal OriginalAmount { get; }

  public decimal ProratedAmount { get; }

  public decimal Ratio { get; }

  protected override IEnumerable<object> GetEqualityComponents()
  {
    yield return OriginalAmount;
    yield return ProratedAmount;
    yield return Ratio;
  }
}
