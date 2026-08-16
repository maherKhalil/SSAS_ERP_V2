using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The first production orchestration boundary permitted to call the D6 restore provider (ADR-022 §17).
// Every Platform read/write is short; no Platform transaction survives a network connection, RESTORE, or
// probe. The executor decides whether work may happen and what the evidence means. The provider and probe
// only report what SQL Server established.
internal sealed class TenantDatabaseRestoreVerificationExecutor(
  ITenantDatabaseRegistryReadRepository registry,
  ITenantDatabaseBackupReadRepository backupReads,
  ITenantDatabaseRestoreVerificationRunStore runStore,
  ITenantDatabaseRestoreVerificationProvider provider,
  ITenantDatabaseRestoreVerificationProbe probe,
  ITenantDatabaseVerificationConnectionFactory verificationConnections,
  ITenantDatabaseRecoveryReadinessWriter recoveryWriter,
  IOptions<TenantDatabaseRestoreVerificationOptions> verificationOptions,
  IDateTimeProvider clock) : ITenantDatabaseRestoreVerificationExecutor
{
  private const string Actor = "tenant-restore-verification-executor";

  public async Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> ExecuteAsync(
    long tenantDatabaseId,
    long expectedVerificationRunId,
    TenantDatabaseRestoreDepth requestedDepth,
    CancellationToken cancellationToken = default)
  {
    if (tenantDatabaseId <= 0 || expectedVerificationRunId <= 0 || !Enum.IsDefined(requestedDepth))
    {
      return Result.Failure<TenantDatabaseRestoreVerificationExecutionOutcome>(
        TenantStorageErrors.RestoreVerificationTargetDrifted);
    }

    var run = await runStore.FindAsync(expectedVerificationRunId, cancellationToken);
    if (run is null || run.VerificationRunId != expectedVerificationRunId ||
      run.TenantDatabaseId != tenantDatabaseId)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationExecutionOutcome>(
        TenantStorageErrors.RestoreVerificationTargetDrifted);
    }

    // Exact operation recheck. A stale worker neither substitutes another active run nor replays one that
    // has already moved. Restoring is intentionally active for LOW-C reconciliation; terminal rows remain
    // authoritative and are never rewritten by a stale executor.
    if (run.Status != TenantDatabaseRestoreVerificationStatus.Admitted)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationExecutionOutcome>(
        TenantStorageErrors.RestoreVerificationNotAdmitted);
    }

    if (run.Depth != requestedDepth || run.SourceBackupRunId <= 0)
    {
      return await RefuseAdmittedAsync(
        run, TenantStorageErrors.RestoreVerificationTargetDrifted.Code, cancellationToken);
    }

    TenantDatabaseDescriptor? database;
    TenantDatabaseBackupPolicyRecord? policy;
    IReadOnlyList<TenantDatabaseDescriptor> registered;
    string verificationDatabaseName;

    try
    {
      registered = await registry.ListPhysicalDatabasesAsync(0, int.MaxValue, cancellationToken);
      database = registered.SingleOrDefault(candidate => candidate.TenantDatabaseId == tenantDatabaseId);
      if (database is null)
      {
        // Explicit physical-row existence check. No FK or missing-baseline side effect stands in for it.
        return await RefuseAdmittedAsync(
          run, TenantStorageErrors.TenantDatabaseRequired.Code, cancellationToken);
      }

      policy = await backupReads.FindPolicyAsync(tenantDatabaseId, cancellationToken);
      var eligibilityError = EligibilityError(database, policy);
      if (eligibilityError is not null)
      {
        return await RefuseAdmittedAsync(run, eligibilityError.Code, cancellationToken);
      }

      var options = verificationOptions.Value;
      if (!options.Enabled || string.IsNullOrWhiteSpace(options.RestoreServerKey) ||
        string.IsNullOrWhiteSpace(options.RestoreDataRoot) ||
        string.IsNullOrWhiteSpace(options.RestoreLogRoot) ||
        options.OrphanCleanupGracePeriod <= TimeSpan.Zero ||
        !string.Equals(options.RestoreServerKey.Trim(), run.RestoreServerKey, StringComparison.Ordinal))
      {
        return await RefuseAdmittedAsync(
          run, TenantStorageErrors.RestoreVerificationTargetDrifted.Code, cancellationToken);
      }

      verificationDatabaseName = TenantDatabaseVerificationNaming.ForRun(tenantDatabaseId, run.VerificationRunId);
      if (!TenantDatabaseVerificationTargetGuard.CanRestoreInto(
        verificationDatabaseName,
        tenantDatabaseId,
        run.VerificationRunId,
        registered.Select(candidate => candidate.DatabaseName)))
      {
        return await RefuseAdmittedAsync(
          run, TenantStorageErrors.RestoreVerificationTargetNameNotSafe.Code, cancellationToken);
      }

      // Validate the current dedicated credential/target mapping and isolation before taking execution.
      // Creating a SqlConnection performs no network I/O; it is disposed immediately after validation.
      var target = new TenantDatabaseVerificationTarget(run.RestoreServerKey, database.ServerKey);
      var targetConnection = verificationConnections.Create(target);
      if (targetConnection.IsFailure)
      {
        return await RefuseAdmittedAsync(run, targetConnection.Error.Code, cancellationToken);
      }

      await targetConnection.Value.DisposeAsync();

      // CAS is the only authority to execute. If another actor moved this exact row, provider invocation is
      // forbidden even though our earlier read observed Admitted.
      var begun = await runStore.BeginRestoreAsync(
        run.VerificationRunId, verificationDatabaseName, Actor, cancellationToken);
      if (begun.IsFailure)
      {
        return Result.Failure<TenantDatabaseRestoreVerificationExecutionOutcome>(begun.Error);
      }
    }
    catch (OperationCanceledException)
    {
      await TryMarkUnavailableAsync(run.VerificationRunId, "ExecutionCancelled", CancellationToken.None);
      throw;
    }
    catch (Exception exception)
    {
      return await InfrastructureUnavailableAsync(run, Safe(exception), CancellationToken.None);
    }

    try
    {
      // Freeze current Platform-owned evidence only after this executor owns the lifecycle row. There is no
      // source-msdb fallback. CheckpointLsn is present in this production projection for the selector.
      var candidates = await backupReads.ListChainCandidatesAsync(tenantDatabaseId, cancellationToken);
      var selected = TenantDatabaseBackupChainSelector.Select(candidates, requestedDepth);
      if (selected.IsFailure)
      {
        return selected.Error.Code switch
        {
          var code when code == TenantStorageErrors.RestoreChainBroken.Code =>
            await VerificationFailedAsync(
              run, TenantDatabaseVerificationFailure.RequiredChainBreak,
              achievedDepth: null, code, cancellationToken),

          var code when code == TenantStorageErrors.RestoreChainMetadataUnavailable.Code =>
            await InfrastructureUnavailableAsync(run, code, cancellationToken),

          _ => await VerificationFailedAsync(
            run, TenantDatabaseVerificationFailure.BaselineRestoreFailed,
            achievedDepth: null, selected.Error.Code, cancellationToken)
        };
      }

      // The admitted baseline identity is immutable. A newly selected full means the due state moved after
      // admission; this operation is stale and must not restore either the old or the new chain.
      var full = selected.Value.Steps[0].Artifact;
      if (full.BackupRunId != run.SourceBackupRunId ||
        selected.Value.Steps[0].Kind != TenantDatabaseRestoreStepKind.Full)
      {
        return await InfrastructureUnavailableAsync(
          run, TenantStorageErrors.RestoreVerificationTargetDrifted.Code, cancellationToken);
      }

      // EXECUTION-TIME RECHECK, immediately before the first component allowed to issue RESTORE. The
      // earlier check established admission eligibility; this one closes drift while the lifecycle CAS and
      // chain read were happening. Every fact is authoritative again, including the exact now-Restoring
      // operation and its durable target name.
      var currentRun = await runStore.FindAsync(run.VerificationRunId, cancellationToken);
      var currentRegistered = await registry.ListPhysicalDatabasesAsync(0, int.MaxValue, cancellationToken);
      var currentDatabase = currentRegistered.SingleOrDefault(
        candidate => candidate.TenantDatabaseId == tenantDatabaseId);
      var currentPolicy = await backupReads.FindPolicyAsync(tenantDatabaseId, cancellationToken);
      var currentOptions = verificationOptions.Value;

      if (currentRun is null || currentDatabase is null ||
        currentRun.VerificationRunId != run.VerificationRunId ||
        currentRun.TenantDatabaseId != tenantDatabaseId ||
        currentRun.SourceBackupRunId != run.SourceBackupRunId ||
        currentRun.Depth != requestedDepth ||
        currentRun.Status != TenantDatabaseRestoreVerificationStatus.Restoring ||
        !string.Equals(currentRun.RestoreServerKey, run.RestoreServerKey, StringComparison.Ordinal) ||
        !string.Equals(currentRun.VerificationDatabaseName, verificationDatabaseName, StringComparison.Ordinal) ||
        EligibilityError(currentDatabase, currentPolicy) is not null ||
        !CurrentVerificationConfigurationMatches(currentOptions, run.RestoreServerKey) ||
        !TenantDatabaseVerificationTargetGuard.CanRestoreInto(
          verificationDatabaseName,
          tenantDatabaseId,
          run.VerificationRunId,
          currentRegistered.Select(candidate => candidate.DatabaseName)))
      {
        return await InfrastructureUnavailableAsync(
          run, TenantStorageErrors.RestoreVerificationNotEligible.Code, cancellationToken);
      }

      var currentTarget = verificationConnections.Create(
        new TenantDatabaseVerificationTarget(run.RestoreServerKey, currentDatabase.ServerKey));
      if (currentTarget.IsFailure)
      {
        return await InfrastructureUnavailableAsync(run, currentTarget.Error.Code, cancellationToken);
      }
      await currentTarget.Value.DisposeAsync();

      database = currentDatabase;

      TenantDatabaseRestoreVerificationResult restored;
      try
      {
        restored = await provider.ExecuteAsync(
          new TenantDatabaseRestoreVerificationRequest(
            tenantDatabaseId,
            run.VerificationRunId,
            selected.Value,
            run.RestoreServerKey,
            database!.ServerKey,
            verificationDatabaseName),
          cancellationToken);
      }
      catch (OperationCanceledException)
      {
        await TryMarkUnavailableAsync(run.VerificationRunId, "ExecutionCancelled", CancellationToken.None);
        throw;
      }
      catch (Exception exception)
      {
        return await InfrastructureUnavailableAsync(run, Safe(exception), cancellationToken);
      }

      if (restored.Outcome != TenantDatabaseRestoreVerificationOutcome.RestoredAndOnline)
      {
        return await RecordProviderFailureAsync(run, restored, cancellationToken);
      }

      if (!string.Equals(
        restored.VerificationDatabaseName, verificationDatabaseName, StringComparison.Ordinal) ||
        restored.AchievedDepth is not { } achievedDepth)
      {
        return await VerificationFailedAsync(
          run,
          requestedDepth == TenantDatabaseRestoreDepth.Full
            ? TenantDatabaseVerificationFailure.BaselineRestoreFailed
            : TenantDatabaseVerificationFailure.DeeperChainRestoreFailed,
          restored.AchievedDepth,
          TenantStorageErrors.RestoreVerificationDepthNotAchieved.Code,
          cancellationToken);
      }

      // Ordered depth is load-bearing: provider success never substitutes for the requested guarantee.
      if (achievedDepth < requestedDepth)
      {
        return await VerificationFailedAsync(
          run,
          TenantDatabaseVerificationFailure.DeeperChainRestoreFailed,
          achievedDepth,
          TenantStorageErrors.RestoreVerificationDepthNotAchieved.Code,
          cancellationToken);
      }

      TenantDatabaseRestoreProbeResult probed;
      try
      {
        probed = await probe.ExecuteAsync(
          new TenantDatabaseRestoreProbeRequest(
            tenantDatabaseId,
            run.VerificationRunId,
            run.RestoreServerKey,
            database!.ServerKey,
            verificationDatabaseName),
          cancellationToken);
      }
      catch (OperationCanceledException)
      {
        await TryMarkUnavailableAsync(run.VerificationRunId, "ExecutionCancelled", CancellationToken.None);
        throw;
      }
      catch (Exception exception)
      {
        return await InfrastructureUnavailableAsync(run, Safe(exception), cancellationToken);
      }

      if (probed.Outcome == TenantDatabaseRestoreProbeOutcome.Unavailable)
      {
        return await InfrastructureUnavailableAsync(run, probed.SafeErrorSummary, cancellationToken);
      }

      if (probed.Outcome != TenantDatabaseRestoreProbeOutcome.Succeeded ||
        probed.ObservedRecoveryModel is null)
      {
        return await VerificationFailedAsync(
          run,
          TenantDatabaseVerificationFailure.PostRestoreProbeFailed,
          achievedDepth,
          probed.SafeErrorSummary ?? TenantStorageErrors.RestoreVerificationSchemaPositionUnexpected.Code,
          cancellationToken);
      }

      Result<DateTimeOffset> persisted;
      try
      {
        persisted = await runStore.MarkSucceededAndRecordEvidenceAsync(
          run.VerificationRunId, run.SourceBackupRunId, Actor, cancellationToken);
      }
      catch (Exception exception)
      {
        return await InfrastructureUnavailableAsync(run, Safe(exception), cancellationToken);
      }

      if (persisted.IsFailure)
      {
        return await InfrastructureUnavailableAsync(run, persisted.Error.Code, cancellationToken);
      }

      // Projection follows durable success. If it fails, the run and exact source backup still carry the
      // authoritative truth and stale admission is already rejected; a later reconciliation may project it.
      string? projectionError = null;
      try
      {
        await RecordSuccessfulReadinessAsync(
          tenantDatabaseId, probed.ObservedRecoveryModel.Value, persisted.Value, cancellationToken);
      }
      catch (Exception exception)
      {
        projectionError = Safe(exception);
      }

      return Result.Success(new TenantDatabaseRestoreVerificationExecutionOutcome(
        tenantDatabaseId,
        run.VerificationRunId,
        TenantDatabaseRestoreVerificationStatus.Succeeded,
        achievedDepth,
        RestoreVerified: true,
        projectionError));
    }
    catch (OperationCanceledException)
    {
      await TryMarkUnavailableAsync(run.VerificationRunId, "ExecutionCancelled", CancellationToken.None);
      throw;
    }
    catch (Exception exception)
    {
      return await InfrastructureUnavailableAsync(run, Safe(exception), CancellationToken.None);
    }
  }

  private async Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> RecordProviderFailureAsync(
    TenantDatabaseRestoreVerificationRunRecord run,
    TenantDatabaseRestoreVerificationResult restored,
    CancellationToken cancellationToken) => restored.Outcome switch
  {
    TenantDatabaseRestoreVerificationOutcome.InfrastructureUnavailable or
      TenantDatabaseRestoreVerificationOutcome.BlockedByPrecondition =>
      await InfrastructureUnavailableAsync(run, restored.SafeErrorSummary, cancellationToken),

    // A durable selected artifact that cannot be opened is a known required-chain break, including when the
    // missing segment is deeper than Full. It is not the same as a deeper RESTORE statement failing after a
    // usable baseline was already established.
    TenantDatabaseRestoreVerificationOutcome.ArtifactUnavailable =>
      await VerificationFailedAsync(
        run, TenantDatabaseVerificationFailure.RequiredChainBreak,
        restored.AchievedDepth, restored.SafeErrorSummary, cancellationToken),

    TenantDatabaseRestoreVerificationOutcome.RestoreFailed when restored.RestoredStepCount > 0 &&
      run.Depth > TenantDatabaseRestoreDepth.Full =>
      await VerificationFailedAsync(
        run, TenantDatabaseVerificationFailure.DeeperChainRestoreFailed,
        restored.AchievedDepth, restored.SafeErrorSummary, cancellationToken),

    _ => await VerificationFailedAsync(
      run, TenantDatabaseVerificationFailure.BaselineRestoreFailed,
      restored.AchievedDepth, restored.SafeErrorSummary, cancellationToken)
  };

  private async Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> RefuseAdmittedAsync(
    TenantDatabaseRestoreVerificationRunRecord run,
    string reason,
    CancellationToken cancellationToken) =>
    await InfrastructureUnavailableAsync(run, reason, cancellationToken);

  private async Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> InfrastructureUnavailableAsync(
    TenantDatabaseRestoreVerificationRunRecord run,
    string? reason,
    CancellationToken cancellationToken)
  {
    await TryMarkUnavailableAsync(run.VerificationRunId, reason, cancellationToken);
    await TryRecordFailureReadinessAsync(
      run.TenantDatabaseId,
      TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable,
      cancellationToken);

    return Result.Success(Outcome(
      run,
      TenantDatabaseRestoreVerificationStatus.InfrastructureUnavailable,
      achievedDepth: null,
      restoreVerified: false,
      reason));
  }

  private async Task<Result<TenantDatabaseRestoreVerificationExecutionOutcome>> VerificationFailedAsync(
    TenantDatabaseRestoreVerificationRunRecord run,
    TenantDatabaseVerificationFailure failure,
    TenantDatabaseRestoreDepth? achievedDepth,
    string? reason,
    CancellationToken cancellationToken)
  {
    var marked = await runStore.MarkFailedAsync(run.VerificationRunId, reason, Actor, cancellationToken);
    if (marked.IsFailure)
    {
      return Result.Failure<TenantDatabaseRestoreVerificationExecutionOutcome>(marked.Error);
    }

    await TryRecordFailureReadinessAsync(run.TenantDatabaseId, failure, cancellationToken);
    return Result.Success(Outcome(
      run, TenantDatabaseRestoreVerificationStatus.Failed, achievedDepth, false, reason));
  }

  private async Task RecordSuccessfulReadinessAsync(
    long tenantDatabaseId,
    TenantDatabaseRecoveryModel recoveryModel,
    DateTimeOffset completedUtc,
    CancellationToken cancellationToken)
  {
    var inputs = await BuildCurrentReadinessInputsAsync(
      tenantDatabaseId, recoveryModel, cancellationToken);
    if (inputs is null)
    {
      throw new InvalidOperationException(TenantStorageErrors.TenantDatabaseRequired.Code);
    }

    inputs = inputs with
    {
      LastRestoreVerificationUtc = completedUtc,
      PlatformChainBreakDetected = false
    };
    var status = TenantDatabaseRecoveryReadinessEvaluator.Evaluate(inputs, completedUtc);

    await recoveryWriter.RecordRecoveryReadinessAsync(
      tenantDatabaseId,
      status,
      Actor,
      lastRestoreVerificationUtc: completedUtc,
      cancellationToken: cancellationToken);
  }

  private async Task TryRecordFailureReadinessAsync(
    long tenantDatabaseId,
    TenantDatabaseVerificationFailure failure,
    CancellationToken cancellationToken)
  {
    try
    {
      var inputs = await BuildCurrentReadinessInputsAsync(
        tenantDatabaseId, observedRecoveryModel: null, cancellationToken);
      if (inputs is null)
      {
        return;
      }

      inputs = inputs with
      {
        PlatformChainBreakDetected = failure == TenantDatabaseVerificationFailure.RequiredChainBreak ||
          failure == TenantDatabaseVerificationFailure.VerificationInfrastructureUnavailable &&
          inputs.PlatformChainBreakDetected
      };
      var status = TenantDatabaseRecoveryReadinessEvaluator.EvaluateAfterVerificationFailure(
        failure, inputs, clock.UtcNow);
      if (status is { } observed)
      {
        await recoveryWriter.RecordRecoveryReadinessAsync(
          tenantDatabaseId, observed, Actor, cancellationToken: cancellationToken);
      }
    }
    catch
    {
      // The terminal verification row is authoritative. A projection failure must not strand Restoring or
      // replace the classified verification result with a second, less useful exception.
    }
  }

  // One reconstruction path for every D7 exit. Policy and evidence are reloaded after the outcome rather
  // than reusing admission/execution snapshots. A removed policy is represented explicitly as absent, and
  // an unobserved recovery model stays null so the D1 evaluator can conservatively refuse false protection.
  private async Task<TenantDatabaseRecoveryReadinessInputs?> BuildCurrentReadinessInputsAsync(
    long tenantDatabaseId,
    TenantDatabaseRecoveryModel? observedRecoveryModel,
    CancellationToken cancellationToken)
  {
    var registered = await registry.ListPhysicalDatabasesAsync(0, int.MaxValue, cancellationToken);
    var database = registered.SingleOrDefault(candidate => candidate.TenantDatabaseId == tenantDatabaseId);
    if (database is null)
    {
      return null;
    }

    var policy = await backupReads.FindPolicyAsync(tenantDatabaseId, cancellationToken);
    var evidence = await backupReads.FindRecoveryEvidenceAsync(tenantDatabaseId, cancellationToken);
    return Inputs(database, policy, evidence, observedRecoveryModel);
  }

  private async Task TryMarkUnavailableAsync(
    long verificationRunId,
    string? reason,
    CancellationToken cancellationToken)
  {
    try
    {
      await runStore.MarkInfrastructureUnavailableAsync(
        verificationRunId, reason, Actor, cancellationToken);
    }
    catch
    {
      // Best effort only. A persistence outage cannot be repaired by retrying inside the same failing exit.
    }
  }

  private static Error? EligibilityError(
    TenantDatabaseDescriptor database,
    TenantDatabaseBackupPolicyRecord? policy)
  {
    if (database.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      database.ProvisioningStatus != TenantDatabaseProvisioningStatus.Ready)
    {
      return TenantStorageErrors.RestoreVerificationNotEligible;
    }

    if (policy is null || !policy.Enabled ||
      policy.ManagementMode != TenantDatabaseBackupManagementMode.AutomaticByPlatform)
    {
      return TenantStorageErrors.RestoreVerificationNotEligible;
    }

    return null;
  }

  private static bool CurrentVerificationConfigurationMatches(
    TenantDatabaseRestoreVerificationOptions options,
    string restoreServerKey) =>
    options.Enabled &&
    !string.IsNullOrWhiteSpace(options.RestoreServerKey) &&
    !string.IsNullOrWhiteSpace(options.RestoreDataRoot) &&
    !string.IsNullOrWhiteSpace(options.RestoreLogRoot) &&
    options.OrphanCleanupGracePeriod > TimeSpan.Zero &&
    string.Equals(options.RestoreServerKey.Trim(), restoreServerKey, StringComparison.Ordinal);

  private static TenantDatabaseRecoveryReadinessInputs Inputs(
    TenantDatabaseDescriptor database,
    TenantDatabaseBackupPolicyRecord? policy,
    TenantDatabaseRecoveryEvidenceRecord? evidence,
    TenantDatabaseRecoveryModel? observedRecoveryModel) =>
    new(
      database.HostingMode,
      PolicyExists: policy is not null,
      PolicyEnabled: policy?.Enabled ?? false,
      ManagementMode: policy?.ManagementMode ?? TenantDatabaseBackupManagementMode.CustomerDba,
      FullBackupIntervalMinutes: policy?.FullBackupIntervalMinutes,
      DifferentialBackupIntervalMinutes: policy?.DifferentialBackupIntervalMinutes,
      TransactionLogBackupIntervalMinutes: policy?.TransactionLogBackupIntervalMinutes,
      RestoreVerificationIntervalDays: policy?.RestoreVerificationIntervalDays,
      MaximumBackupAgeMinutes: policy?.MaximumBackupAgeMinutes,
      evidence?.LastSuccessfulFullBackupUtc,
      evidence?.LastSuccessfulDifferentialBackupUtc,
      evidence?.LastSuccessfulLogBackupUtc,
      evidence?.LastRestoreVerificationUtc,
      observedRecoveryModel ??
        (evidence?.RecoveryReadinessStatus == TenantDatabaseRecoveryReadinessStatus.RecoveryModelInvalid
          ? TenantDatabaseRecoveryModel.Simple
          : null),
      PlatformChainBreakDetected:
        evidence?.RecoveryReadinessStatus == TenantDatabaseRecoveryReadinessStatus.Unprotected);

  private static TenantDatabaseRestoreVerificationExecutionOutcome Outcome(
    TenantDatabaseRestoreVerificationRunRecord run,
    TenantDatabaseRestoreVerificationStatus status,
    TenantDatabaseRestoreDepth? achievedDepth,
    bool restoreVerified,
    string? safeError) =>
    new(run.TenantDatabaseId, run.VerificationRunId, status, achievedDepth, restoreVerified, safeError);

  private static string Safe(Exception exception) => $"{exception.GetType().Name}";
}
