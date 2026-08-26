using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// THE SPINE: AN APPEND-ONLY HISTORY, EXACTLY ONE IN FORCE (FP-014, `OD-SUB-0008`).
//
// ---- THERE IS NO CLOSING COLUMN, AND THAT IS THE WHOLE DESIGN.
//
// The obvious model — `EffectiveFromUtc` plus `EffectiveToUtc`, where a plan change closes the old row and
// opens a new one — is wrong here for a reason `EmployeePositionAssignment` already settled: closing an
// interval means **UPDATING the previous row**, which is precisely the history mutation append-only exists
// to prevent.
//
// So a record carries `EffectiveFromUtc` and nothing that closes it. "Exactly one in force" is not a column
// and not a flag: it is a **derived invariant** — the record in force at instant `T` is the one with the
// greatest `EffectiveFromUtc <= T`. A plan change appends; nothing is edited. That is also what makes
// `REQ-SUB-0028`'s proration computable — reconstructing a past entitlement is a query, not an audit-trail
// reading exercise.
//
// ---- `IAppendOnlyEntity` IS REAL ENFORCEMENT HERE, NOT DECORATION.
//
// `PlatformDbContext.PreventAppendOnlyMutation` refuses `Modified` and `Deleted` for anything marked this
// way, on **both** innermost save overloads. That guard did not exist when FP-014 was written — its absence
// was the finding that produced it — and this is the data it was built for.
//
// ---- NO `RowVersion`, NO `ModifiedUtc`, NO `ModifiedBy`, AND ALL THREE ABSENCES ARE THE SAME ABSENCE.
//
// The row is never updated, so there is no concurrency state to protect and no modification to record.
// `EmployeePositionAssignment` states it: "a record that is never updated has no concurrency state to
// protect". Adding any of the three would invite the update the guard refuses.
public sealed class TenantSubscription : Entity<Guid>, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;
  public const int ReasonCodeMaximumLength = 32;
  public const int ReasonTextMaximumLength = 512;
  public const int CurrencyCodeLength = 3;

  private TenantSubscription(
    Guid tenantSubscriptionId,
    Guid tenantId,
    Guid subscriptionPlanId,
    DateTimeOffset effectiveFromUtc,
    SubscriptionTerm term,
    string billingCurrencyCode,
    string changedBy,
    string? changeReasonCode,
    string? changeReasonText,
    DateTimeOffset occurredUtc) : base(tenantSubscriptionId)
  {
    TenantId = tenantId;
    SubscriptionPlanId = subscriptionPlanId;
    EffectiveFromUtc = effectiveFromUtc.ToUniversalTime();
    Term = term;
    BillingCurrencyCode = billingCurrencyCode;
    ChangedBy = changedBy;
    ChangeReasonCode = changeReasonCode;
    ChangeReasonText = changeReasonText;
    CreatedUtc = occurredUtc.ToUniversalTime();
  }

  private TenantSubscription() : base(Guid.Empty)
  {
    Term = null!;
    BillingCurrencyCode = string.Empty;
  }

  public Guid TenantSubscriptionId => Id;

  // The tenant is the SUBJECT of the agreement, never its owner (`DEC-SUB-0002`, `REQ-SUB-0004`). Both this
  // row and `Tenant` live in the Platform database, so this is an intra-database foreign key and is one.
  // `DEC-SUB-0009` bars **cross-database** keys; it does not bar this.
  public Guid TenantId { get; private set; }

  public Guid SubscriptionPlanId { get; private set; }

  // The only interval column.
  public DateTimeOffset EffectiveFromUtc { get; private set; }

  public SubscriptionTerm Term { get; private set; }

  // Which of the plan's currencies this tenant is billed in. Held on the RECORD rather than on the tenant,
  // so a currency change is a history event like any other rather than a silent overwrite.
  public string BillingCurrencyCode { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public string ChangedBy { get; private set; } = string.Empty;

  public string? ChangeReasonCode { get; private set; }

  public string? ChangeReasonText { get; private set; }

  // ==================================================================================================
  // THE MONOTONIC APPEND.
  // ==================================================================================================
  //
  // `currentMaximumEffectiveFromUtc` is the greatest `EffectiveFromUtc` already recorded for this tenant, or
  // null if this is the first record. The new record must take effect **strictly after** it.
  //
  // ---- WHY STRICTLY, AND WHY THIS IS NOT PEDANTRY.
  //
  // An append at or behind the present rewrites what was in force at a past instant. A metered overage
  // judged against that instant — `OD-SUB-0017` makes a cap a property of **the record live at that
  // moment**, not of the tenant — would change answer retroactively, and the invoice that was correct last
  // month would silently stop being correct. Append-only without monotonicity buys nothing: the row is
  // immutable and the *history* is not.
  //
  // ---- THE PARAMETER IS PASSED IN RATHER THAN QUERIED, ON PURPOSE.
  //
  // This type cannot reach the database, and a domain object that could would be a different kind of thing.
  // The caller reads the maximum inside the same transaction that appends. **That read is not sufficient on
  // its own** — two appends could each read the same maximum and both satisfy this rule — so the write also
  // takes a lock on the tenant row, and `(TenantId, EffectiveFromUtc)` is unique in the schema. Belt and
  // braces, deliberately: the lock makes the race rare, the constraint makes it impossible, and this check
  // makes the ordinary mistake a modelled error rather than a constraint violation surfacing as a 500.
  public static Result<TenantSubscription> Append(
    Guid tenantId,
    Guid subscriptionPlanId,
    DateTimeOffset effectiveFromUtc,
    DateTimeOffset? currentMaximumEffectiveFromUtc,
    SubscriptionTerm term,
    string? billingCurrencyCode,
    string actor,
    string? changeReasonCode,
    string? changeReasonText,
    DateTimeOffset occurredUtc)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidTenant);
    }

    if (subscriptionPlanId == Guid.Empty)
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidPlan);
    }

    if (term is null)
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidTerm);
    }

    if (string.IsNullOrWhiteSpace(actor) || actor.Length > ActorMaximumLength)
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidActor);
    }

    var currency = billingCurrencyCode?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(currency) || currency.Length != CurrencyCodeLength ||
      !currency.All(char.IsLetter))
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidCurrencyCode);
    }

    if (changeReasonCode is { Length: > ReasonCodeMaximumLength } ||
      changeReasonText is { Length: > ReasonTextMaximumLength })
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.InvalidActor);
    }

    var effectiveUtc = effectiveFromUtc.ToUniversalTime();

    if (currentMaximumEffectiveFromUtc is { } maximum && effectiveUtc <= maximum.ToUniversalTime())
    {
      return Result.Failure<TenantSubscription>(SubscriptionErrors.NonMonotonicAppend);
    }

    return Result.Success(new TenantSubscription(
      Guid.NewGuid(),
      tenantId,
      subscriptionPlanId,
      effectiveUtc,
      term,
      currency,
      actor,
      string.IsNullOrWhiteSpace(changeReasonCode) ? null : changeReasonCode.Trim(),
      string.IsNullOrWhiteSpace(changeReasonText) ? null : changeReasonText.Trim(),
      occurredUtc));
  }

  // Whether this record is the one in force at an instant is not answerable by the record alone — it
  // depends on every other record for the tenant. What the record CAN answer is whether it had taken effect.
  public bool HasTakenEffectAt(DateTimeOffset instant) => EffectiveFromUtc <= instant;

  // Expiry is read, never stored (`OD-SUB-0010`: orthogonal to `TenantStatus`, and expiry never writes it).
  public bool HasExpiredAt(DateTimeOffset instant) => Term.HasExpiredAt(instant);
}
