namespace SSAS.Platform.Domain.Enums;

// A PLAN STOPS BEING SELLABLE WITHOUT BEING DELETED (FP-014).
//
// The ruling set is silent on plan lifecycle, and `domain-model.md` records the choice rather than leaving
// it to the build: historical `TenantSubscription` records point at a plan, and `REQ-SUB-0028`'s proration
// reconstructs what was in force on a past date. Deleting a plan a past record references would break that
// reconstruction, so a plan is RETIRED and never removed.
public enum SubscriptionPlanStatus
{
  Draft = 0,
  Active = 1,
  Retired = 2
}
