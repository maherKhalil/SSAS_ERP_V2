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
  // At most one row can match: the filtered unique index admits one active cutover per tenant, and the
  // statuses below are a subset of that filter.
  public Task<TenantCutoverWriteGate?> FindActiveWriteGateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default) =>
    dbContext.TenantCutoverOperations
      .AsNoTracking()
      .Where(operation => operation.TenantId == tenantId &&
        (operation.Status == TenantCutoverOperationStatus.Frozen ||
         operation.Status == TenantCutoverOperationStatus.RoutingFlipped))
      .Select(operation => new TenantCutoverWriteGate(
        operation.Id,
        operation.TenantId,
        operation.SourceTenantDatabaseId,
        operation.TargetTenantDatabaseId,
        operation.Status,
        operation.PostCutoverWriteObservedUtc))
      .SingleOrDefaultAsync(cancellationToken);

  public Task<Result> RecordPostCutoverWriteAsync(
    long cutoverOperationId,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      cutoverOperationId,
      operation => operation.RecordPostCutoverWrite(actor, clock.UtcNow),
      cancellationToken);

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
