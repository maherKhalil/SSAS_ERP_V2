using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The write path for restore-verification operations (ADR-022 §17, TS-Backup Phase D).
//
// THE ADMISSION METHOD IS THE LOAD-BEARING PART OF THIS SLICE. Everything else here is ordinary lifecycle.
public sealed class TenantDatabaseRestoreVerificationRunStore(
  PlatformDbContext dbContext,
  IDateTimeProvider clock) : ITenantDatabaseRestoreVerificationRunStore
{
  // SQL Server error numbers for a uniqueness violation. 2601 is a unique INDEX, 2627 a unique CONSTRAINT;
  // the filtered index below raises 2601, and both are matched so a future model change cannot silently
  // turn a caught contention into an unhandled exception.
  private const int DuplicateKeyErrorNumber = 2601;

  private const int UniqueConstraintErrorNumber = 2627;

  private static readonly int[] UniquenessViolationNumbers =
    [DuplicateKeyErrorNumber, UniqueConstraintErrorNumber];

  // ADMISSION (ADR-022 compliance rule 43).
  //
  // THE INVARIANT IS PER DUE STATE, NOT PER ACTIVE RUN. "At most one verification running at a time" is a
  // strictly weaker property and does not satisfy the ADR: it permits a stale worker to repeat a
  // verification another instance has already completed, because completion frees the active slot.
  //
  // Three mechanisms, closing three different duplicates:
  //
  //   1. PER-DATABASE ADMISSION LOCK, taken first inside the transaction. Serialises admission decisions for
  //      one physical database so a recheck cannot be invalidated between reading and inserting. Scoped to a
  //      single row of a single database — emphatically not a fleet lock.
  //
  //   2. AUTHORITATIVE RECHECK of BOTH halves of the due state — the baseline AND the successful-verification
  //      anchor. This closes SEQUENTIAL duplicates from a stale decision, which a baseline-only check cannot:
  //      a completed verification does not move the baseline.
  //
  //   3. FILTERED UNIQUE INDEX on active runs. Two instances that both pass the recheck simultaneously both
  //      INSERT; the database admits exactly one. This closes CONCURRENT duplicates, and it is the part a
  //      claim on an existing row cannot do, because each instance would otherwise claim its own record.
  //
  // Losing any of these races is a SUCCESS of the invariant, not an error, so each returns an ordinary
  // failure the caller reports as a controlled non-execution — with a distinct reason, because "someone
  // already did this work" and "someone is doing it right now" call for different operator responses.
  public async Task<Result<long>> TryAdmitAsync(
    TenantDatabaseRestoreVerificationAdmissionRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var admitted = TenantDatabaseRestoreVerificationRun.Admit(
      request.TenantDatabaseId,
      request.SourceBackupRunId,
      request.Depth,
      request.RestoreServerKey,
      request.Actor,
      clock.UtcNow);
    if (admitted.IsFailure)
    {
      return Result.Failure<long>(admitted.Error);
    }

    // ONE ATOMIC ADMISSION DECISION. The recheck below and the insert must not be separable: without a
    // shared transaction the state a recheck observed can change before the insert lands, which is precisely
    // the read-then-execute shape Phase C had to remove.
    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

    // The serialising event, and the reason the rechecks can be ordinary reads.
    //
    // UPDLOCK + HOLDLOCK on the SINGLE TenantDatabases row: an update lock held to the end of the
    // transaction, so a second admission for the same database waits here rather than racing. Taken FIRST
    // and always on one row, so two admissions cannot acquire resources in opposing orders — which is how a
    // SERIALIZABLE range-lock approach over the two evidence tables would deadlock instead of queue.
    await dbContext.Database.ExecuteSqlInterpolatedAsync(
      $"SELECT TOP (1) 1 FROM [platform].[TenantDatabases] WITH (UPDLOCK, HOLDLOCK) WHERE [TenantDatabaseId] = {request.TenantDatabaseId}",
      cancellationToken);

    // RECHECK 1 — the baseline. A newer full backup means the chain this decision selected has moved on.
    var currentBaselineId = await dbContext.Set<TenantDatabaseBackupRun>()
      .AsNoTracking()
      .Where(run => run.TenantDatabaseId == request.TenantDatabaseId &&
        run.Status == TenantDatabaseBackupRunStatus.Succeeded &&
        run.Operation.OperationCode == "Full")
      .OrderByDescending(run => run.Id)
      .Select(run => (long?)run.Id)
      .FirstOrDefaultAsync(cancellationToken);

    if (currentBaselineId is null || currentBaselineId.Value != request.SourceBackupRunId)
    {
      return Result.Failure<long>(TenantStorageErrors.RestoreVerificationNotDue);
    }

    // RECHECK 2 — the successful-verification anchor. THE ONE THAT CLOSES THE SEQUENTIAL DUPLICATE.
    //
    // Read from the DURABLE VERIFICATION RUN, never from TenantDatabase.LastRestoreVerificationUtc. That
    // aggregate field is written through the recovery-readiness writer AFTER the run reaches Succeeded, so
    // between those two moments the active slot is already free while the timestamp still reads stale — a
    // gap a stale worker would walk straight through. Depending on it would rebuild Phase C's exact ordering
    // defect: evidence visible in one place before the derived value catches up.
    var latestSuccessfulVerificationId = await dbContext.Set<TenantDatabaseRestoreVerificationRun>()
      .AsNoTracking()
      .Where(run => run.TenantDatabaseId == request.TenantDatabaseId &&
        run.Status == TenantDatabaseRestoreVerificationStatus.Succeeded)
      .OrderByDescending(run => run.Id)
      .Select(run => (long?)run.Id)
      .FirstOrDefaultAsync(cancellationToken);

    // A FAILED verification is deliberately not evidence here: it satisfied no obligation, so it must not
    // move the anchor and make a legitimate retry look stale.
    if (latestSuccessfulVerificationId != request.ExpectedPreviousSuccessfulVerificationRunId)
    {
      return Result.Failure<long>(TenantStorageErrors.RestoreVerificationAlreadySatisfied);
    }

    dbContext.Set<TenantDatabaseRestoreVerificationRun>().Add(admitted.Value);

    try
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniquenessViolation(exception))
    {
      // Another instance holds an ACTIVE verification. Distinct from the stale case above: that one means
      // the work is already done, this one means it is happening now.
      //
      // Detach so this context is not left holding a row the database rejected — a later SaveChanges on the
      // same scope would otherwise retry the losing insert.
      dbContext.Entry(admitted.Value).State = EntityState.Detached;
      return Result.Failure<long>(TenantStorageErrors.RestoreVerificationAlreadyAdmitted);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(admitted.Value.Id);
  }

  public Task<Result> BeginRestoreAsync(
    long verificationRunId,
    string verificationDatabaseName,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      verificationRunId,
      run => run.BeginRestore(verificationDatabaseName, actor, clock.UtcNow),
      cancellationToken);

  public Task<Result> MarkSucceededAsync(
    long verificationRunId,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(verificationRunId, run => run.Succeed(actor, clock.UtcNow), cancellationToken);

  public Task<Result> MarkFailedAsync(
    long verificationRunId,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(verificationRunId, run => run.Fail(errorSummary, actor, clock.UtcNow), cancellationToken);

  public Task<Result> MarkInfrastructureUnavailableAsync(
    long verificationRunId,
    string? reasonSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      verificationRunId,
      run => run.AbandonUnavailable(reasonSummary, actor, clock.UtcNow),
      cancellationToken);

  public Task<Result> RecordCleanupAsync(
    long verificationRunId,
    TenantDatabaseVerificationCleanupState state,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(
      verificationRunId,
      run => run.RecordCleanup(state, errorSummary, actor, clock.UtcNow),
      cancellationToken);

  private async Task<Result> ApplyAsync(
    long verificationRunId,
    Func<TenantDatabaseRestoreVerificationRun, Result> transition,
    CancellationToken cancellationToken)
  {
    var run = await dbContext.Set<TenantDatabaseRestoreVerificationRun>()
      .FirstOrDefaultAsync(candidate => candidate.Id == verificationRunId, cancellationToken);
    if (run is null)
    {
      return Result.Failure(TenantStorageErrors.TenantDatabaseRequired);
    }

    var result = transition(run);
    if (result.IsFailure)
    {
      return result;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }

  private static bool IsUniquenessViolation(DbUpdateException exception) =>
    exception.InnerException is SqlException sqlException &&
    Array.IndexOf(UniquenessViolationNumbers, sqlException.Number) >= 0;
}
