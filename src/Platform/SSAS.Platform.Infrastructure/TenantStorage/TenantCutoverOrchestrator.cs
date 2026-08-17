using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// THE SHARED → DEDICATED CUTOVER ORCHESTRATOR (ADR-020, TS-Storage Phase E5).
//
// ONE OWNERSHIP LEASE SPANS EVERY PHASE. Freeze, copy, validation, the activation recheck, the flip and
// finalisation all run while this run holds the operation's session-scoped applock, so no release, no
// standalone copy, no standalone flip and no second Resume can interleave between them. E3 and E4 acquire
// that same resource when called standalone, which is why they are entered here through their
// under-ownership paths instead: taking it twice from two connections is the deadlock E4 review found.
//
// PREFLIGHT BEFORE FREEZE, BECAUSE FREEZING COSTS THE TENANT WRITES. Everything checkable without downtime
// — routing, endpoints, eligibility, target schema, and the recovery activation gate — is checked first,
// and a failure there creates no durable operation at all. None of it is trusted afterwards: the gate and
// the exact validation are both rechecked while frozen, immediately before the flip.
//
// FAILURE AFTER FROZEN LEAVES THE TENANT FROZEN. This looks unhelpful and is deliberate: E3's restart
// safety depends on the source not moving, so resuming writes because a copy failed would make an
// already-copied target stale and convert a retryable failure into an inconsistent one. Releasing is an
// operator's decision and stays an explicit, separate call (ADR-020 "Freeze failure safety").
//
// AFTER THE FLIP COMMITS THERE IS NO WAY BACK. No failure in invalidation, verification, finalisation or
// response delivery may reactivate the Shared assignment; the worst outcome is FinalizationPending, which
// Resume finishes.
internal sealed class TenantCutoverOrchestrator(
  PlatformDbContext platform,
  ITenantCutoverOperationStore operations,
  ITenantCutoverFreezeService freezeService,
  TenantCutoverCopyService copyService,
  TenantCutoverRoutingFlipService flipService,
  ITenantDatabaseRecoveryActivationGate activationGate,
  ITenantDatabaseResolver resolver,
  IOptions<TenantCutoverCopyOptions> optionsAccessor) : ITenantCutoverOrchestrator
{
  private const string Actor = "tenant-cutover-orchestrator";

  public async Task<Result<TenantCutoverOrchestrationReport>> StartAsync(
    TenantCutoverStartRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    // ---- PREFLIGHT, BEFORE ANYTHING DURABLE EXISTS. A refusal here costs the tenant nothing: no
    // operation row, no freeze, no downtime.
    var preflight = await PreflightAsync(request.TenantId, request.TargetTenantDatabaseId, cancellationToken);
    if (preflight.IsFailure)
    {
      return Result.Failure<TenantCutoverOrchestrationReport>(preflight.Error);
    }

    var begun = await operations.BeginAsync(
      new TenantCutoverBeginRequest(
        request.TenantId, preflight.Value.SourceTenantDatabaseId, request.TargetTenantDatabaseId,
        request.Actor),
      cancellationToken);

    // A losing racer gets CutoverAlreadyActive from the filtered unique index — the database is the final
    // authority on "one cutover per tenant", not this check.
    if (begun.IsFailure)
    {
      return Result.Failure<TenantCutoverOrchestrationReport>(begun.Error);
    }

    return await RunAsync(begun.Value, cancellationToken);
  }

  public Task<Result<TenantCutoverOrchestrationReport>> ResumeAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default) =>
    RunAsync(cutoverOperationId, cancellationToken);

  // The whole sequence, driven by the operation's DURABLE STATUS rather than by anything this process
  // remembers. That is what makes Start and Resume the same code: a resumed run simply finds the operation
  // further along and skips what is already true.
  private async Task<Result<TenantCutoverOrchestrationReport>> RunAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken)
  {
    var options = optionsAccessor.Value;

    var operation = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure<TenantCutoverOrchestrationReport>(TenantStorageErrors.CutoverOperationNotFound);
    }

    if (operation.Status == TenantCutoverOperationStatus.Completed)
    {
      return Completed(operation, TenantCutoverOrchestrationOutcome.AlreadyCompleted, null, null, null);
    }

    // ABANDONED IS TERMINAL. A released or failed cutover is not resumed automatically — the operator who
    // released it decided the tenant should carry on where it is, and quietly restarting would undo that.
    if (operation.Status == TenantCutoverOperationStatus.Abandoned)
    {
      return Result.Failure<TenantCutoverOrchestrationReport>(TenantStorageErrors.CutoverOperationNotFrozen);
    }

    // ---- ONE LEASE, HELD TO THE END. Its connection is this run's; if the process dies the session dies
    // with it and the operation is free for the next Resume, with no lease to expire and no owner to clean.
    await using var ownershipConnection = new SqlConnection(platform.Database.GetConnectionString());
    await ownershipConnection.OpenAsync(cancellationToken);

    var ownership = await TenantCutoverOperationLock.AcquireForSessionAsync(
      ownershipConnection, cutoverOperationId, options.OwnershipTimeout, cancellationToken);
    if (ownership is null)
    {
      // BOUNDED, NOT INDEFINITE: another orchestration owns this cutover and the caller is told so rather
      // than left hanging behind a run that may take an hour.
      return Result.Failure<TenantCutoverOrchestrationReport>(
        TenantStorageErrors.CutoverCopyOwnershipNotAcquired);
    }

    // Re-read under ownership: the status may have moved between the read above and acquiring the lease.
    operation = await operations.FindAsync(cutoverOperationId, cancellationToken);
    if (operation is null)
    {
      return Result.Failure<TenantCutoverOrchestrationReport>(TenantStorageErrors.CutoverOperationNotFound);
    }

    return operation.Status switch
    {
      TenantCutoverOperationStatus.Completed =>
        Completed(operation, TenantCutoverOrchestrationOutcome.AlreadyCompleted, null, null, null),

      // Already flipped by a run that died before finalising: finalise only. Never copy, never flip again.
      TenantCutoverOperationStatus.RoutingFlipped =>
        await FinalizeAsync(operation, null, null, cancellationToken),

      TenantCutoverOperationStatus.Preparing =>
        await FromPreparingAsync(operation, ownership, cancellationToken),

      TenantCutoverOperationStatus.Frozen =>
        await FromFrozenAsync(operation, ownership, cancellationToken),

      _ => Result.Failure<TenantCutoverOrchestrationReport>(TenantStorageErrors.CutoverOperationNotFrozen)
    };
  }

  private async Task<Result<TenantCutoverOrchestrationReport>> FromPreparingAsync(
    TenantCutoverOperationRecord operation,
    TenantCutoverOwnership ownership,
    CancellationToken cancellationToken)
  {
    // ---- FREEZE AND DRAIN [E1]. Idempotent: a resumed run that finds the freeze already durable does not
    // drain again. The freeze service takes the TENANT's drain lock, not the operation lock, so calling it
    // while holding ownership cannot deadlock.
    var frozen = await freezeService.FreezeAsync(operation.CutoverOperationId, cancellationToken);
    if (frozen.IsFailure)
    {
      // E1 owns what a failed freeze means, including its terminal timeout semantics. Nothing is
      // reinterpreted here.
      return Result.Failure<TenantCutoverOrchestrationReport>(frozen.Error);
    }

    var refreshed = await operations.FindAsync(operation.CutoverOperationId, cancellationToken);
    return refreshed is null
      ? Result.Failure<TenantCutoverOrchestrationReport>(TenantStorageErrors.CutoverOperationNotFound)
      : await FromFrozenAsync(refreshed, ownership, cancellationToken);
  }

  private async Task<Result<TenantCutoverOrchestrationReport>> FromFrozenAsync(
    TenantCutoverOperationRecord operation,
    TenantCutoverOwnership ownership,
    CancellationToken cancellationToken)
  {
    // ---- COPY AND EXACT VALIDATION [E3]. Restart-safe: tables already committed are re-validated exactly
    // and skipped, and anything partial or divergent refuses rather than being finished.
    var copied = await copyService.CopyUnderOwnershipAsync(ownership, cancellationToken);
    if (copied.IsFailure)
    {
      // STAYS FROZEN. Releasing here would resume source writes against a target that is already partly
      // copied, turning a retryable failure into an inconsistent one.
      return Resumable(operation, TenantCutoverPhase.Frozen, copied.Error);
    }

    // ---- THE RECOVERY ACTIVATION GATE, RECHECKED WHILE FROZEN. Preflight was to avoid needless downtime;
    // THIS is the authoritative check, because recoverability can degrade during the copy window and moving
    // live traffic onto a database that can no longer be restored is what the gate exists to prevent.
    var activation = await activationGate.AuthorizeActivationAsync(
      operation.TargetTenantDatabaseId, cancellationToken);
    if (activation.IsFailure)
    {
      // Frozen, unflipped, and explicitly NOT unfrozen: this is a retry-or-intervene outcome.
      return Resumable(operation, TenantCutoverPhase.Frozen, activation.Error);
    }

    // ---- THE ATOMIC FLIP [E4]. It revalidates the target exactly one more time inside its own ownership-
    // free path, revalidates every precondition inside its transaction, and moves assignment, RoutingVersion
    // and operation status together.
    var flipped = await flipService.FlipUnderOwnershipAsync(ownership, cancellationToken);
    if (flipped.IsFailure)
    {
      return Resumable(operation, TenantCutoverPhase.Frozen, flipped.Error);
    }

    return await FinalizeAsync(
      operation, flipped.Value.RoutingVersion, copied.Value.TotalRows,
      cancellationToken, flipped.Value.InvalidationError);
  }

  // ---- POST-FLIP. Routing is authoritative from here; every path below either completes or leaves the
  // operation RoutingFlipped for a later Resume. None of them can flip back.
  private async Task<Result<TenantCutoverOrchestrationReport>> FinalizeAsync(
    TenantCutoverOperationRecord operation,
    long? routingVersion,
    long? copiedRows,
    CancellationToken cancellationToken,
    Error? advisory = null)
  {
    // ---- CONVERGENCE, THROUGH THE REAL VERSION-AWARE RESOLVER [E2]. Not a cache inspection and not a
    // fleet-wide acknowledgement: a fresh authoritative resolution proving the tenant now routes where the
    // flip put it. If a local invalidation failed, this is what says correctness is nonetheless satisfied.
    var route = await resolver.ResolveAsync(operation.TenantId, cancellationToken);
    if (route.IsFailure)
    {
      return FinalizationPending(operation, routingVersion, copiedRows, route.Error);
    }

    if (route.Value.TenantDatabaseId != operation.TargetTenantDatabaseId ||
      route.Value.StorageMode != TenantDatabaseStorageMode.Dedicated ||
      (routingVersion is { } expected && route.Value.RoutingVersion != expected))
    {
      return FinalizationPending(
        operation, routingVersion, copiedRows, TenantStorageErrors.CutoverConcurrencyConflict);
    }

    var completed = await operations.CompleteAsync(operation.CutoverOperationId, Actor, cancellationToken);
    if (completed.IsFailure)
    {
      return FinalizationPending(operation, routingVersion, copiedRows, completed.Error);
    }

    return Completed(
      operation, TenantCutoverOrchestrationOutcome.Completed,
      routingVersion ?? route.Value.RoutingVersion, copiedRows, advisory);
  }

  // Everything checkable without costing the tenant a write. Deliberately duplicated in spirit by the
  // checks the individual services make later — this exists to fail EARLY, not to be trusted.
  private async Task<Result<PreflightFacts>> PreflightAsync(
    Guid tenantId,
    long targetTenantDatabaseId,
    CancellationToken cancellationToken)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.TenantRequired);
    }

    // The tenant must route somewhere today, and that somewhere is the cutover's source.
    var active = await platform.TenantDatabaseAssignments
      .AsNoTracking()
      .Where(assignment => assignment.TenantId == tenantId && assignment.EndedUtc == null)
      .Select(assignment => (long?)assignment.TenantDatabaseId)
      .SingleOrDefaultAsync(cancellationToken);

    if (active is not { } sourceTenantDatabaseId)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.ActiveAssignmentMissing);
    }

    if (sourceTenantDatabaseId == targetTenantDatabaseId)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.CutoverTargetNotEligible);
    }

    var endpoints = await platform.TenantDatabases
      .AsNoTracking()
      .Where(database => database.Id == sourceTenantDatabaseId || database.Id == targetTenantDatabaseId)
      .Select(database => new
      {
        database.Id, database.HostingMode, database.StorageMode, database.SchemaCompatibilityStatus
      })
      .ToListAsync(cancellationToken);

    var source = endpoints.SingleOrDefault(database => database.Id == sourceTenantDatabaseId);
    var target = endpoints.SingleOrDefault(database => database.Id == targetTenantDatabaseId);

    if (source is null ||
      source.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      source.StorageMode != TenantDatabaseStorageMode.Shared)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.CutoverSourceNotEligible);
    }

    // V1 promotes onto platform-managed dedicated storage and nothing else (ADR-021).
    if (target is null ||
      target.HostingMode != TenantDatabaseHostingMode.PlatformManaged ||
      target.StorageMode != TenantDatabaseStorageMode.Dedicated)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.CutoverTargetNotEligible);
    }

    if (target.SchemaCompatibilityStatus != TenantDatabaseSchemaCompatibilityStatus.UpToDate)
    {
      return Result.Failure<PreflightFacts>(TenantStorageErrors.CutoverSchemaIncompatible);
    }

    // ---- THE RECOVERY ACTIVATION GATE, CHECKED BEFORE THE TENANT LOSES WRITES. Discovering after a freeze
    // that the target is not recoverable would have cost the tenant downtime for a cutover that could never
    // have completed. It is checked again while frozen; this one only saves the outage.
    var activation = await activationGate.AuthorizeActivationAsync(
      targetTenantDatabaseId, cancellationToken);
    if (activation.IsFailure)
    {
      return Result.Failure<PreflightFacts>(activation.Error);
    }

    return Result.Success(new PreflightFacts(sourceTenantDatabaseId));
  }

  private static Result<TenantCutoverOrchestrationReport> Completed(
    TenantCutoverOperationRecord operation,
    TenantCutoverOrchestrationOutcome outcome,
    long? routingVersion,
    long? copiedRows,
    Error? advisory) =>
    Result.Success(new TenantCutoverOrchestrationReport(
      operation.CutoverOperationId, operation.TenantId,
      operation.SourceTenantDatabaseId, operation.TargetTenantDatabaseId,
      outcome, TenantCutoverPhase.Completed, routingVersion, copiedRows, advisory));

  private static Result<TenantCutoverOrchestrationReport> Resumable(
    TenantCutoverOperationRecord operation,
    TenantCutoverPhase phase,
    Error reason) =>
    Result.Success(new TenantCutoverOrchestrationReport(
      operation.CutoverOperationId, operation.TenantId,
      operation.SourceTenantDatabaseId, operation.TargetTenantDatabaseId,
      TenantCutoverOrchestrationOutcome.Resumable, phase, null, null, reason));

  private static Result<TenantCutoverOrchestrationReport> FinalizationPending(
    TenantCutoverOperationRecord operation,
    long? routingVersion,
    long? copiedRows,
    Error reason) =>
    Result.Success(new TenantCutoverOrchestrationReport(
      operation.CutoverOperationId, operation.TenantId,
      operation.SourceTenantDatabaseId, operation.TargetTenantDatabaseId,
      TenantCutoverOrchestrationOutcome.FinalizationPending, TenantCutoverPhase.RoutingFlipped,
      routingVersion, copiedRows, reason));

  private sealed record PreflightFacts(long SourceTenantDatabaseId);
}
