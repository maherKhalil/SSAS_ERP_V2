using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.ValueObjects;

// THE TERM, AND WHY THE PERPETUAL MARKER IS A KIND RATHER THAN A NULL (FP-014, `OD-SUB-0009`).
//
// The owner ruled "a term exists, with a start and an end **or an explicit perpetual marker**". A nullable
// `EndUtc` on its own carries both readings — perpetual, and not-yet-set — and they are not the same fact:
// one means the tenant is never locked out on a date, the other means nobody has said. `OD-SUB-0009` also
// made expiry the **only** commercial event that refuses login, so the difference decides whether a whole
// tenant can sign in.
//
// This type therefore carries a closed `SubscriptionTermKind` alongside the nullable end and **refuses the
// two incoherent combinations at construction**, which is what makes the marker explicit rather than
// inferred. The same pair is `CHECK`-constrained in the schema; neither is redundant, because the domain
// stops the mistake being made and the constraint stops it arriving by any other path.
public sealed class SubscriptionTerm : ValueObject
{
  private SubscriptionTerm(SubscriptionTermKind kind, DateTimeOffset startUtc, DateTimeOffset? endUtc)
  {
    Kind = kind;
    StartUtc = startUtc;
    EndUtc = endUtc;
  }

  public SubscriptionTermKind Kind { get; }

  public DateTimeOffset StartUtc { get; }

  // Null if and only if the term is perpetual. The "if and only if" is enforced, not documented.
  public DateTimeOffset? EndUtc { get; }

  public static Result<SubscriptionTerm> Fixed(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
    endUtc <= startUtc
      ? Result.Failure<SubscriptionTerm>(SubscriptionErrors.InvalidTerm)
      : Result.Success(new SubscriptionTerm(
        SubscriptionTermKind.Fixed, startUtc.ToUniversalTime(), endUtc.ToUniversalTime()));

  public static SubscriptionTerm Perpetual(DateTimeOffset startUtc) =>
    new(SubscriptionTermKind.Perpetual, startUtc.ToUniversalTime(), null);

  // ---- REHYDRATION, AND WHY IT VALIDATES RATHER THAN TRUSTS.
  //
  // EF materialises through this, so a row written before a constraint existed — or by a path that bypassed
  // the domain — would otherwise become an object the rest of the model believes is valid. It refuses the
  // same two combinations the factories do.
  public static Result<SubscriptionTerm> Rehydrate(
    SubscriptionTermKind kind, DateTimeOffset startUtc, DateTimeOffset? endUtc) => kind switch
    {
      SubscriptionTermKind.Perpetual when endUtc is null =>
        Result.Success(new SubscriptionTerm(kind, startUtc, null)),
      SubscriptionTermKind.Fixed when endUtc is { } end && end > startUtc =>
        Result.Success(new SubscriptionTerm(kind, startUtc, end)),
      _ => Result.Failure<SubscriptionTerm>(SubscriptionErrors.InvalidTerm)
    };

  // ---- EXPIRY IS A READ, NOT A STATE.
  //
  // Nothing writes "expired" anywhere. `OD-SUB-0010` ruled subscription state and `TenantStatus` orthogonal
  // — expiry never touches `TenantStatus` — so whether a term has run out is answered by comparing it to an
  // instant, every time it is asked. A stored flag would need a scheduler to maintain and would be wrong
  // between runs.
  public bool HasExpiredAt(DateTimeOffset instant) =>
    EndUtc is { } end && instant > end;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Kind;
    yield return StartUtc;
    yield return EndUtc;
  }
}
