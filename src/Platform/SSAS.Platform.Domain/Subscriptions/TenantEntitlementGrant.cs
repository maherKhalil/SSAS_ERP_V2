using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Subscriptions;

// ADDITIVE GRANTS, AND ADDITIVE IS A SHAPE RATHER THAN A RULE (FP-014, `OD-SUB-0011`).
//
// A tenant may be granted a module or a raised cap **above** its plan, never below (`REQ-SUB-0010`). That
// covers pilots, negotiated deals and goodwill without letting an override silently remove something the
// customer is paying for.
//
// ---- THE ADDITIVE RULE IS ENFORCED TWICE, ON PURPOSE.
//
// **At write time**, `RaiseLimit` refuses a value at or below the plan's cap with a modelled error — loud,
// at the moment someone makes the mistake, naming both numbers.
//
// **At resolution time**, the cap is `max(plan, grants)` — see `TenantEntitlement` — so even a grant that
// somehow named a lower value **cannot lower anything**. The invariant is a property of the resolution
// function's shape rather than a rule a future author must remember.
//
// Neither half is redundant. Without the refusal the mistake is silently absorbed and nobody learns the
// grant did nothing; without the `max` the rule depends on every future write path remembering it.
//
// ---- APPEND-ONLY, FOR THE SAME REASON THE SUBSCRIPTION HISTORY IS.
//
// A grant that was in force last March must still be discoverable next March. Revocation is a later record,
// not an edit — and an expiring grant is not a mutation either, it is a value read at resolution time.
public sealed class TenantEntitlementGrant : Entity<Guid>, IAppendOnlyEntity
{
  public const int ActorMaximumLength = 256;
  public const int ReasonCodeMaximumLength = 32;
  public const int ReasonTextMaximumLength = 512;

  private TenantEntitlementGrant(
    Guid tenantEntitlementGrantId,
    Guid tenantId,
    EntitlementGrantKind grantKind,
    ModuleKey? moduleKey,
    string? limitKey,
    long? limitValue,
    DateTimeOffset effectiveFromUtc,
    DateTimeOffset? expiresUtc,
    string grantedBy,
    string? reasonCode,
    string? reasonText,
    DateTimeOffset occurredUtc) : base(tenantEntitlementGrantId)
  {
    TenantId = tenantId;
    GrantKind = grantKind;
    ModuleKey = moduleKey;
    LimitKey = limitKey;
    LimitValue = limitValue;
    EffectiveFromUtc = effectiveFromUtc.ToUniversalTime();
    ExpiresUtc = expiresUtc?.ToUniversalTime();
    GrantedBy = grantedBy;
    ReasonCode = reasonCode;
    ReasonText = reasonText;
    CreatedUtc = occurredUtc.ToUniversalTime();
  }

  private TenantEntitlementGrant() : base(Guid.Empty)
  {
  }

  public Guid TenantEntitlementGrantId => Id;

  public Guid TenantId { get; private set; }

  public EntitlementGrantKind GrantKind { get; private set; }

  // Set if and only if `ModuleGrant`. The schema `CHECK`s the same pairing, because a row that is neither
  // shape is a row the resolution function cannot interpret.
  public ModuleKey? ModuleKey { get; private set; }

  public string? LimitKey { get; private set; }

  public long? LimitValue { get; private set; }

  public DateTimeOffset EffectiveFromUtc { get; private set; }

  // Null means "until revoked by a later record". A pilot grant that ends is the ordinary case.
  public DateTimeOffset? ExpiresUtc { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public string GrantedBy { get; private set; } = string.Empty;

  public string? ReasonCode { get; private set; }

  public string? ReasonText { get; private set; }

  public static Result<TenantEntitlementGrant> GrantModule(
    Guid tenantId,
    ModuleKey moduleKey,
    DateTimeOffset effectiveFromUtc,
    DateTimeOffset? expiresUtc,
    string actor,
    string? reasonCode,
    string? reasonText,
    DateTimeOffset occurredUtc)
  {
    if (moduleKey is null)
    {
      return Result.Failure<TenantEntitlementGrant>(SubscriptionErrors.InvalidModuleKey);
    }

    var validation = Validate(tenantId, effectiveFromUtc, expiresUtc, actor, reasonCode, reasonText);
    return validation.IsFailure
      ? Result.Failure<TenantEntitlementGrant>(validation.Error)
      : Result.Success(new TenantEntitlementGrant(
        Guid.NewGuid(), tenantId, EntitlementGrantKind.ModuleGrant, moduleKey, null, null,
        effectiveFromUtc, expiresUtc, actor, Clean(reasonCode), Clean(reasonText), occurredUtc));
  }

  // ---- THE WRITE-TIME HALF OF THE ADDITIVE RULE.
  //
  // `planLimitValue` is the cap on the plan carried by the subscription record in force — the caller reads
  // it, because a grant cannot reach across aggregates. Null means the plan carries no such limit, in which
  // case ANY value raises: from "no cap defined" to a number is still additive in the only sense that
  // matters, since resolution treats a missing plan limit as nothing to exceed.
  public static Result<TenantEntitlementGrant> RaiseLimit(
    Guid tenantId,
    string? limitKey,
    long limitValue,
    long? planLimitValue,
    DateTimeOffset effectiveFromUtc,
    DateTimeOffset? expiresUtc,
    string actor,
    string? reasonCode,
    string? reasonText,
    DateTimeOffset occurredUtc)
  {
    var trimmedKey = limitKey?.Trim();
    if (string.IsNullOrWhiteSpace(trimmedKey) || trimmedKey.Length > PlanLimit.KeyMaximumLength)
    {
      return Result.Failure<TenantEntitlementGrant>(SubscriptionErrors.InvalidLimitKey);
    }

    if (limitValue < 0)
    {
      return Result.Failure<TenantEntitlementGrant>(SubscriptionErrors.InvalidLimitValue);
    }

    // The refusal. At or below the plan's cap is not a grant — it is either a no-op the caller believes did
    // something, or an attempt to lower, and `OD-SUB-0011` forbids the second outright.
    if (planLimitValue is { } planValue && limitValue <= planValue)
    {
      return Result.Failure<TenantEntitlementGrant>(SubscriptionErrors.GrantWouldNotRaise);
    }

    var validation = Validate(tenantId, effectiveFromUtc, expiresUtc, actor, reasonCode, reasonText);
    return validation.IsFailure
      ? Result.Failure<TenantEntitlementGrant>(validation.Error)
      : Result.Success(new TenantEntitlementGrant(
        Guid.NewGuid(), tenantId, EntitlementGrantKind.LimitRaise, null, trimmedKey, limitValue,
        effectiveFromUtc, expiresUtc, actor, Clean(reasonCode), Clean(reasonText), occurredUtc));
  }

  // In force at an instant: taken effect, and not yet expired. Both are reads.
  public bool IsInForceAt(DateTimeOffset instant) =>
    EffectiveFromUtc <= instant && (ExpiresUtc is null || instant <= ExpiresUtc);

  private static Result Validate(
    Guid tenantId,
    DateTimeOffset effectiveFromUtc,
    DateTimeOffset? expiresUtc,
    string actor,
    string? reasonCode,
    string? reasonText)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure(SubscriptionErrors.InvalidTenant);
    }

    if (string.IsNullOrWhiteSpace(actor) || actor.Length > ActorMaximumLength)
    {
      return Result.Failure(SubscriptionErrors.InvalidActor);
    }

    if (expiresUtc is { } expires && expires <= effectiveFromUtc)
    {
      return Result.Failure(SubscriptionErrors.InvalidTerm);
    }

    return reasonCode is { Length: > ReasonCodeMaximumLength } ||
      reasonText is { Length: > ReasonTextMaximumLength }
      ? Result.Failure(SubscriptionErrors.InvalidActor)
      : Result.Success();
  }

  private static string? Clean(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
