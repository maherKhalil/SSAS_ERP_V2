using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Subscriptions;

// ==================================================================================================
// THE OTHER HALF OF ONE RULE: THE TRIAL AT TENANT CREATION (FP-014, `DEC-L-034`, T-041).
// ==================================================================================================
//
// `AddTrialSubscriptionSeed` issues the trial to every tenant existing at cutover. This issues it to every
// tenant created afterwards. **`DEC-L-034` requires one rule, not two that agree** — so both read the same
// definition, `TrialSubscription`, and neither carries a plan id, a term length or a currency of its own.
//
// ---- IT IS THE SAME ROW, NOT AN EQUIVALENT ONE.
//
// Same plan (`TrialSubscription.PlanId`, a fixed identity precisely so two writers name one row), same
// 14-day fixed term, same `XXX` billing currency, same reason code. A tenant provisioned today and one
// provisioned before cutover are indistinguishable in this table, which is what makes the trial one thing
// to explain to a customer rather than two.
//
// ---- WHAT IT DOES NOT DO, AND THAT LIST IS THE DESIGN.
//
// It sets no flag, because there is none (`OD-SUB-0014`: a trial **is** a plan with a short term). It
// records no expiry date anywhere but the term, because expiry is read and never stored (`OD-SUB-0010`).
// It does not save — see below. And it does not touch `TenantStatus`: a trial ending takes gated modules
// and leaves the door open (`DEC-L-033`), so there is no lifecycle event here to record.
public interface ITrialSubscriptionIssuer
{
  Task<Result> IssueAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class TrialSubscriptionIssuer(
  ITenantSubscriptionRepository subscriptions,
  IDateTimeProvider clock) : ITrialSubscriptionIssuer
{
  // ---- IT ADDS TO THE CALLER'S UNIT OF WORK AND DOES NOT SAVE.
  //
  // `CreateTenantCommandHandler` adds the tenant, calls this, and saves once. **A tenant that exists
  // without a trial is the lockout this whole task exists to prevent**, and a single transaction makes that
  // state unrepresentable rather than merely unlikely — there is no window, no compensating write and no
  // background job to be behind. The foreign key from `TenantSubscriptions` to `Tenants` is satisfied
  // inside the same transaction because EF orders the inserts by dependency.
  public async Task<Result> IssueAsync(Guid tenantId, CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure(SubscriptionErrors.InvalidTenant);
    }

    // ---- THE DOUBLE-ISSUE REFUSAL, AND WHY IT IS NOT MERELY ABOUT DUPLICATE ROWS.
    //
    // A tenant already holding ANY subscription record is left alone. Appending a second one would not
    // just duplicate: the history is append-only and the record in force is **the one with the greatest
    // `EffectiveFromUtc`**, so a trial appended after a plan somebody bought would silently become the
    // plan they are on — downgrading a paying customer to a 14-day trial without a single error.
    var greatestEffectiveFromUtc =
      await subscriptions.GreatestEffectiveFromUtcAsync(tenantId, cancellationToken);

    if (greatestEffectiveFromUtc is not null)
    {
      return Result.Success();
    }

    // The plan is seeded by migration, so its absence is a deployment defect rather than a caller's
    // mistake. The foreign key would refuse the insert regardless and the transaction would roll back;
    // this exists so the failure names its remedy instead of arriving as a constraint violation.
    if (!await subscriptions.PlanExistsAsync(TrialSubscription.PlanId, cancellationToken))
    {
      return Result.Failure(SubscriptionErrors.TrialPlanMissing);
    }

    var now = clock.UtcNow;

    var term = TrialSubscription.TermFrom(now);
    if (term.IsFailure)
    {
      return Result.Failure(term.Error);
    }

    // `EffectiveFromUtc` is now, never the tenant's creation instant. This writes the first record of a
    // history; it does not reconstruct one (`OD-SUB-0008`).
    var record = TenantSubscription.Append(
      tenantId,
      TrialSubscription.PlanId,
      now,
      greatestEffectiveFromUtc,
      term.Value,
      TrialSubscription.BillingCurrencyCode,
      TrialSubscription.SeedActor,
      TrialSubscription.ChangeReasonCode,
      TrialSubscription.ChangeReasonText,
      now);

    if (record.IsFailure)
    {
      return Result.Failure(record.Error);
    }

    await subscriptions.AddAsync(record.Value, cancellationToken);
    return Result.Success();
  }
}
