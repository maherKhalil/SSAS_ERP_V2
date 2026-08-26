using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// ==================================================================================================
// THE TRIAL IS A PLAN WITH A SHORT TERM. THERE IS NOTHING ELSE TO IT (FP-014, `DEC-L-034`, T-041).
// ==================================================================================================
//
// ---- WHAT IS DELIBERATELY ABSENT, AND WHY THE ABSENCE IS THE DESIGN.
//
// There is no `IsTrial`, no fourth `TenantStatus`, no flag on `TenantSubscription` and no branch anywhere
// that asks whether a subscription is a trial. `OD-SUB-0014` ruled a trial **is** a plan with a short term,
// and every one of those additions would have created a second way to be entitled beside the one
// `TenantEntitlementSnapshot` already resolves.
//
// The consequence is worth stating because it is what makes the ruling pay: **the day a customer buys, the
// only act is appending a subscription record naming a different plan.** No flag is cleared, no state is
// transitioned, nothing is migrated. The trial ends because a later record took effect, which is the same
// mechanism every plan change already uses.
//
// So this type declares **data, not behaviour**. It is the one place the trial's identity is written down,
// and both the things that issue it -- the cutover migration and tenant creation -- read it from here.
// `DEC-L-034` requires one rule for existing and new tenants; one definition is how that is kept true
// rather than asserted.
//
// ---- FOURTEEN DAYS, WHICH IS SHORT FOR AN ERP AND IS NOT AN OVERSIGHT.
//
// The owner ruled it. There is no grace period -- `DEC-L-009` ruled none -- and the consequence of the term
// running out is bounded by `DEC-L-033`: gated modules stop resolving and **login is untouched**. A lapsed
// tenant can still reach the platform plane, which is the surface a trial is converted from. Softening the
// term would trade a bounded, reversible consequence for a vaguer one.
//
// ---- THE BILLING CURRENCY IS `XXX`, AND THAT IS ISO 4217, NOT A SENTINEL.
//
// `TenantSubscription.BillingCurrencyCode` is mandatory: three letters, non-null in the schema. A free
// trial is billed in no currency, and the model has no way to say so. `XXX` is ISO 4217's own code for
// "no currency involved" -- a real code rather than a magic string -- so it needs no special case in the
// validation that already exists and reads correctly in a plan list. **The mandatory column is a finding
// about the model and is reported as one**; inventing a nullable column or a private sentinel here would
// have buried it.
public static class TrialSubscription
{
  // ---- A WELL-KNOWN IDENTITY, BECAUSE TWO WRITERS MUST NAME THE SAME ROW.
  //
  // The cutover migration creates this plan in SQL; tenant creation issues subscriptions against it in C#,
  // in a process that may never have run a migration. Looking it up by code would work and would also make
  // the plan's code load-bearing for correctness rather than for humans. A fixed id makes the two writers
  // agree by construction.
  public static readonly Guid PlanId = new("14d17a1a-0000-4000-8000-000000000014");

  public const string PlanCodeValue = "TRIAL-14D";

  // ---- A NAME A HUMAN READING A PLAN LIST UNDERSTANDS.
  //
  // A future administration surface lists this beside plans somebody actually bought. It says what it is
  // and how long it lasts, because the reader of that list is the person answering "why did this tenant
  // stop working".
  public const string PlanNameValue = "Trial - all modules, 14 days";

  public const int TermDays = 14;

  public const string BillingCurrencyCode = "XXX";

  // Recorded on every issued record, so the reason a tenant holds this plan is in the history rather than
  // inferred from the plan it names.
  public const string ChangeReasonCode = "TRIAL";

  public const string ChangeReasonText = "Automatic 14-day trial (DEC-L-034).";

  public const string SeedActor = "system:trial-seed";

  // ---- EVERY GATEABLE MODULE THE PRODUCT DECLARES, AND A TEST HOLDS THAT CLAIM.
  //
  // "All modules" is a promise about the product, not a list somebody maintained once. Platform cannot
  // reference the module assemblies, so the keys are written here -- and an architecture test asserts this
  // array equals the set the four `IModuleEnablementDescriptor` implementations declare. A fifth module
  // added to the product fails that test until it is added here, which is the drift that would otherwise
  // hand new tenants a trial silently missing a module.
  public static readonly string[] ModuleKeys = ["HR", "Finance.GL", "Payroll", "Attendance"];

  // Display names for the module catalog. The descriptors carry a key and nothing else -- deliberately, so
  // that a rename in a menu cannot un-entitle a tenant -- so the human-facing text is written here.
  public static readonly (string Key, string DisplayName)[] ModuleCatalog =
  [
    ("HR", "Human Resources"),
    ("Finance.GL", "Finance - General Ledger"),
    ("Payroll", "Payroll"),
    ("Attendance", "Attendance and Leave"),
  ];

  // The term a trial issued at `startUtc` runs for. One expression, so the migration's `DATEADD` and the
  // issuer's term cannot disagree about what "14 days" means.
  public static Result<SubscriptionTerm> TermFrom(DateTimeOffset startUtc) =>
    SubscriptionTerm.Fixed(startUtc, startUtc.AddDays(TermDays));
}
