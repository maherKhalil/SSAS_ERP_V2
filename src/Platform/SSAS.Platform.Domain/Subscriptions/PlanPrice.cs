using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Subscriptions;

// ONE PRICE PER CURRENCY PER BILLING PERIOD (FP-014, `OD-SUB-0015`).
//
// Multi-currency was ruled, so price is a COLLECTION rather than a scalar on the plan. `Amount` is
// `decimal(19,4)` inherited from `ADR-027` -- already activated, already inherited unchanged by GL, Payroll
// and Attendance, and deliberately not restated here as a decision of this package's own (`DEC-SUB-0008`).
public sealed class PlanPrice
{
  public const int CurrencyCodeLength = 3;

  private PlanPrice(
    Guid subscriptionPlanId, string currencyCode, SubscriptionBillingPeriod billingPeriod, decimal amount)
  {
    SubscriptionPlanId = subscriptionPlanId;
    CurrencyCode = currencyCode;
    BillingPeriod = billingPeriod;
    Amount = amount;
  }

  private PlanPrice() => CurrencyCode = string.Empty;

  public Guid SubscriptionPlanId { get; private set; }

  public string CurrencyCode { get; private set; }

  public SubscriptionBillingPeriod BillingPeriod { get; private set; }

  public decimal Amount { get; private set; }

  internal static PlanPrice For(
    Guid subscriptionPlanId, string currencyCode, SubscriptionBillingPeriod billingPeriod, decimal amount) =>
    new(subscriptionPlanId, currencyCode, billingPeriod, amount);
}
