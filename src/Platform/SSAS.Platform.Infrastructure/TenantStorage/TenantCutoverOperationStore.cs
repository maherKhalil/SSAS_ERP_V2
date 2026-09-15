using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Durable cutover-operation persistence (ADR-020, TS-Storage Phase E1).
public sealed class TenantCutoverOperationStore(
  PlatformDbContext dbContext,
  IDateTimeProvider clock,
  TimeSpan? releaseOwnershipTimeout = null) : ITenantCutoverOperationStore
{
  private readonly TimeSpan releaseOwnershipTimeout = releaseOwnershipTimeout ?? TimeSpan.FromSeconds(5);

  public async Task<Result<long>> BeginAsync(
    TenantCutoverBeginRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    // ---- ELIGIBILITY, ESTABLISHED FROM THE REGISTRY RATHER THAN FROM THE CALLER.
    var endpoints = await dbContext.TenantDatabases
      .AsNoTracking()
      .Where(database => database.Id == request.SourceTenantDatabaseId ||
        database.Id == request.TargetTenantDatabaseId)
      .Select(database => new { database.Id, database.HostingMode, database.StorageMode })
      .ToListAsync(cancellationToken);

    var source = endpoints.SingleOrDefault(database => database.Id == request.SourceTenantDatabaseId);
    var target = endpoints.SingleOrDefault(database => database.Id == request.TargetTenantDatabaseId);

    if (source is null || source.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<long>(TenantStorageErrors.CutoverSourceNotEligible);
    }

    // V1 MOVES TENANTS ONTO PLATFORM-MANAGED DEDICATED STORAGE AND NOTHING ELSE. A CustomerManaged target
    // has no platform recovery path and no supported provisioning path (ADR-021); a Shared target is not a
    // promotion at all.
    if (target is null ||
      target.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      target.StorageMode != TenantDatabaseStorageMode.Dedicated)
    {
      return Result.Failure<long>(TenantStorageErrors.CutoverTargetNotEligible);
    }

    // The source must be where the tenant actually routes today, not merely a database someone named.
    var activeAssignment = await dbContext.TenantDatabaseAssignments
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == request.TenantId && assignment.EndedUtc == null)
      .Select(assignment => (long?)assignment.TenantDatabaseId)
      .SingleOrDefaultAsync(cancellationToken);

    if (activeAssignment != request.SourceTenantDatabaseId)
    {
      return Result.Failure<long>(TenantStorageErrors.CutoverSourceNotEligible);
    }

    var begun = TenantCutoverOperation.Begin(
      request.TenantId,
      request.SourceTenantDatabaseId,
      request.TargetTenantDatabaseId,
      request.Actor,
      clock.UtcNow);
    if (begun.IsFailure)
    {
      return Result.Failure<long>(begun.Error);
    }

    dbContext.TenantCutoverOperations.Add(begun.Value);

    try
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniquenessViolation(exception))
    {
      // Another instance already holds this tenant's cutover slot. An ORDINARY OUTCOME — the invariant
      // worked — so it is reported rather than thrown.
      dbContext.Entry(begun.Value).State = EntityState.Detached;
      return Result.Failure<long>(TenantStorageErrors.CutoverAlreadyActive);
    }

    return Result.Success(begun.Value.Id);
  }

  public Task<TenantCutoverOperationRecord?> FindAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantCutoverOperations
      .AsNoTracking()
      .Where(operation => operation.Id == cutoverOperationId)
      .Select(operation => new TenantCutoverOperationRecord(
        operation.Id,
        operation.TenantId,
        operation.SourceTenantDatabaseId,
        operation.TargetTenantDatabaseId,
        operation.Status,
        operation.FreezeRequestedUtc,
        operation.FrozenUtc,
        operation.FreezeReleasedUtc,
        operation.FailureSummary))
      .FirstOrDefaultAsync(cancellationToken)!;

  // READ FROM THE DURABLE ROW, ALWAYS. This is the half of the fence that survives process loss: the drain
  // lock stops writers that are already in flight, and this stops the ones that arrive afterwards.
  //
  // COMPLETED COUNTS, TOO (TS-Storage Phase E5).
  //
  // Marking orchestration finished does not make a stale writer safe. A context created before the cutover
  // still holds a connection to the SOURCE and still never re-resolves, so if this stopped matching at
  // Completed the fence would return "no cutover holds this tenant" and admit that writer straight into the
  // database the tenant was moved off — hours or days later. Completion is an orchestration milestone; the
  // source is wrong forever.
  //
  // THE MOST RECENT ONE DECIDES. Unlike the active statuses, Completed is not unique per tenant: a tenant
  // can be cut over more than once over its life, and each completed operation names the target that was
  // authoritative at the time. The latest is the one describing where the tenant is now, so a writer bound
  // to any earlier database — the original Shared one, or the target of a cutover before last — fails the
  // target comparison and is refused.
  public Task<TenantCutoverWriteGate?> FindActiveWriteGateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantCutoverOperations
      .AsNoTracking()
      .Where(operation => operation.TenantId == tenantId &&
        (operation.Status == TenantCutoverOperationStatus.Frozen ||
         operation.Status == TenantCutoverOperationStatus.RoutingFlipped ||
         operation.Status == TenantCutoverOperationStatus.Completed))
      .OrderByDescending(operation => operation.Id)
      .Select(operation => new TenantCutoverWriteGate(
        operation.Id,
        operation.TenantId,
        operation.SourceTenantDatabaseId,
        operation.TargetTenantDatabaseId,
        operation.Status,
        operation.PostCutoverWriteObservedUtc))
      .FirstOrDefaultAsync(cancellationToken);

  // WRITE-ONCE, AND A LOST RACE IS NOT A FAILURE (E4 review LOW-1).
  //
  // Two application writes can both be the "first" one from their own point of view: both read the gate
  // before either recorded, so both attempt the update and one loses on the RowVersion token. Surfacing
  // that as a concurrency conflict would fail a legitimate tenant write for a conflict that does not exist
  // — the loser wanted the timestamp set, and it is set.
  //
  // NARROWLY, THOUGH. The conflict is only absorbed after RE-READING and confirming the observation is
  // genuinely now recorded. A concurrency failure for any other reason still surfaces, because a blanket
  // catch here would hide real contention on this row.
  //
  // ---- THE LOSER IS NOT ALWAYS ANOTHER WRITER (post-Phase-E hardening H1, Phase E final review LOW-1).
  //
  // The row has a second writer that is not recording an observation at all: the orchestrator's Complete().
  // It can win the RowVersion race and leave the timestamp exactly as it was, so the re-read finds NULL and
  // the call fails — through no fault of the write it was fencing.
  //
  // ⚠ THE BRANCH THIS CAME FROM DESCRIBED THAT AS A FALSE NEGATIVE — *"the tenant's write, whose result
  // was previously discarded, committed anyway"*. **That was true when the fence discarded this result.
  // T-135 stopped it doing so, so the write no longer commits and the platform cannot believe none did.**
  //
  // What is left is the opposite failure: a FALSE REFUSAL. The write is turned away for a bookkeeping race
  // the caller cannot see and could not have avoided — it receives `CutoverConcurrencyConflict` with no way
  // to tell the platform losing a race with itself from another writer genuinely contending.
  //
  // So a re-read showing NULL now means "nobody has recorded it and this attempt was displaced", which is
  // a reason to try once more against fresh state — not a reason to give up. ONE retry, because the only
  // competing non-observation writer is Complete() and it runs once; a loop here would turn a bookkeeping
  // race into an unbounded wait on the tenant's write path.
  public async Task<Result> RecordPostCutoverWriteAsync(
    long cutoverOperationId,
    long tenantDatabaseId,
    string actor,
    CancellationToken cancellationToken = default)
  {
    var first = await TryRecordPostCutoverWriteAsync(cutoverOperationId, actor, cancellationToken);
    if (first.IsSuccess || first.Error.Code != TenantStorageErrors.CutoverConcurrencyConflict.Code)
    {
      // Anything that is not the displacement race — the operation vanished, the status no longer admits an
      // observation, a genuine persistence failure — is the caller's to see, unchanged.
      return first;
    }

    var refreshed = await ReadWriteGateByOperationAsync(cutoverOperationId, cancellationToken);
    if (refreshed is null)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotFound);
    }

    // Another writer got there first: what this call wanted is true.
    if (refreshed.PostCutoverWriteObservedUtc is not null)
    {
      return Result.Success();
    }

    // ---- STILL THE SAME WRITE? Retrying on "the timestamp is null" alone would record an observation for a
    // write the fence would no longer admit: THIS operation may have moved on — rolled back or failed —
    // between admission and here. Revalidated against the row just read, not the one the caller decided on.
    //
    // ⚠ IT DOES NOT CATCH A FURTHER CUTOVER, and an earlier draft of this comment said it did (T-138). A
    // second cutover is a DIFFERENT ROW with a different Id, and the read above is BY Id. That case is caught
    // at admission instead: `FindActiveWriteGateAsync` orders by `Id` descending and hands the fence the
    // NEWEST operation for the tenant. The protection is real and it lives there — stated because the wrong
    // attribution reads as the reason a by-Id seek is sufficient here.
    // ⚠ THE SAME CONDITION AS THE FENCE'S ROUTE CHECK, AND IT CARRIES THE SAME CODE (T-213).
    //
    // This is the second site of the misrouted case — the revalidation, rather than admission. It shared
    // `TenantWritesFrozen` for the same inherited reason and is wrong for the same one: nothing is frozen,
    // this writer is bound to the tenant's previous database. **Splitting only the fence would have left the
    // signal ambiguous**, since a caller cannot tell which site refused it.
    if (!refreshed.PermitsWriteTo(tenantDatabaseId))
    {
      return Result.Failure(TenantStorageErrors.TenantWriteRouteStale);
    }

    var second = await TryRecordPostCutoverWriteAsync(cutoverOperationId, actor, cancellationToken);
    if (second.IsSuccess || second.Error.Code != TenantStorageErrors.CutoverConcurrencyConflict.Code)
    {
      return second;
    }

    // Displaced again — only a true first-write recorder can have done it this time, so the one question
    // left is whether the observation now exists. If it still does not, the caller must refuse the write.
    var settled = await ReadWriteGateByOperationAsync(cutoverOperationId, cancellationToken);
    return settled?.PostCutoverWriteObservedUtc is not null
      ? Result.Success()
      : Result.Failure(TenantStorageErrors.CutoverConcurrencyConflict);
  }


  // above can tell displacement apart from a real failure without catching exceptions to do it.
  private async Task<Result> TryRecordPostCutoverWriteAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken)
  {
    try
    {
      return await ApplyAsync(
        cutoverOperationId,
        operation => operation.RecordPostCutoverWrite(actor, clock.UtcNow),
        cancellationToken);
    }
    catch (DbUpdateConcurrencyException)
    {
      // Detach so the next read observes the database rather than the failed attempt still in the tracker.
      foreach (var entry in dbContext.ChangeTracker.Entries<TenantCutoverOperation>().ToArray())
      {
        entry.State = EntityState.Detached;
      }

      return Result.Failure(TenantStorageErrors.CutoverConcurrencyConflict);
    }
  }

  // The same projection the tenant-facing gate uses, keyed by the operation this call already holds — a
  // primary-key seek, so it introduces no access path the existing indexes do not already serve.
  private Task<TenantCutoverWriteGate?> ReadWriteGateByOperationAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken) =>
    dbContext.TenantCutoverOperations
      .AsNoTracking()
      .Where(operation => operation.Id == cutoverOperationId)
      .Select(operation => new TenantCutoverWriteGate(
        operation.Id,
        operation.TenantId,
        operation.SourceTenantDatabaseId,
        operation.TargetTenantDatabaseId,
        operation.Status,
        operation.PostCutoverWriteObservedUtc))
      .SingleOrDefaultAsync(cancellationToken);

  public Task<Result> CompleteAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      cutoverOperationId,
      operation => operation.Complete(actor, clock.UtcNow),
      cancellationToken);

  // At most one row can match: the filtered unique index admits one active cutover per tenant.
  public Task<TenantCutoverOperationRecord?> FindActiveForTenantAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantCutoverOperations
      .AsNoTracking()
      .Where(operation => operation.TenantId == tenantId &&
        (operation.Status == TenantCutoverOperationStatus.Preparing ||
         operation.Status == TenantCutoverOperationStatus.Frozen ||
         operation.Status == TenantCutoverOperationStatus.RoutingFlipped))
      .Select(operation => new TenantCutoverOperationRecord(
        operation.Id,
        operation.TenantId,
        operation.SourceTenantDatabaseId,
        operation.TargetTenantDatabaseId,
        operation.Status,
        operation.FreezeRequestedUtc,
        operation.FrozenUtc,
        operation.FreezeReleasedUtc,
        operation.FailureSummary))
      .SingleOrDefaultAsync(cancellationToken)!;

  public Task<Result> RequestFreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(cutoverOperationId, operation => operation.RequestFreeze(actor, clock.UtcNow), cancellationToken);

  public Task<Result> FreezeAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(cutoverOperationId, operation => operation.Freeze(actor, clock.UtcNow), cancellationToken);

  public Task<Result> FailFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      cutoverOperationId,
      operation => operation.FailFreeze(failureSummary, actor, clock.UtcNow),
      cancellationToken);

  // RELEASE CONTENDS WITH A RUNNING COPY (ADR-020, TS-Storage Phase E3).
  //
  // E1 deliberately took no lock here, because release must work when the SOURCE database is unhealthy —
  // and it still does: this lock lives in the Platform database, which release is already connected to.
  //
  // What changed is that a copy now exists. Releasing under one would make the source writable again while
  // the copy is still streaming from it, so the copy would validate a target against a source that moved
  // after it was read. A release that cannot take the operation is REFUSED rather than queued: an operator
  // needs an answer, and "a copy is running" is the answer.
  //
  // A dead copy holds nothing — its ownership is session-scoped — so this cannot leave a tenant permanently
  // frozen, which is the property ADR-020 requires release to keep.
  public async Task<Result> ReleaseFreezeAsync(
    long cutoverOperationId,
    string? failureSummary,
    string actor,
    CancellationToken cancellationToken = default)
  {
    var connection = dbContext.Database.GetDbConnection();
    if (connection is not SqlConnection sqlConnection)
    {
      return await ApplyAsync(
        cutoverOperationId,
        operation => operation.ReleaseFreeze(failureSummary, actor, clock.UtcNow),
        cancellationToken);
    }

    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync(cancellationToken);
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    var owned = await TenantCutoverOperationLock.TryAcquireForTransactionAsync(
      sqlConnection,
      (SqlTransaction)transaction.GetDbTransaction(),
      cutoverOperationId,
      releaseOwnershipTimeout,
      cancellationToken);

    if (!owned)
    {
      await transaction.RollbackAsync(cancellationToken);
      return Result.Failure(TenantStorageErrors.CutoverReleaseBlockedByActiveCopy);
    }

    var released = await ApplyAsync(
      cutoverOperationId,
      operation => operation.ReleaseFreeze(failureSummary, actor, clock.UtcNow),
      cancellationToken);

    if (released.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return released;
    }

    await transaction.CommitAsync(cancellationToken);
    return released;
  }

  private async Task<Result> ApplyAsync(
    long cutoverOperationId,
    Func<TenantCutoverOperation, Result> transition,
    CancellationToken cancellationToken)
  {
    var operation = await dbContext.TenantCutoverOperations
      .FirstOrDefaultAsync(candidate => candidate.Id == cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure(TenantStorageErrors.CutoverOperationNotFound);
    }

    var result = transition(operation);
    if (result.IsFailure)
    {
      return result;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }

  private static bool IsUniquenessViolation(DbUpdateException exception) =>
    exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };
}
