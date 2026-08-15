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
  // Two mechanisms, closing two different duplicates:
  //
  //   1. AUTHORITATIVE RECHECK, inside the transaction. The scheduler's view of "due" may be stale by the
  //      time it gets here — the Phase C lesson — so the current baseline is re-read and admission refuses
  //      if it has moved. This closes SEQUENTIAL duplicates from a stale decision.
  //
  //   2. FILTERED UNIQUE INDEX on active runs. Two instances that both pass the recheck simultaneously both
  //      INSERT; the database admits exactly one and the loser gets a duplicate-key violation. This closes
  //      CONCURRENT duplicates, and it is the part a claim on an existing row cannot do, because each
  //      instance would otherwise be claiming its own record.
  //
  // Losing either race is a SUCCESS of the invariant, not an error, so both are returned as ordinary
  // failures the caller reports as a controlled non-execution.
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

    // AUTHORITATIVE RECHECK. The baseline this verification was decided against must still be the latest
    // successful full backup for the database. If a newer full has landed since the scheduler read its
    // candidate, the due state has changed and this operation is answering a question nobody is asking any
    // more.
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

    dbContext.Set<TenantDatabaseRestoreVerificationRun>().Add(admitted.Value);

    try
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniquenessViolation(exception))
    {
      // Another instance was admitted first. Detach so this context is not left holding a row the database
      // rejected — a later SaveChanges on the same scope would otherwise retry the losing insert.
      dbContext.Entry(admitted.Value).State = EntityState.Detached;
      return Result.Failure<long>(TenantStorageErrors.RestoreVerificationAlreadyAdmitted);
    }

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
