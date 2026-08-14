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

  public async Task<Result<TenantDatabaseBackupExecutionOutcome>> ExecuteAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
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

    var result = await provider.ExecuteAsync(
      new TenantDatabaseBackupRequest(
        tenantDatabaseId,
        operation,
        runId,
        new TenantDatabaseBackupOptions(policy.DestinationKey ?? string.Empty, policy.CompressionMode)),
      cancellationToken);

    return Result.Success(await RecordAsync(tenantDatabaseId, runId, operation, result, cancellationToken));
  }

  private async Task<TenantDatabaseBackupExecutionOutcome> RecordAsync(
    long tenantDatabaseId,
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
        await RecordRecoveryObservationAsync(tenantDatabaseId, operation, result, cancellationToken);

        return Outcome(tenantDatabaseId, runId, operation,
          TenantDatabaseBackupRunStatus.Succeeded, result.ProviderBackupSetIdentity, null);

      case TenantDatabaseBackupOutcome.SkippedOwnershipHeld:
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
  // The readiness STATUS it reports can never be Protected: ADR-022 §6 requires restore-verification
  // evidence, and the first actual restore verification belongs to Phase D. A successful full backup moves a
  // database from "no baseline" to "baseline exists but unverified", which is exactly VerificationOverdue.
  private async Task RecordRecoveryObservationAsync(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    TenantDatabaseBackupProviderResult result,
    CancellationToken cancellationToken)
  {
    var observedUtc = result.CompletedUtc;
    if (observedUtc is null)
    {
      return;
    }

    var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterSuccessfulBackup();

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
