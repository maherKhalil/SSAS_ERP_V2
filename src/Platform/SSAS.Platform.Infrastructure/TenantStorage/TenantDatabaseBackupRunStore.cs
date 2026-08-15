using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The WRITE path for backup run history (ADR-022 §15). Phase A added the read repository and the entity;
// this is the narrow store the executor needs to record a lifecycle.
//
// INTENT-SPECIFIC METHODS, not a generic aggregate repository. Each method names one transition, so an
// invalid one is hard to express — the same reasoning that made the health writer dimension-scoped. In
// particular there is no "set status to whatever" method, because the two statuses most worth protecting
// (Succeeded, and the two skips) mean very different things and must not be interchangeable.
//
// Every method is a SHORT write. No Platform transaction is held across a backup: a BACKUP can run for
// hours, and holding a Platform transaction open across it would be indefensible.
public interface ITenantDatabaseBackupRunStore
{
  Task<long> StartAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    string? destinationKey,
    string actor,
    CancellationToken cancellationToken = default);

  // Records the reconciled evidence that justifies success. Provider backup identity is REQUIRED by the
  // aggregate, so a run cannot be marked successful without it.
  Task MarkSucceededAsync(
    long backupRunId,
    TenantDatabaseBackupProviderResult result,
    string actor,
    CancellationToken cancellationToken = default);

  Task MarkFailedAsync(
    long backupRunId,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default);

  // A controlled non-execution. Restricted to the skip statuses by the aggregate, so this cannot quietly
  // become a way to write Succeeded or Failed.
  Task MarkSkippedAsync(
    long backupRunId,
    TenantDatabaseBackupRunStatus status,
    string? reasonSummary,
    string actor,
    CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseBackupRunStore(
  PlatformDbContext dbContext,
  IDateTimeProvider clock) : ITenantDatabaseBackupRunStore
{
  public async Task<long> StartAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    string? destinationKey,
    string actor,
    CancellationToken cancellationToken = default)
  {
    var run = TenantDatabaseBackupRun.Start(tenantDatabaseId, operation, destinationKey, actor, clock.UtcNow);
    if (run.IsFailure)
    {
      throw new InvalidOperationException(run.Error.Code);
    }

    dbContext.TenantDatabaseBackupRuns.Add(run.Value);
    await dbContext.SaveChangesAsync(cancellationToken);
    return run.Value.Id;
  }

  public Task MarkSucceededAsync(
    long backupRunId,
    TenantDatabaseBackupProviderResult result,
    string actor,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(result);

    return ApplyAsync(backupRunId, run => run.Succeed(
      // Never null by the time this is called — the executor only reaches success after reconciliation —
      // but the aggregate refuses a blank identity independently, which is the actual guarantee.
      result.ProviderBackupSetIdentity ?? string.Empty,
      result.SafeArtifactReference,
      result.BackupSizeBytes,
      result.FirstLsn,
      result.LastLsn,
      result.DatabaseBackupLsn,
      result.BackupSetGuid,
      actor,
      // The provider's OBSERVED finish time, not the local clock: the authoritative completion moment is
      // the one SQL Server recorded, and readiness timestamps are derived from it.
      result.CompletedUtc ?? clock.UtcNow),
      cancellationToken);
  }

  public Task MarkFailedAsync(
    long backupRunId,
    string? errorSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(backupRunId, run => run.Fail(errorSummary, actor, clock.UtcNow), cancellationToken);

  public Task MarkSkippedAsync(
    long backupRunId,
    TenantDatabaseBackupRunStatus status,
    string? reasonSummary,
    string actor,
    CancellationToken cancellationToken = default) =>
    ApplyAsync(backupRunId, run => run.Skip(status, reasonSummary, actor, clock.UtcNow), cancellationToken);

  private async Task ApplyAsync(
    long backupRunId,
    Func<TenantDatabaseBackupRun, SSAS.BuildingBlocks.Domain.Result> mutate,
    CancellationToken cancellationToken)
  {
    var run = await dbContext.TenantDatabaseBackupRuns
      .SingleOrDefaultAsync(item => item.Id == backupRunId, cancellationToken);
    if (run is null)
    {
      return;
    }

    var applied = mutate(run);
    if (applied.IsFailure)
    {
      // The aggregate refused the transition. Nothing was mutated, so nothing is saved — the run keeps the
      // truth it already had rather than acquiring a state the domain rejects.
      dbContext.Entry(run).State = EntityState.Detached;
      throw new InvalidOperationException(applied.Error.Code);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    dbContext.Entry(run).State = EntityState.Detached;
  }
}
