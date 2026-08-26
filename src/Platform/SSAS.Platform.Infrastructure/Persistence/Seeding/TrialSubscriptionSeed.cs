using System.Globalization;
using System.Text;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Seeding;

// ==================================================================================================
// THE SEED, AS ONE STRING TWO CALLERS SHARE (FP-014, `DEC-L-034`, T-041).
// ==================================================================================================
//
// ---- WHY THE SQL LIVES HERE AND NOT INSIDE THE MIGRATION.
//
// A migration runs once, which makes "re-running the seed does not double-issue" untestable if the seed is
// only reachable through it. Holding the statement here lets the migration execute it **and** lets a test
// execute it twice against a real database and assert one subscription per tenant. There is exactly one
// copy of the SQL, so the thing proved idempotent is the thing that ships.
//
// ---- AND WHY IT IS BUILT FROM `TrialSubscription` RATHER THAN WRITTEN OUT.
//
// The plan id, the code, the name, the fourteen days, the currency and the module list appear here and in
// the C# issuer that runs at tenant creation. `DEC-L-034` requires **one rule for existing and new
// tenants**, and two hand-written copies of a rule are two rules that agree until someone edits one. These
// are interpolated from the single definition, so the SQL cannot drift from the code.
//
// Interpolating into SQL is the shape that usually deserves suspicion. **Every interpolated value here is a
// compile-time constant**; this seed accepts no argument and no runtime input, so there is nothing for a
// caller to inject through.
//
// ---- ITS RELATIONSHIP WITH `AddSubscriptionCommercialPlane`'s `THROW`, WHICH IS NOT A CONFLICT.
//
// That migration ends by counting plans, subscriptions and grants and **throwing if any exist**. This seed
// writes all three. Both are correct, because the `THROW` is a check on **one migration's own effect,
// evaluated at the foot of its own `Up` and inside its own transaction** -- not a standing constraint on
// the tables. It fires if a convenience backfill is ever added to *that file*, which is what it was built
// for; it cannot see, and is not meant to see, what a later migration does.
//
// The ordering is therefore deliberate and load-bearing rather than incidental: the plane is created empty
// and **proved** empty, and a separate, dated, reviewable migration fills it. A reader who finds only one
// of the two files is told about the other from both ends.
//
// ---- IDEMPOTENT PER ROW, NOT PER RUN.
//
// Every insert is guarded by its own `NOT EXISTS`. Re-running writes nothing new, and -- the case that
// actually matters -- a tenant that already holds a subscription is **left alone entirely**. Issuing a
// second one would not merely duplicate a row: `TenantSubscriptions` is append-only and monotonic, so a
// second record at a later instant would silently become the one in force and would replace whatever that
// tenant had actually agreed to.
//
// ---- NO STATUS FILTER ON `Tenants`, WHICH IS A DECISION.
//
// Suspended and archived tenants are seeded too. `OD-SUB-0010` ruled subscription state and `TenantStatus`
// **orthogonal**, and filtering here would be exactly the coupling it excluded -- with the failure landing
// on the tenant reactivated a year from now and found to be the only one in the estate holding nothing.
//
// ---- AND NO HISTORY IS RECONSTRUCTED.
//
// `EffectiveFromUtc` is the instant the seed runs, never the tenant's creation date. The subscription
// history is append-only and this writes its first record; **inventing an earlier start would be
// fabricating a commercial agreement for a period nobody agreed to one**, which is the same objection
// `CON-0001` raises to a default plan.
public static class TrialSubscriptionSeed
{
  public static readonly string Sql = Build();

  private static string Build()
  {
    var planId = TrialSubscription.PlanId.ToString("D", CultureInfo.InvariantCulture);
    var normalizedPlanCode = TrialSubscription.PlanCodeValue.ToUpperInvariant();
    var builder = new StringBuilder();

    builder.Append(CultureInfo.InvariantCulture, $"""
      -- FP-014 T-041: the 14-day all-module trial (`DEC-L-034`). Idempotent per row.
      DECLARE @now datetimeoffset(7) = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
      DECLARE @planId uniqueidentifier = '{planId}';

      -- 1. THE PLAN. `Active`, because a plan a tenant is actually on is not a draft.
      IF NOT EXISTS (SELECT 1 FROM [platform].[SubscriptionPlans] WHERE [SubscriptionPlanId] = @planId)
          INSERT INTO [platform].[SubscriptionPlans]
              ([SubscriptionPlanId], [PlanCode], [NormalizedPlanCode], [PlanName], [Status],
               [CreatedUtc], [CreatedBy])
          VALUES (@planId, N'{TrialSubscription.PlanCodeValue}', N'{normalizedPlanCode}',
                  N'{TrialSubscription.PlanNameValue}', N'Active', @now,
                  N'{TrialSubscription.SeedActor}');


      """);

    // 2. THE GRANTED MODULES -- every gateable key the product declares. An architecture test holds
    //    `TrialSubscription.ModuleKeys` against the descriptors, so "all modules" stays a fact rather than
    //    a list somebody maintained once.
    builder.AppendLine("-- 2. EVERY GATEABLE MODULE. \"All modules\" is the plan's whole content.");

    foreach (var moduleKey in TrialSubscription.ModuleKeys)
    {
      builder.Append(CultureInfo.InvariantCulture, $"""
        IF NOT EXISTS (SELECT 1 FROM [platform].[SubscriptionPlanModules]
                       WHERE [SubscriptionPlanId] = @planId AND [ModuleKey] = N'{moduleKey}')
            INSERT INTO [platform].[SubscriptionPlanModules] ([SubscriptionPlanId], [ModuleKey])
            VALUES (@planId, N'{moduleKey}');

        """);
    }

    // 3. THE PRICE, WHICH IS NOT DECORATION.
    //
    //    `REQ-SUB-0023` requires the plan to carry a price in the currency the tenant is billed in, and the
    //    caller assigning a subscription is meant to check it. Without this row the seeded subscription
    //    would violate that invariant on day one. Zero `XXX` is the honest statement of a free trial:
    //    `XXX` is ISO 4217's code for "no currency involved" (`DEC-L-040`), not a placeholder.
    builder.Append(CultureInfo.InvariantCulture, $"""

      -- 3. A PRICE OF ZERO IN `XXX`. `REQ-SUB-0023` requires a price in the billing currency.
      IF NOT EXISTS (SELECT 1 FROM [platform].[SubscriptionPlanPrices]
                     WHERE [SubscriptionPlanId] = @planId
                       AND [CurrencyCode] = N'{TrialSubscription.BillingCurrencyCode}'
                       AND [BillingPeriod] = N'Monthly')
          INSERT INTO [platform].[SubscriptionPlanPrices]
              ([SubscriptionPlanId], [CurrencyCode], [BillingPeriod], [Amount])
          VALUES (@planId, N'{TrialSubscription.BillingCurrencyCode}', N'Monthly', 0);


      """);

    // 4. THE MODULE CATALOG.
    //
    //    Nothing reads `ModuleDefinitions` yet -- the resolver reads plans, subscriptions and grants. It is
    //    seeded anyway because it is the only place in the database that says what the four granted keys
    //    MEAN and which of them are gateable (`REQ-SUB-0013`). A plan granting four bare names against an
    //    empty catalog is data that cannot be rendered in the plan list this trial was designed to sit in.
    builder.AppendLine(
      "-- 4. THE MODULE CATALOG -- what the granted keys mean, and that all four are gateable.");

    foreach (var (key, displayName) in TrialSubscription.ModuleCatalog)
    {
      builder.Append(CultureInfo.InvariantCulture, $"""
        IF NOT EXISTS (SELECT 1 FROM [platform].[ModuleDefinitions] WHERE [ModuleKey] = N'{key}')
            INSERT INTO [platform].[ModuleDefinitions]
                ([ModuleDefinitionId], [ModuleKey], [DisplayName], [IsGateable], [CreatedUtc], [CreatedBy])
            VALUES (NEWID(), N'{key}', N'{displayName}', 1, @now, N'{TrialSubscription.SeedActor}');

        """);
    }

    // 5. ONE SUBSCRIPTION PER TENANT THAT HOLDS NONE.
    builder.Append(CultureInfo.InvariantCulture, $"""

      -- 5. THE ISSUANCE. A tenant that already holds ANY subscription record is left untouched: this is
      --    the guard that makes a second run a no-op rather than a silent plan change.
      INSERT INTO [platform].[TenantSubscriptions]
          ([TenantSubscriptionId], [TenantId], [SubscriptionPlanId], [EffectiveFromUtc],
           [TermKind], [TermStartUtc], [TermEndUtc], [BillingCurrencyCode],
           [CreatedUtc], [ChangedBy], [ChangeReasonCode], [ChangeReasonText])
      SELECT NEWID(), tenant.[TenantId], @planId, @now,
             N'Fixed', @now, DATEADD(day, {TrialSubscription.TermDays}, @now),
             N'{TrialSubscription.BillingCurrencyCode}', @now,
             N'{TrialSubscription.SeedActor}', N'{TrialSubscription.ChangeReasonCode}',
             N'{TrialSubscription.ChangeReasonText}'
      FROM [platform].[Tenants] AS tenant
      WHERE NOT EXISTS (SELECT 1 FROM [platform].[TenantSubscriptions] AS existing
                        WHERE existing.[TenantId] = tenant.[TenantId]);
      """);

    return builder.ToString();
  }
}
