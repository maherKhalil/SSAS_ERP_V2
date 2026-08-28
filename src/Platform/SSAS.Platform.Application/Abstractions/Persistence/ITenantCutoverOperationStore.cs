using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The durable cutover-operation boundary (ADR-020, TS-Storage Phase E1).
//
// Intent-specific transitions rather than a status setter, following the backup and verification run
// stores: beginning a cutover, recording a drain attempt, establishing the freeze, failing it and releasing
// it mean materially different things, and the two that must never be confused — a freeze that was
// ESTABLISHED and one that was merely REQUESTED — must not be reachable through the same call.
public interface ITenantCutoverOperationStore
{
  // Creates the operation, having verified both endpoints are eligible. Refuses when the tenant already
  // holds an active cutover; the filtered unique index refuses it a second time at the database.
  Task<Result<long>> BeginAsync(
    TenantCutoverBeginRequest request,
    CancellationToken cancellationToken = default);

  Task<TenantCutoverOperationRecord?> FindAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default);

  // THE RUNTIME WRITE GATE. Read on the tenant write path, from durable state — never from a cached flag,
  // and never from the presence of a lock, because a lock disappears with the process that took it.
  //
  // IT RETURNS THE OPERATION, NOT A BOOLEAN, since TS-Storage Phase E4. Once routing can move, "may this
  // tenant write?" is no longer answerable from the tenant alone: after the flip the SOURCE must stay
  // refused while the TARGET must be writable, and a boolean keyed only on TenantId would either freeze the
  // tenant forever or unfreeze the database it just moved off. Null means no active cutover holds it.
  Task<TenantCutoverWriteGate?> FindActiveWriteGateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);

  // Records the first application write that reached the cutover target. Write-once and idempotent.
  //
  // ITS RESULT DECIDES WHETHER THE WRITE MAY PROCEED (T-135). A failure means the observation is known
  // NOT to be recorded, and the caller must refuse the write rather than let a genuine target write land
  // while the platform still believes none has.
  //
  // The physical database is taken so the implementation can revalidate, against freshly read state, that
  // the write it is fencing is still legal before retrying (T-139) — a retry decided only by "the
  // timestamp is still null" would record an observation for a write that is no longer permitted.
  Task<Result> RecordPostCutoverWriteAsync(
    long cutoverOperationId,
    long tenantDatabaseId,
    string actor,
    CancellationToken cancellationToken = default);

  // Marks orchestration finished after a committed flip and successful post-flip verification. Idempotent.
  Task<Result> CompleteAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default);

  // The tenant's active cutover, whatever its phase. The orchestrator needs this to decide whether a Start
  // is a fresh cutover or a collision, and to resume without being handed an identifier.
  Task<TenantCutoverOperationRecord?> FindActiveForTenantAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);

  Task<Result> RequestFreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default);

  // Establishes the durable fence. Called only while the drain lock is held.
  Task<Result> FreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default);

  Task<Result> FailFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default);

  // Pre-flip release. Idempotent, because a failed cutover must never leave a tenant permanently frozen.
  Task<Result> ReleaseFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default);
}

// What the write fence needs to decide admission, and nothing more (ADR-020, TS-Storage Phase E4).
//
// THE DECISION IS PER-ROUTE, NOT PER-TENANT. During the freeze every write is refused; after the flip the
// answer depends entirely on WHICH database the writer is bound to — the source it was moved off, or the
// target it was moved to.
public sealed record TenantCutoverWriteGate(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  TenantCutoverOperationStatus Status,
  DateTimeOffset? PostCutoverWriteObservedUtc)
{
  // Frozen is the copy window: nothing may write anywhere, because the copy is reading a source that must
  // not move and the target is not yet the tenant's database.
  public bool RefusesEveryWrite => Status == TenantCutoverOperationStatus.Frozen;

  // After the flip the target IS the tenant's database, so writes to it are ordinary application traffic —
  // and are the writes whose first occurrence must be recorded.
  public bool PermitsWriteTo(long tenantDatabaseId) =>
    Status is TenantCutoverOperationStatus.RoutingFlipped or TenantCutoverOperationStatus.Completed &&
    tenantDatabaseId == TargetTenantDatabaseId;

  public bool IsFirstTargetWrite => PostCutoverWriteObservedUtc is null;
}

public sealed record TenantCutoverBeginRequest(
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  string Actor);

public sealed record TenantCutoverOperationRecord(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  TenantCutoverOperationStatus Status,
  DateTimeOffset? FreezeRequestedUtc,
  DateTimeOffset? FrozenUtc,
  DateTimeOffset? FreezeReleasedUtc,
  string? FailureSummary);
