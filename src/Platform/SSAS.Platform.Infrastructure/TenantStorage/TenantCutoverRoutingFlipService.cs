using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// THE ATOMIC ROUTING FLIP (ADR-020, TS-Storage Phase E4).
//
// ONE PLATFORM TRANSACTION COVERS ALL THREE FACTS: the Shared assignment ends, the Dedicated assignment
// begins at the next RoutingVersion, and the cutover operation records that routing moved. ADR-020 requires
// them together, and the reason is observability rather than tidiness — a committed state where the
// assignment moved but the operation still said Frozen would tell an operator the tenant was mid-copy while
// its traffic was already on the new database.
//
// TWO FLUSHES INSIDE THAT ONE TRANSACTION, deliberately. UX_TenantDatabaseAssignments_ActiveTenant permits
// one active assignment per tenant, SQL Server has no deferrable constraints, and EF Core does not
// guarantee it emits the UPDATE that vacates the slot before the INSERT that fills it. So the end is
// flushed, then the insert — never observable outside the transaction, and never a single flush that could
// transiently hold two active rows.
//
// THE TARGET IS RE-VALIDATED IMMEDIATELY BEFORE COMMITTING. A validation that passed minutes ago is not
// evidence about now: the source is frozen and therefore stable, but nothing prevents an out-of-band write
// to the target in between, and flipping onto a target that no longer matches would move a tenant onto data
// that is not its own.
//
// INVALIDATION HAPPENS AFTER COMMIT, AND CANNOT UNDO IT. It is an optimisation; E2's version check is the
// correctness mechanism, and every other instance converges without being told anything.
internal sealed class TenantCutoverRoutingFlipService(
  PlatformDbContext platform,
  ITenantCutoverOperationStore operations,
  ITenantCutoverCopyService copyService,
  ITenantRoutingCacheInvalidator invalidator,
  IDateTimeProvider clock,
  IOptions<TenantCutoverCopyOptions> optionsAccessor) : ITenantCutoverRoutingFlipService
{
  private const string Actor = "tenant-cutover-flip";

  // THE SAME FLIP, FOR A CALLER THAT ALREADY OWNS THE OPERATION (TS-Storage Phase E5).
  //
  // Shares the entire core with the public path — preconditions, revalidation, the atomic transaction and
  // post-commit invalidation — and differs only in not acquiring ownership, which the token proves the
  // caller already holds. Re-acquiring from a second connection is the self-deadlock E4 review found.
  public async Task<Result<TenantCutoverFlipReport>> FlipUnderOwnershipAsync(
    TenantCutoverOwnership ownership,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(ownership);
    return await FlipCoreAsync(ownership.CutoverOperationId, owned: true, cancellationToken);
  }

  public Task<Result<TenantCutoverFlipReport>> FlipAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default) =>
    FlipCoreAsync(cutoverOperationId, owned: false, cancellationToken);

  private async Task<Result<TenantCutoverFlipReport>> FlipCoreAsync(
    long cutoverOperationId,
    bool owned,
    CancellationToken cancellationToken)
  {
    var options = optionsAccessor.Value;

    var operation = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverOperationNotFound);
    }

    // ---- ALREADY DONE IS A SUCCESS, NOT A FAILURE. A retry after a committed flip — a crashed caller, a
    // lost response — must not be tempted into flipping a second time and creating a second assignment.
    if (operation.Status is TenantCutoverOperationStatus.RoutingFlipped or
      TenantCutoverOperationStatus.Completed)
    {
      return await AlreadyFlippedAsync(operation, cancellationToken);
    }

    if (operation.Status != TenantCutoverOperationStatus.Frozen)
    {
      return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    // ---- OWNERSHIP, ON THE SAME RESOURCE THE COPY AND THE RELEASE USE. Copy, release and flip are three
    // things that must never overlap on one operation, so they contend for one lock rather than three.
    //
    // SKIPPED WHEN THE CALLER ALREADY OWNS IT. An orchestrator holds one lease across every phase, and
    // taking the same resource again on a second connection would block on itself forever.
    await using var ownershipConnection = owned
      ? null
      : new SqlConnection(platform.Database.GetConnectionString());

    if (ownershipConnection is not null)
    {
      await ownershipConnection.OpenAsync(cancellationToken);
      if (!await TenantCutoverOperationLock.TryAcquireForSessionAsync(
        ownershipConnection, cutoverOperationId, options.OwnershipTimeout, cancellationToken))
      {
        return Result.Failure<TenantCutoverFlipReport>(
          TenantStorageErrors.CutoverCopyOwnershipNotAcquired);
      }
    }

    // ---- THE TARGET STILL MATCHES THE SOURCE, checked under ownership and immediately before the flip.
    // This is E3's exact validation, INVOKED rather than reimplemented: "a copy was attempted" and "the
    // target has rows" are not the same claim as "the target is this tenant's data", and a validation that
    // passed minutes ago is not evidence about now — the source is frozen and therefore stable, but nothing
    // stops an out-of-band write to the target in between.
    //
    // ValidateAsync rather than CopyAsync: it copies nothing, so an incomplete target is refused instead of
    // quietly finished at the worst possible moment, and it takes no ownership lock — this method already
    // holds it, and re-acquiring on a second connection would deadlock the flip against itself.
    var revalidated = await copyService.ValidateAsync(cutoverOperationId, cancellationToken);
    if (revalidated.IsFailure)
    {
      return Result.Failure<TenantCutoverFlipReport>(revalidated.Error);
    }

    return await CommitFlipAsync(operation, cancellationToken);
  }

  private async Task<Result<TenantCutoverFlipReport>> CommitFlipAsync(
    TenantCutoverOperationRecord operation,
    CancellationToken cancellationToken)
  {
    var now = clock.UtcNow;

    await using var transaction = await platform.Database.BeginTransactionAsync(cancellationToken);

    try
    {
      // ---- A. The operation, TRACKED so its RowVersion guards this transaction against a concurrent flip.
      var tracked = await platform.TenantCutoverOperations
        .SingleOrDefaultAsync(
          candidate => candidate.Id == operation.CutoverOperationId, cancellationToken);
      if (tracked is null)
      {
        return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverOperationNotFound);
      }

      // Re-read under the transaction: the status may have moved since the pre-checks above.
      if (tracked.Status != TenantCutoverOperationStatus.Frozen)
      {
        return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverOperationNotFrozen);
      }

      // ---- B. The active assignment, also tracked for its own concurrency token.
      var active = await platform.TenantDatabaseAssignments
        .SingleOrDefaultAsync(
          assignment => assignment.TenantId == tracked.TenantId && assignment.EndedUtc == null,
          cancellationToken);
      if (active is null)
      {
        return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.ActiveAssignmentMissing);
      }

      // ---- C. Preconditions, revalidated against what this transaction actually holds.
      if (active.TenantDatabaseId != tracked.SourceTenantDatabaseId)
      {
        return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverSourceNotEligible);
      }

      var eligible = await EndpointsEligibleAsync(tracked, cancellationToken);
      if (eligible.IsFailure)
      {
        return Result.Failure<TenantCutoverFlipReport>(eligible.Error);
      }

      var previousVersion = active.RoutingVersion;

      // ---- F. THE NEXT VERSION IS COMPUTED FROM THE TENANT'S HIGHEST EVER, NOT FROM THE ACTIVE ROW.
      //
      // Those are not the same number. The durable invariant — and the guard trigger that enforces it — is
      // "greater than every assignment this tenant has ever held", because a version that merely beats the
      // ACTIVE row could still collide with one some earlier assignment already used, and a reused version
      // is indistinguishable from the one a stale cache is still holding. Nothing guarantees the active row
      // carries the maximum; ended history can legitimately sit above it.
      var highestVersion = await platform.TenantDatabaseAssignments
        .AsNoTracking()
        .Where(assignment => assignment.TenantId == tracked.TenantId)
        .MaxAsync(assignment => (long?)assignment.RoutingVersion, cancellationToken) ?? previousVersion;

      // CHECKED arithmetic: silently wrapping a routing version would make every cached route in the estate
      // valid again. Computed before anything is written, so an overflow refuses rather than half-applies.
      long nextVersion;
      try
      {
        nextVersion = checked(highestVersion + 1);
      }
      catch (OverflowException)
      {
        return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.RoutingVersionNotAdvancing);
      }

      // ---- D. End the Shared assignment, and FLUSH — see the type comment on why this cannot share a
      // flush with the insert below.
      var ended = active.End(Actor, now);
      if (ended.IsFailure)
      {
        return Result.Failure<TenantCutoverFlipReport>(ended.Error);
      }

      await platform.SaveChangesAsync(cancellationToken);

      // ---- E. Activate the Dedicated assignment at the advanced version.
      var replacement = TenantDatabaseAssignment.Create(
        tracked.TenantId, tracked.TargetTenantDatabaseId, nextVersion, "cutover", Actor, now);
      if (replacement.IsFailure)
      {
        return Result.Failure<TenantCutoverFlipReport>(replacement.Error);
      }

      platform.TenantDatabaseAssignments.Add(replacement.Value);

      // ---- G. The operation records that routing moved, and the version it moved to.
      var flipped = tracked.RecordRoutingFlip(nextVersion, Actor, now);
      if (flipped.IsFailure)
      {
        return Result.Failure<TenantCutoverFlipReport>(flipped.Error);
      }

      await platform.SaveChangesAsync(cancellationToken);

      // ---- H. One commit makes all three facts true at once.
      await transaction.CommitAsync(cancellationToken);

      // ---- AFTER COMMIT ONLY. Routing is authoritative from here; nothing below may undo it.
      var invalidationError = Invalidate(tracked.TenantId);

      return Result.Success(new TenantCutoverFlipReport(
        tracked.Id, tracked.TenantId, tracked.SourceTenantDatabaseId, tracked.TargetTenantDatabaseId,
        previousVersion, nextVersion, TenantCutoverFlipOutcome.Flipped, invalidationError));
    }
    catch (DbUpdateConcurrencyException)
    {
      // Another instance flipped this operation, or moved the assignment, between the reads above and the
      // save. A CONTROLLED refusal: exactly one flip wins, and the loser must be able to tell that from a
      // genuine failure.
      await SafeRollbackAsync(transaction);
      return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverConcurrencyConflict);
    }
    catch (DbUpdateException exception) when (IsUniquenessOrGuardViolation(exception))
    {
      // The filtered unique index or the routing-version guard trigger refused this transition. Both are
      // the database enforcing an invariant the application also checks; reaching here means something
      // raced or wrote out of band, and either way nothing is committed.
      await SafeRollbackAsync(transaction);
      return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverConcurrencyConflict);
    }
  }

  // Reports what an earlier committed flip established, rather than repeating it.
  private async Task<Result<TenantCutoverFlipReport>> AlreadyFlippedAsync(
    TenantCutoverOperationRecord operation,
    CancellationToken cancellationToken)
  {
    var current = await platform.TenantDatabaseAssignments
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == operation.TenantId && assignment.EndedUtc == null)
      .Select(assignment => new { assignment.TenantDatabaseId, assignment.RoutingVersion })
      .SingleOrDefaultAsync(cancellationToken);

    if (current is null || current.TenantDatabaseId != operation.TargetTenantDatabaseId)
    {
      // The operation says routing moved but the assignment disagrees. Refused rather than reconciled: this
      // is a contradiction between two facts that are written together, and guessing which one is right is
      // exactly what an operator needs to be told about instead.
      return Result.Failure<TenantCutoverFlipReport>(TenantStorageErrors.CutoverConcurrencyConflict);
    }

    return Result.Success(new TenantCutoverFlipReport(
      operation.CutoverOperationId, operation.TenantId,
      operation.SourceTenantDatabaseId, operation.TargetTenantDatabaseId,
      current.RoutingVersion, current.RoutingVersion, TenantCutoverFlipOutcome.AlreadyFlipped));
  }

  // LOCAL, BEST-EFFORT, AND AFTER THE FACT. A failure here is reported, never propagated as a routing
  // failure and never allowed to influence the committed transaction above.
  private Error? Invalidate(Guid tenantId)
  {
    try
    {
      invalidator.Invalidate(tenantId);
      return null;
    }
#pragma warning disable CA1031 // The whole point is that no invalidation failure can affect routing.
    catch (Exception)
#pragma warning restore CA1031
    {
      return TenantStorageErrors.CutoverInvalidationIncomplete;
    }
  }

  private async Task<Result> EndpointsEligibleAsync(
    TenantCutoverOperation operation,
    CancellationToken cancellationToken)
  {
    var endpoints = await platform.TenantDatabases
      .AsNoTracking()
      .Where(database => database.Id == operation.SourceTenantDatabaseId ||
        database.Id == operation.TargetTenantDatabaseId)
      .Select(database => new { database.Id, database.HostingMode, database.StorageMode })
      .ToListAsync(cancellationToken);

    var source = endpoints.SingleOrDefault(database => database.Id == operation.SourceTenantDatabaseId);
    var target = endpoints.SingleOrDefault(database => database.Id == operation.TargetTenantDatabaseId);

    if (source is null ||
      source.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      source.StorageMode != TenantDatabaseStorageMode.Shared)
    {
      return Result.Failure(TenantStorageErrors.CutoverSourceNotEligible);
    }

    if (target is null ||
      target.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      target.StorageMode != TenantDatabaseStorageMode.Dedicated)
    {
      return Result.Failure(TenantStorageErrors.CutoverTargetNotEligible);
    }

    return Result.Success();
  }

  private static async Task SafeRollbackAsync(
    Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
  {
    try
    {
      await transaction.RollbackAsync(CancellationToken.None);
    }
#pragma warning disable CA1031 // A rollback failure must not mask the original refusal.
    catch (Exception)
#pragma warning restore CA1031
    {
    }
  }

  // 2601/2627 are the filtered unique index; 51020/51021 are the routing-version guard trigger's own
  // THROWs, which are the database refusing a transition that failed to advance the version.
  private static bool IsUniquenessOrGuardViolation(DbUpdateException exception) =>
    exception.InnerException is SqlException { Number: 2601 or 2627 or 51020 or 51021 };
}
