using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// THE REUSABLE COMMERCIAL DEFINITION (FP-014, `REQ-SUB-0002`, `REQ-SUB-0003`).
//
// Platform-global catalog data: `ADR-017` § Lookup classification puts subscription plans in class A,
// "stored in the Platform database, tenants cannot create global rows". The visible consequence is that the
// uniqueness key carries **no `TenantId`** — the one structural difference between this table and every
// tenant-owned table in the product.
//
// ---- MUTABLE, AND THAT IS NOT AN INCONSISTENCY WITH THE APPEND-ONLY HISTORY.
//
// A plan is a catalog entry, edited before and between uses; its changes are audited in the established
// shape. What must never be mutable is **the association between a tenant and a plan at a past instant**,
// and that lives on `TenantSubscription`, which is append-only. Confusing the two would either freeze a
// catalog nobody could maintain or let a price edit rewrite what a tenant was charged last March.
//
// ---- RETIRED, NEVER DELETED.
//
// Historical subscription records point here and `REQ-SUB-0028`'s proration reconstructs what was in force
// on a past date. A deleted plan breaks that reconstruction, so the lifecycle has no removal.
public sealed class SubscriptionPlan : AggregateRoot<Guid>
{
  private readonly List<PlanModuleGrant> moduleGrants = [];
  private readonly List<PlanLimit> limits = [];
  private readonly List<PlanPrice> prices = [];

  private string normalizedPlanCode = string.Empty;

  private SubscriptionPlan(
    Guid subscriptionPlanId,
    PlanCode planCode,
    PlanName planName,
    string actor,
    DateTimeOffset occurredUtc) : base(subscriptionPlanId)
  {
    PlanCode = planCode;
    normalizedPlanCode = planCode.NormalizedValue;
    PlanName = planName;
    Status = SubscriptionPlanStatus.Draft;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
  }

  private SubscriptionPlan() : base(Guid.Empty)
  {
    PlanCode = null!;
    PlanName = null!;
  }

  public Guid SubscriptionPlanId => Id;

  public PlanCode PlanCode { get; private set; }

  public string NormalizedPlanCode => normalizedPlanCode;

  public PlanName PlanName { get; private set; }

  public SubscriptionPlanStatus Status { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public string CreatedBy { get; private set; } = string.Empty;

  public DateTimeOffset? ModifiedUtc { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public IReadOnlyCollection<PlanModuleGrant> ModuleGrants => moduleGrants.AsReadOnly();

  public IReadOnlyCollection<PlanLimit> Limits => limits.AsReadOnly();

  public IReadOnlyCollection<PlanPrice> Prices => prices.AsReadOnly();

  public static Result<SubscriptionPlan> Create(
    PlanCode planCode, PlanName planName, string actor, DateTimeOffset occurredUtc)
  {
    if (planCode is null)
    {
      return Result.Failure<SubscriptionPlan>(SubscriptionErrors.InvalidPlanCode);
    }

    if (planName is null)
    {
      return Result.Failure<SubscriptionPlan>(SubscriptionErrors.InvalidPlanName);
    }

    return string.IsNullOrWhiteSpace(actor)
      ? Result.Failure<SubscriptionPlan>(SubscriptionErrors.InvalidActor)
      : Result.Success(new SubscriptionPlan(Guid.NewGuid(), planCode, planName, actor, occurredUtc));
  }

  // ---- THE GRANTED MODULE SET.
  //
  // A set, not a list: granting the same module twice is a caller error rather than a second fact, and the
  // composite primary key `(SubscriptionPlanId, ModuleKey)` in the schema says the same thing. The pair
  // **is** the fact, so there is no surrogate.
  public Result GrantModule(ModuleKey moduleKey, string actor, DateTimeOffset occurredUtc)
  {
    if (moduleKey is null)
    {
      return Result.Failure(SubscriptionErrors.InvalidModuleKey);
    }

    if (moduleGrants.Any(grant => grant.ModuleKey == moduleKey))
    {
      return Result.Failure(SubscriptionErrors.DuplicateModuleGrant);
    }

    moduleGrants.Add(PlanModuleGrant.For(Id, moduleKey));
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // ---- LIMITS ARE KEYED, NOT A `SeatCap` COLUMN (`OD-SUB-0017`).
  //
  // Seats are the first limit and will not be the last. A keyed cap means a second limit — storage bytes,
  // API calls — needs no schema change and does not introduce a third notion of "limit" beside the first
  // two. `bigint` for the same reason: widening a value column later is the expensive kind of change.
  public Result SetLimit(string limitKey, long limitValue, string actor, DateTimeOffset occurredUtc)
  {
    var trimmed = limitKey?.Trim();
    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > PlanLimit.KeyMaximumLength)
    {
      return Result.Failure(SubscriptionErrors.InvalidLimitKey);
    }

    if (limitValue < 0)
    {
      return Result.Failure(SubscriptionErrors.InvalidLimitValue);
    }

    if (limits.Any(limit => string.Equals(limit.LimitKey, trimmed, StringComparison.Ordinal)))
    {
      return Result.Failure(SubscriptionErrors.DuplicateLimit);
    }

    limits.Add(PlanLimit.For(Id, trimmed, limitValue));
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // ---- PRICE IS A COLLECTION BECAUSE `OD-SUB-0015` RULED MULTI-CURRENCY.
  //
  // Money is `decimal(19,4)` **inherited from `ADR-027`**, which four modules already use unchanged. This
  // package does not restate it as a decision of its own (`DEC-SUB-0008`).
  public Result SetPrice(
    string currencyCode,
    SubscriptionBillingPeriod billingPeriod,
    decimal amount,
    string actor,
    DateTimeOffset occurredUtc)
  {
    var normalized = currencyCode?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != PlanPrice.CurrencyCodeLength ||
      !normalized.All(char.IsLetter))
    {
      return Result.Failure(SubscriptionErrors.InvalidCurrencyCode);
    }

    if (amount < 0m)
    {
      return Result.Failure(SubscriptionErrors.InvalidAmount);
    }

    if (prices.Any(price =>
      string.Equals(price.CurrencyCode, normalized, StringComparison.Ordinal) &&
      price.BillingPeriod == billingPeriod))
    {
      return Result.Failure(SubscriptionErrors.DuplicatePrice);
    }

    prices.Add(PlanPrice.For(Id, normalized, billingPeriod, amount));
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  public Result Activate(string actor, DateTimeOffset occurredUtc)
  {
    if (Status == SubscriptionPlanStatus.Retired)
    {
      return Result.Failure(SubscriptionErrors.PlanRetired);
    }

    Status = SubscriptionPlanStatus.Active;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // Terminal by construction: there is no transition out of `Retired`, because the reason a plan is retired
  // rather than deleted is that past records still point at it.
  public Result Retire(string actor, DateTimeOffset occurredUtc)
  {
    Status = SubscriptionPlanStatus.Retired;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // True when this plan carries a price a tenant billed in the given currency could be charged. Checked by
  // the caller that assigns a subscription rather than by a constraint, because the check spans two
  // aggregates (`REQ-SUB-0023`).
  public bool HasPriceIn(string currencyCode) =>
    prices.Any(price => string.Equals(price.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase));

  private void Touch(string actor, DateTimeOffset occurredUtc)
  {
    ModifiedUtc = occurredUtc.ToUniversalTime();
    ModifiedBy = actor;
  }
}
