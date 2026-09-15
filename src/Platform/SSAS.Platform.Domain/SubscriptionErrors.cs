using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

// THE COMMERCIAL PLANE'S MODELLED FAILURES (FP-014).
//
// Separate from `TenantLifecycleErrors` because the two planes fail for different reasons and a caller
// handling one should not have to read past the other's vocabulary. Same shape and same naming rule.
public static class SubscriptionErrors
{
  public static readonly Error InvalidPlanCode = new("Subscription.InvalidPlanCode", "The plan code is invalid.");
  public static readonly Error InvalidPlanName = new("Subscription.InvalidPlanName", "The plan name is invalid.");
  public static readonly Error InvalidModuleKey = new("Subscription.InvalidModuleKey", "The module key is invalid.");
  public static readonly Error InvalidLimitKey = new("Subscription.InvalidLimitKey", "The limit key is invalid.");
  public static readonly Error InvalidLimitValue = new("Subscription.InvalidLimitValue", "A limit value must be zero or greater.");
  public static readonly Error InvalidCurrencyCode = new("Subscription.InvalidCurrencyCode", "The currency code must be a three-letter ISO 4217 code.");
  public static readonly Error InvalidAmount = new("Subscription.InvalidAmount", "A price amount cannot be negative.");
  public static readonly Error InvalidActor = new("Subscription.InvalidActor", "A trusted actor is required.");
  public static readonly Error InvalidTerm = new("Subscription.InvalidTerm", "The subscription term is invalid.");
  public static readonly Error PlanNotActive = new("Subscription.PlanNotActive", "Only an Active plan may be assigned.");
  public static readonly Error PlanRetired = new("Subscription.PlanRetired", "A retired plan cannot be reactivated.");
  public static readonly Error DuplicateModuleGrant = new("Subscription.DuplicateModuleGrant", "The plan already grants that module.");
  public static readonly Error DuplicateLimit = new("Subscription.DuplicateLimit", "The plan already carries that limit.");
  public static readonly Error DuplicatePrice = new("Subscription.DuplicatePrice", "The plan already carries a price for that currency and billing period.");
  public static readonly Error InvalidTenant = new("Subscription.InvalidTenant", "A tenant is required.");
  public static readonly Error InvalidPlan = new("Subscription.InvalidPlan", "A plan is required.");

  // ---- THE MONOTONIC-APPEND REFUSAL.
  //
  // An append at or behind the tenant's current maximum would rewrite what was in force at a past instant —
  // and a metered overage judged against that instant would change answer retroactively. `OD-SUB-0008`'s
  // append-only ruling is only meaningful if the append moves forward.
  public static readonly Error NonMonotonicAppend = new(
    "Subscription.NonMonotonicAppend",
    "A subscription record must take effect strictly after the tenant's current latest record.");

  // ---- THE ADDITIVE-GRANT REFUSAL (`OD-SUB-0011`, `DEC-L-009`).
  //
  // Refused at write time so the mistake is visible at the moment someone makes it.
  //
  // ---- ⚠⚠⚠ THE REDUNDANCY CLAIM BELOW WAS INVERTED. CORRECTED 2026-09-06; THE ORIGINAL IS KEPT BECAUSE
  // ---- IT IS THE RECORD OF WHAT WAS BELIEVED, AND A READER NEEDS TO KNOW THE CLAIM WAS MADE.
  //
  // THIS COMMENT PREVIOUSLY CONTINUED: *"Resolution ALSO takes `max(plan, grants)`, so a grant that somehow
  // named a lower value could not lower anything — the two are deliberate belt and braces, and this error is
  // the loud half."*
  //
  // ***THAT IS BACKWARDS. THIS ERROR IS NOT THE LOUD HALF; IT IS THE HALF THE PRODUCT NEVER REACHES.***
  //
  //   `TenantEntitlementGrant.RaiseLimit` (`:122`) is the only producer of this error, and **nothing in
  //   `src/` calls it** — the sole references are its own definition and two comments. It is exercised by
  //   `SubscriptionInvariantTests` and `TenantEntitlementResolutionTests` and by nothing the product runs.
  //   *Unit-tested is not reachable, and the tests are what make the absence easy to miss.*
  //
  //   `TenantEntitlement.cs:142-144`'s `Math.Max` is therefore not the backup. **It is the whole enforcement
  //   that executes.** That file already says so: *"remove the write-time check and this still cannot lower
  //   a cap."* The two comments disagreed and this one was wrong.
  //
  // ⚠⚠ ***DO NOT SIMPLIFY `Math.Max(current, granted)` AS REDUNDANT WITH THIS REFUSAL.*** Deleting it removes
  // the only lowering guard that runs, this error does not fire in its place, and **every test stays green** —
  // the failure is invisible in exactly the direction that makes the change look like tidying.
  //
  // Evidence and the full reasoning: `SubscriptionInvariantTests.cs:906-927`.
  //
  // ⚠ Whether `RaiseLimit` SHOULD be reachable is a product question (`AC-SUB-0016`), not a comment's to
  // settle. Nothing here changes behaviour.
  public static readonly Error GrantWouldNotRaise = new(
    "Subscription.GrantWouldNotRaise",
    "An entitlement grant may only raise a limit above the plan's value; it may never lower one.");

  // ---- THE SEED IS MISSING, WHICH IS A DEPLOYMENT DEFECT AND NOT A CALLER'S MISTAKE.
  //
  // `DEC-L-034`'s trial plan is created by `AddTrialSubscriptionSeed`. If it is absent the foreign key
  // would refuse the insert anyway and the whole transaction would roll back — so the tenant is safe either
  // way and only the DIAGNOSIS differs. It is modelled rather than left to a constraint violation because
  // "the trial plan has not been seeded" names the remedy and a foreign-key error does not.
  public static readonly Error TrialPlanMissing = new(
    "Subscription.TrialPlanMissing",
    "The trial subscription plan has not been seeded; apply the platform database migrations.");

  public static readonly Error GrantKindMismatch = new(
    "Subscription.GrantKindMismatch",
    "An entitlement grant must carry a module key or a limit, matching its kind, and never both.");
}
