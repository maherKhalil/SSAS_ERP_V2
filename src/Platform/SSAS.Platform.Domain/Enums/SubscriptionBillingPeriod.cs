namespace SSAS.Platform.Domain.Enums;

// A CLOSED SET, BECAUSE A PRICE IS PER PERIOD AND THE PERIOD IS PART OF THE KEY (FP-014, `OD-SUB-0015`).
public enum SubscriptionBillingPeriod
{
  Monthly = 0,
  Annual = 1
}
