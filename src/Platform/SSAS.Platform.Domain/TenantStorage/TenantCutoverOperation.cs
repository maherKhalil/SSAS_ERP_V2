using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// The DURABLE RECORD of one Shared → Dedicated cutover (ADR-020).
//
// WHY THIS IS PERSISTED STATE RATHER THAN A RUNTIME OBJECT. ADR-020 requires the flip transaction to cover
// the assignment change, the RoutingVersion increment and this record together, and requires the record to
// track whether a post-cutover write has occurred so that WHICH ROLLBACK REGIME APPLIES is a recorded fact
// rather than a judgement made under incident pressure. Neither is expressible in a process's memory.
//
// IT IS ALSO THE FREEZE ITSELF. A session-scoped lock disappears when its process dies; a tenant whose
// writes silently resumed because a cutover worker crashed mid-copy would have exactly the split-brain
// ADR-020 forbids. The lock drains in-flight writers, this row decides whether new ones may commit, and
// only the second of those survives process loss.
public sealed class TenantCutoverOperation : AggregateRoot<long>, IAuditableEntity
{
  public const int ActorMaximumLength = 256;

  public const int FailureSummaryMaximumLength = 512;

  private TenantCutoverOperation(
    long id,
    Guid tenantId,
    long sourceTenantDatabaseId,
    long targetTenantDatabaseId,
    string actor,
    DateTimeOffset occurredUtc) : base(id)
  {
    TenantId = tenantId;
    SourceTenantDatabaseId = sourceTenantDatabaseId;
    TargetTenantDatabaseId = targetTenantDatabaseId;
    Status = TenantCutoverOperationStatus.Preparing;
    StartedUtc = occurredUtc.ToUniversalTime();
    CreatedUtc = StartedUtc;
    CreatedBy = actor;
    ModifiedUtc = StartedUtc;
    ModifiedBy = actor;
  }

  private TenantCutoverOperation() : base(0)
  {
  }

  public Guid TenantId { get; private set; }

  // The physical database the tenant is routed to today, and the one whose writes are frozen.
  public long SourceTenantDatabaseId { get; private set; }

  // The prepared Dedicated database. Recorded at the start so the operation identifies both endpoints even
  // if it never reaches the flip.
  public long TargetTenantDatabaseId { get; private set; }

  public TenantCutoverOperationStatus Status { get; private set; }

  public DateTimeOffset StartedUtc { get; private set; }

  // ---- Freeze lifecycle. Requested and established are distinct: a freeze that timed out draining was
  // requested and never established, and collapsing the two would lose that.
  public DateTimeOffset? FreezeRequestedUtc { get; private set; }

  public DateTimeOffset? FrozenUtc { get; private set; }

  public DateTimeOffset? FreezeReleasedUtc { get; private set; }

  // ---- Routing flip. Written by the LATER slice, in the same Platform transaction as the assignment
  // change and the version increment. Present now so that slice does not require a second schema change.
  public DateTimeOffset? RoutingFlippedUtc { get; private set; }

  public long? RoutingVersion { get; private set; }

  // ADR-020: the record must track whether a post-cutover write has occurred, because that fact — not an
  // opinion formed during an incident — decides whether any rollback regime is available at all.
  public DateTimeOffset? PostCutoverWriteObservedUtc { get; private set; }

  public DateTimeOffset? CompletedUtc { get; private set; }

  // Safe summary only. No connection strings, credentials or endpoints.
  public string? FailureSummary { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  // OCCUPIES THE TENANT'S CUTOVER SLOT. Enforced by a filtered unique index as well, so a second instance
  // cannot create a parallel cutover for the same tenant.
  public bool IsActive => Status is TenantCutoverOperationStatus.Preparing or
    TenantCutoverOperationStatus.Frozen or TenantCutoverOperationStatus.RoutingFlipped;

  // REFUSES APPLICATION WRITES. Frozen is the copy window; RoutingFlipped keeps the source refused because
  // the tenant's authoritative database has moved and a source write would be written to the wrong place.
  public bool RefusesApplicationWrites => Status is TenantCutoverOperationStatus.Frozen or
    TenantCutoverOperationStatus.RoutingFlipped;

  public static Result<TenantCutoverOperation> Begin(
    Guid tenantId,
    long sourceTenantDatabaseId,
    long targetTenantDatabaseId,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<TenantCutoverOperation>(TenantStorageErrors.TenantRequired);
    }

    if (sourceTenantDatabaseId <= 0 || targetTenantDatabaseId <= 0)
    {
      return Result.Failure<TenantCutoverOperation>(TenantStorageErrors.TenantDatabaseRequired);
    }

    // A cutover from a database to itself is not a cutover. Caught here as well as by the eligibility
    // checks above it, because this is the aggregate that would otherwise record the nonsense.
    if (sourceTenantDatabaseId == targetTenantDatabaseId)
    {
      return Result.Failure<TenantCutoverOperation>(TenantStorageErrors.CutoverTargetNotEligible);
    }

    if (string.IsNullOrWhiteSpace(actor) || actor.Length > ActorMaximumLength)
    {
      return Result.Failure<TenantCutoverOperation>(TenantStorageErrors.ActorRequired);
    }

    return Result.Success(new TenantCutoverOperation(
      0, tenantId, sourceTenantDatabaseId, targetTenantDatabaseId, actor, occurredUtc));
  }

  // Recorded BEFORE the drain is attempted, so an operation that dies mid-drain is distinguishable from one
  // that never tried.
  public Result RequestFreeze(string actor, DateTimeOffset occurredUtc)
  {
    if (Status != TenantCutoverOperationStatus.Preparing)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotPreparing);
    }

    FreezeRequestedUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // THE FENCE BECOMES DURABLE HERE. Written while the drain lock is still held, so no writer can pass
  // between the drain completing and this row being visible.
  public Result Freeze(string actor, DateTimeOffset occurredUtc)
  {
    // Idempotent: a resumed or retried freeze that finds the operation already frozen has nothing to do.
    if (Status == TenantCutoverOperationStatus.Frozen)
    {
      return Result.Success();
    }

    if (Status != TenantCutoverOperationStatus.Preparing)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotPreparing);
    }

    Status = TenantCutoverOperationStatus.Frozen;
    FrozenUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // PRE-FLIP RELEASE ONLY. Routing never moved, so the tenant simply returns to normal on its existing
  // database and this operation becomes history. Idempotent, because release is the one step that must be
  // as reliable as the copy: a failed cutover must never leave a tenant permanently frozen (ADR-020).
  public Result ReleaseFreeze(string? failureSummary, string actor, DateTimeOffset occurredUtc)
  {
    if (Status == TenantCutoverOperationStatus.Abandoned)
    {
      return Result.Success();
    }

    // AFTER THE FLIP THERE IS NO RELEASE PATH HERE. The authoritative database has moved; "unfreezing" the
    // source would invite writes to a database that is no longer the tenant's own.
    if (Status is TenantCutoverOperationStatus.RoutingFlipped or TenantCutoverOperationStatus.Completed)
    {
      return Result.Failure(TenantStorageErrors.CutoverAlreadyFlipped);
    }

    Status = TenantCutoverOperationStatus.Abandoned;
    FreezeReleasedUtc = occurredUtc.ToUniversalTime();
    CompletedUtc = FreezeReleasedUtc;
    FailureSummary = Truncate(failureSummary) ?? FailureSummary;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // THE POINT OF NO RETURN (ADR-020, TS-Storage Phase E4).
  //
  // Written in the SAME Platform transaction as ending the Shared assignment and inserting the Dedicated
  // one, so there is no committed state in which routing has moved and this row still says Frozen, or this
  // row says RoutingFlipped and routing is still Shared.
  //
  // It records the version it flipped TO, so the authoritative RoutingVersion an operator sees on the
  // operation is the same number the assignment carries — reconstructing it later from assignment history
  // would be a guess during an incident.
  public Result RecordRoutingFlip(long routingVersion, string actor, DateTimeOffset occurredUtc)
  {
    // Idempotent for the SAME version: a retry that finds its own flip already committed has succeeded.
    if (Status == TenantCutoverOperationStatus.RoutingFlipped && RoutingVersion == routingVersion)
    {
      return Result.Success();
    }

    if (Status != TenantCutoverOperationStatus.Frozen)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    if (routingVersion <= 0)
    {
      return Result.Failure(TenantStorageErrors.RoutingVersionInvalid);
    }

    Status = TenantCutoverOperationStatus.RoutingFlipped;
    RoutingVersion = routingVersion;
    RoutingFlippedUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // ORCHESTRATION IS FINISHED (ADR-020, TS-Storage Phase E5).
  //
  // Completed means: the flip committed, the Dedicated route is authoritative, post-flip verification
  // succeeded, and no pre-traffic step remains. It deliberately does NOT mean every instance received an
  // invalidation (they converge through the version check instead), that a target write has happened (an
  // idle tenant must be able to finish), or that source data has been removed (retention is a separate
  // operational capability that does not exist).
  //
  // REACHABLE ONLY FROM RoutingFlipped, so completion cannot be claimed for a cutover whose routing never
  // moved. Idempotent, because finalisation is the step most likely to be retried after a lost response.
  //
  // No new persisted field: CompletedUtc already exists for exactly this, and ModifiedUtc/ModifiedBy carry
  // the provenance.
  public Result Complete(string actor, DateTimeOffset occurredUtc)
  {
    if (Status == TenantCutoverOperationStatus.Completed)
    {
      return Result.Success();
    }

    if (Status != TenantCutoverOperationStatus.RoutingFlipped)
    {
      return Result.Failure(TenantStorageErrors.CutoverNotFlipped);
    }

    Status = TenantCutoverOperationStatus.Completed;
    CompletedUtc = occurredUtc.ToUniversalTime();
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // THE FIRST APPLICATION WRITE THAT LANDED ON THE NEW DATABASE.
  //
  // ADR-020 requires this as a recorded fact because it decides which rollback regime is available at all —
  // and that decision is otherwise made under incident pressure from someone's recollection.
  //
  // WRITE-ONCE AND IDEMPOTENT. Only the first observation matters; later writes must not move it, or "when
  // did the target become authoritative in practice" would drift forward forever.
  public Result RecordPostCutoverWrite(string actor, DateTimeOffset occurredUtc)
  {
    if (Status is not (TenantCutoverOperationStatus.RoutingFlipped or TenantCutoverOperationStatus.Completed))
    {
      return Result.Failure(TenantStorageErrors.CutoverNotFlipped);
    }

    if (PostCutoverWriteObservedUtc is not null)
    {
      return Result.Success();
    }

    var observedUtc = occurredUtc.ToUniversalTime();

    // The check constraint requires the observation not to precede the flip. A clock that disagrees is
    // clamped rather than allowed to fail the write it is only observing.
    PostCutoverWriteObservedUtc = RoutingFlippedUtc is { } flipped && observedUtc < flipped
      ? flipped
      : observedUtc;
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // A drain that could not complete inside its budget. Terminal, and deliberately NOT "Frozen": a partial
  // freeze claim is the one outcome that would make every later step unsafe.
  public Result FailFreeze(string? failureSummary, string actor, DateTimeOffset occurredUtc)
  {
    if (Status is not (TenantCutoverOperationStatus.Preparing or TenantCutoverOperationStatus.Abandoned))
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotPreparing);
    }

    Status = TenantCutoverOperationStatus.Abandoned;
    CompletedUtc = occurredUtc.ToUniversalTime();
    FailureSummary = Truncate(failureSummary);
    Touch(actor, occurredUtc);
    return Result.Success();
  }

  // Explicit, following every other tenant-storage aggregate: the auditing interceptor needs setters, and
  // exposing them publicly would let any caller rewrite provenance.
  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }

  private void Touch(string actor, DateTimeOffset occurredUtc)
  {
    ModifiedUtc = occurredUtc.ToUniversalTime();
    ModifiedBy = actor;
  }

  private static string? Truncate(string? value) =>
    string.IsNullOrWhiteSpace(value)
      ? null
      : value.Length <= FailureSummaryMaximumLength ? value : value[..FailureSummaryMaximumLength];
}

// The cutover lifecycle. Deliberately small: this slice owns the freeze, and the two later values exist so
// the flip slice can move the operation forward without a second schema change.
public enum TenantCutoverOperationStatus
{
  // Created, endpoints recorded, writes still normal.
  Preparing = 1,

  // The drain completed and application writes for this tenant are refused.
  Frozen = 2,

  // Routing has moved to the target. Written by the LATER flip slice.
  RoutingFlipped = 3,

  // Convergence finished. Written by the LATER flip slice.
  Completed = 4,

  // Terminal before the flip: released, or the drain failed. Routing never moved.
  Abandoned = 5
}
