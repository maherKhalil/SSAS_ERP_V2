using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class SubscriptionOverage : ValueObject
{
  public SubscriptionOverage(int excessSeats, decimal perSeatPrice, decimal totalOverageCharge)
  {
    ExcessSeats = excessSeats;
    PerSeatPrice = perSeatPrice;
    TotalOverageCharge = totalOverageCharge;
  }

  public int ExcessSeats { get; }

  public decimal PerSeatPrice { get; }

  public decimal TotalOverageCharge { get; }

  protected override IEnumerable<object> GetEqualityComponents()
  {
    yield return ExcessSeats;
    yield return PerSeatPrice;
    yield return TotalOverageCharge;
  }
}
