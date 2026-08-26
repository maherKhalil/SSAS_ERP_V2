namespace SSAS.Platform.Domain.Subscriptions;

// A KEYED CAP (FP-014, `OD-SUB-0017`).
//
// `Seats` is the first key. Keyed rather than a `SeatCap` column so a second cap needs no schema change and
// does not introduce a third notion of "limit". `long` because a limit may one day count storage bytes or
// API calls, and widening a value column later is the expensive kind of change.
public sealed class PlanLimit
{
  public const int KeyMaximumLength = 64;

  // The first limit key, named here so the string is not re-spelled at each call site.
  public const string Seats = "Seats";

  private PlanLimit(Guid subscriptionPlanId, string limitKey, long limitValue)
  {
    SubscriptionPlanId = subscriptionPlanId;
    LimitKey = limitKey;
    LimitValue = limitValue;
  }

  private PlanLimit() => LimitKey = string.Empty;

  public Guid SubscriptionPlanId { get; private set; }

  public string LimitKey { get; private set; }

  public long LimitValue { get; private set; }

  internal static PlanLimit For(Guid subscriptionPlanId, string limitKey, long limitValue) =>
    new(subscriptionPlanId, limitKey, limitValue);
}
