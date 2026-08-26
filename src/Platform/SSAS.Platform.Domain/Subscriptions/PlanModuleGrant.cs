using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// WHICH MODULES A PLAN GRANTS. THE PAIR IS THE FACT, SO THERE IS NO SURROGATE (FP-014).
//
// Primary key `(SubscriptionPlanId, ModuleKey)`. A surrogate here would allow the same module to be granted
// twice by one plan, which is not a second fact -- it is the same fact recorded twice, and the composite key
// makes it unrepresentable rather than merely refused.
public sealed class PlanModuleGrant
{
  private PlanModuleGrant(Guid subscriptionPlanId, ModuleKey moduleKey)
  {
    SubscriptionPlanId = subscriptionPlanId;
    ModuleKey = moduleKey;
  }

  private PlanModuleGrant() => ModuleKey = null!;

  public Guid SubscriptionPlanId { get; private set; }

  public ModuleKey ModuleKey { get; private set; }

  internal static PlanModuleGrant For(Guid subscriptionPlanId, ModuleKey moduleKey) =>
    new(subscriptionPlanId, moduleKey);
}
