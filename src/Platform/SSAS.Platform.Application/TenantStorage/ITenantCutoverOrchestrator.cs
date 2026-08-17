using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// THE SHARED → DEDICATED CUTOVER, END TO END (ADR-020, TS-Storage Phase E5).
//
// IT COMPOSES; IT DOES NOT REIMPLEMENT. The freeze is E1's, the copy and exact validation are E3's, the
// atomic flip is E4's, and the convergence check is E2's. Nothing here writes an assignment, computes a
// RoutingVersion, moves a row, or reads a cache — the value it adds is ORDER, OWNERSHIP and RESUMABILITY,
// and every correctness decision stays in the component that already owns it and has already been reviewed.
//
// NOT ACTIVATED FROM ANYWHERE. No HTTP surface, no scheduler, no queue consumer: exposing a one-way
// operation on customer data is a separate operational and security decision, and this slice deliberately
// does not take it.
//
// TWO ENTRY POINTS, because a cutover outlives the process that started it. Start begins one; Resume
// continues whatever a lost process left behind — after a crash, a network failure, a caller timeout, or a
// flip that committed and never got to say so. Neither requires a database edit to recover.
public interface ITenantCutoverOrchestrator
{
  Task<Result<TenantCutoverOrchestrationReport>> StartAsync(
    TenantCutoverStartRequest request,
    CancellationToken cancellationToken = default);

  Task<Result<TenantCutoverOrchestrationReport>> ResumeAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default);
}

public sealed record TenantCutoverStartRequest(
  Guid TenantId,
  long TargetTenantDatabaseId,
  string Actor);

// What the orchestration achieved, in the terms an operator needs mid-cutover. A bool would say none of it.
public sealed record TenantCutoverOrchestrationReport(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  TenantCutoverOrchestrationOutcome Outcome,
  // The phase the operation durably reached. On a resumable outcome this is where a later Resume picks up.
  TenantCutoverPhase Phase,
  long? RoutingVersion = null,
  long? CopiedRows = null,
  // Set when routing is authoritative but a local, non-correctness step did not succeed — an invalidation
  // that failed, for instance. Never a reason to consider the flip incomplete.
  Error? Advisory = null)
{
  public bool RoutingIsAuthoritative => Phase is TenantCutoverPhase.RoutingFlipped or TenantCutoverPhase.Completed;
}

public enum TenantCutoverOrchestrationOutcome
{
  // The tenant is now served from its dedicated database and nothing pre-traffic remains.
  Completed = 1,

  // This call found the cutover already finished. Idempotent success — a retry after a lost response.
  AlreadyCompleted = 2,

  // Stopped safely and can be continued by Resume. The tenant is either untouched or still frozen; which
  // one is in Phase. DELIBERATELY NOT a release: unfreezing on failure would let source writes resume and
  // turn a retryable problem into a target that no longer matches (ADR-020).
  Resumable = 3,

  // Routing is authoritative but post-flip finalisation did not finish. Resume finalises; nothing flips
  // back, because after the flip there is nothing safe to flip back to.
  FinalizationPending = 4
}

// The durable phase the operation is in, mirroring its status rather than inventing a parallel vocabulary.
public enum TenantCutoverPhase
{
  // Nothing durable was created: preflight refused before a tenant could be frozen.
  NotStarted = 0,
  Preparing = 1,
  Frozen = 2,
  RoutingFlipped = 3,
  Completed = 4
}
