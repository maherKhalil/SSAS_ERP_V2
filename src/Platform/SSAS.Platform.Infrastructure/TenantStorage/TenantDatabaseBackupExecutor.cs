using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Owns the RUN LIFECYCLE for one managed backup (ADR-022 §15), following the ADR-018 precedent where the
// migration orchestrator owns lifecycle and the ownership primitive stays separate.
//
// The division of labour is the point: this decides WHETHER a backup may happen and what its outcome MEANS;
// the provider decides HOW SQL Server performs it and returns evidence. Infrastructure never lets a provider
// mutate Platform domain state directly.
//
// Invoked EXPLICITLY, one database at a time. There is no scheduler, timer or hosted service behind it —
// fleet scheduling is Phase C and stays blocked until the session-loss question is settled (ADR-022 §14).
public sealed class TenantDatabaseBackupExecutor(
  ITenantDatabaseRegistryReadRepository registry,
  ITenantDatabaseBackupReadRepository backupReads,
  ITenantDatabaseBackupRunStore runStore,
  ITenantDatabaseBackupProvider provider,
  ITenantDatabaseRecoveryReadinessWriter recoveryWriter) : ITenantDatabaseBackupExecutor
{
  private const string BackupActor = "tenant-backup-executor";

  public Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    CancellationToken cancellationToken = default) =>
    RunAsync(tenantDatabaseId, operation, dueAnchorUtc: null, cancellationToken);

  public Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteScheduledAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    DateTimeOffset? dueAnchorUtc,
    CancellationToken cancellationToken = default) =>
    // A scheduled call with no anchor still means "no platform backup of this type has ever succeeded", so
    // DateTimeOffset.MinValue is the correct floor: any platform backup of this operation at all supersedes
    // the decision.
    RunAsync(tenantDatabaseId, operation, dueAnchorUtc ?? DateTimeOffset.MinValue, cancellationToken);

  private async Task<Result<TenantDatabaseBackupExecutionOutcome>> RunAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    DateTimeOffset? dueAnchorUtc,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(operation);

    var descriptor = await FindDescriptorAsync(tenantDatabaseId, cancellationToken);
    if (descriptor is null)
    {
      return Result.Failure<TenantDatabaseBackupExecutionOutcome>(TenantStorageErrors.TenantDatabaseRequired);
    }

    // CustomerManaged is refused HERE and again in the connection factory (ADR-021). There is no supported
    // runtime connectivity path to a customer's server, and inventing one for backups would contradict the
    // decision that put backup custody with the customer in the first place.
    if (descriptor.HostingMode != TenantDatabaseHostingMode.PlatformManaged)
    {
      return Result.Failure<TenantDatabaseBackupExecutionOutcome>(
        TenantStorageErrors.BackupNotPermittedByManagementMode);
    }

    var policy = await backupReads.FindPolicyAsync(tenantDatabaseId, cancellationToken);
    if (policy is null)
    {
      return Result.Failure<TenantDatabaseBackupExecutionOutcome>(TenantStorageErrors.BackupPolicyNotConfigured);
    }

    // AUTHORITY, checked before a run row exists so a forbidden request leaves no misleading history.
    //
    // PlatformAfterApproval BLOCKS in Phase B. There is no approval workflow yet, and ADR-022 §5 states that
    // absence of approval is DENIAL — so the alternative would be a caller-supplied "approved" flag, which
    // is a security hole wearing a parameter's clothing.
    var authority = policy.ManagementMode switch
    {
      TenantDatabaseBackupManagementMode.AutomaticByPlatform => null,
      _ => TenantStorageErrors.BackupNotPermittedByManagementMode
    };

    if (authority is not null || !policy.Enabled)
    {
      var reason = authority ?? TenantStorageErrors.BackupPolicyDisabled;
      var blockedRunId = await runStore.StartAsync(
        tenantDatabaseId, operation, policy.DestinationKey, BackupActor, cancellationToken);
      await runStore.MarkSkippedAsync(
        blockedRunId, TenantDatabaseBackupRunStatus.BlockedByPolicy, reason.Code, BackupActor, cancellationToken);

      return Result.Success(new TenantDatabaseBackupExecutionOutcome(
        tenantDatabaseId, blockedRunId, operation.ProviderKey, operation.OperationCode,
        TenantDatabaseBackupRunStatus.BlockedByPolicy, null, reason.Code));
    }

    // The run exists before the operation does, so an interrupted execution still leaves evidence that
    // something was attempted. Each Platform write below is SHORT — no transaction spans the backup itself.
    var runId = await runStore.StartAsync(
      tenantDatabaseId, operation, policy.DestinationKey, BackupActor, cancellationToken);

    TenantDatabaseBackupProviderResult result;
    try
    {
      result = await provider.ExecuteAsync(
        new TenantDatabaseBackupRequest(
          tenantDatabaseId,
          operation,
          runId,
          new TenantDatabaseBackupOptions(policy.DestinationKey ?? string.Empty, policy.CompressionMode),
          dueAnchorUtc),
        cancellationToken);
    }
    catch (OperationCanceledException)
    {
      // A RUN MUST NEVER BE STRANDED IN `Running`.
      //
      // Cancellation reaches here from a client-side abort, and Phase B established that aborting the client
      // does not reliably stop a server-side BACKUP — so this cannot claim the operation stopped. It records
      // a terminal Failed with an explicit reason and leaves the rest to the mandatory in-flight guard,
      // which is what prevents the next attempt from colliding with a backup that may still be running.
      //
      // Recording Failed here is deliberately conservative: it never marks success without reconciled
      // evidence, and it never advances a successful-backup timestamp.
      await runStore.MarkFailedAsync(
        runId, TenantStorageErrors.BackupExecutionCancelled.Code, BackupActor, CancellationToken.None);
      throw;
    }

    return Result.Success(
      await RecordAsync(tenantDatabaseId, descriptor, policy, runId, operation, result, cancellationToken));
  }

  private async Task<TenantDatabaseBackupExecutionOutcome> RecordAsync(
    long tenantDatabaseId,
    TenantDatabaseDescriptor descriptor,
    TenantDatabaseBackupPolicyRecord policy,
    long runId,
    TenantDatabaseBackupOperation operation,
    TenantDatabaseBackupProviderResult result,
    CancellationToken cancellationToken)
  {
    switch (result.Outcome)
    {
      case TenantDatabaseBackupOutcome.Succeeded:
        await runStore.MarkSucceededAsync(runId, result, BackupActor, cancellationToken);

        // Recovery observation follows PROVEN evidence, never a completed command. It is written through the
        // existing dimension-scoped recovery writer, so it touches nothing but the recovery dimension.
        //
        // Deliberately AFTER the run is recorded and deliberately not part of run truth: if this write loses
        // its RowVersion race repeatedly it abandons silently (a known carried finding), which leaves the
        // observation stale rather than the run's history wrong. Stale readiness is caught by freshness;
        // a corrupted run record would not be.
        await RecordRecoveryObservationAsync(
          tenantDatabaseId, descriptor, policy, operation, result, cancellationToken);

        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.Succeeded, result.ProviderBackupSetIdentity, null);

      case TenantDatabaseBackupOutcome.SkippedOwnershipHeld:
        await runStore.MarkSkippedAsync(runId, TenantDatabaseBackupRunStatus.SkippedOwnershipHeld,
          result.SafeErrorSummary, BackupActor, cancellationToken);
        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.SkippedOwnershipHeld, null, result.SafeErrorSummary);

      case TenantDatabaseBackupOutcome.SkippedSupersededByRecentBackup:
        // Recorded under SkippedOwnershipHeld, with the precise reason in the safe error summary.
        //
        // The status family is right — this run did not execute because another worker's ownership of the
        // same due event covered it — and a dedicated status would mean a new enum value, which the
        // CK_TenantDatabaseBackupRuns_Status check constraint makes a schema migration. Phase C is a
        // no-schema slice, and BackupSupersededByRecentRun carries the distinction without one.
        await runStore.MarkSkippedAsync(runId, TenantDatabaseBackupRunStatus.SkippedOwnershipHeld,
          result.SafeErrorSummary, BackupActor, cancellationToken);
        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.SkippedOwnershipHeld, null, result.SafeErrorSummary);

      case TenantDatabaseBackupOutcome.SkippedInFlightOperation:
        await runStore.MarkSkippedAsync(runId, TenantDatabaseBackupRunStatus.SkippedInFlightOperation,
          result.SafeErrorSummary, BackupActor, cancellationToken);
        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.SkippedInFlightOperation, null, result.SafeErrorSummary);

      case TenantDatabaseBackupOutcome.BlockedByPrecondition:
        await runStore.MarkSkippedAsync(runId, TenantDatabaseBackupRunStatus.BlockedByPolicy,
          result.SafeErrorSummary, BackupActor, cancellationToken);
        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.BlockedByPolicy, null, result.SafeErrorSummary);

      default:
        await runStore.MarkFailedAsync(runId, result.SafeErrorSummary, BackupActor, cancellationToken);
        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.Failed, null, result.SafeErrorSummary);
    }
  }

  // Updates ONLY the timestamp for the operation that actually succeeded, using SQL Server's OBSERVED finish
  // time rather than a local clock reading — the authoritative moment is the one the server recorded.
  //
  // The readiness STATUS it reports can never be Protected, and the reason is honest rather than merely
  // conservative: this path has observed neither the database's recovery model nor its chain continuity, and
  // `Protected` asserts both (ADR-022 §6). The full evaluation belongs to the verification/readiness sweep,
  // which observes them.
  //
  // WHAT PHASE D FIXES HERE is the mislabelling. Phase B reported `VerificationOverdue` unconditionally,
  // which called a database overdue for restore verification its policy had never asked for. The status is
  // now derived from the policy's actual verification obligation against the evidence already held
  // (ADR-022 §6, v1.2).
  private async Task RecordRecoveryObservationAsync(
    long tenantDatabaseId,
    TenantDatabaseDescriptor descriptor,
    TenantDatabaseBackupPolicyRecord policy,
    TenantDatabaseBackupOperation operation,
    TenantDatabaseBackupProviderResult result,
    CancellationToken cancellationToken)
  {
    var observedUtc = result.CompletedUtc;
    if (observedUtc is null)
    {
      return;
    }

    var evidence = await backupReads.FindRecoveryEvidenceAsync(tenantDatabaseId, cancellationToken);
    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup(
      new TenantDatabaseRecoveryReadinessInputs(
        descriptor.HostingMode,
        PolicyExists: true,
        policy.Enabled,
        policy.ManagementMode,
        policy.FullBackupIntervalMinutes,
        policy.DifferentialBackupIntervalMinutes,
        policy.TransactionLogBackupIntervalMinutes,
        policy.RestoreVerificationIntervalDays,
        policy.MaximumBackupAgeMinutes,
        evidence?.LastSuccessfulFullBackupUtc,
        evidence?.LastSuccessfulDifferentialBackupUtc,
        evidence?.LastSuccessfulLogBackupUtc,
        evidence?.LastRestoreVerificationUtc),
      observedUtc.Value);

    await recoveryWriter.RecordRecoveryReadinessAsync(
      tenantDatabaseId,
      status,
      BackupActor,
      lastSuccessfulFullBackupUtc: operation.OperationCode == "Full" ? observedUtc : null,
      lastSuccessfulDifferentialBackupUtc: operation.OperationCode == "Differential" ? observedUtc : null,
      lastSuccessfulLogBackupUtc: operation.OperationCode == "TransactionLog" ? observedUtc : null,
      cancellationToken: cancellationToken);
  }

  private async Task<TenantDatabaseDescriptor?> FindDescriptorAsync(
    long tenantDatabaseId,
    CancellationToken cancellationToken)
  {
    var page = await registry.ListPhysicalDatabasesAsync(tenantDatabaseId - 1, 1, cancellationToken);
    var descriptor = page.Count > 0 ? page[0] : null;
    return descriptor?.TenantDatabaseId == tenantDatabaseId ? descriptor : null;
  }

  private static TenantDatabaseBackupExecutionOutcome Outcome(
    long tenantDatabaseId,
    long runId,
    TenantDatabaseBackupOperation operation,
    TenantDatabaseBackupRunStatus status,
    string? providerIdentity,
    string? safeError) =>
    new(tenantDatabaseId, runId, operation.ProviderKey, operation.OperationCode, status, providerIdentity, safeError);
}
