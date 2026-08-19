namespace SSAS.Platform.Application.Branches;

// MUTUAL EXCLUSION OVER A TENANT'S BRANCH TOPOLOGY, exposed to the Application layer (Branch foundation
// B1b).
//
// ---- WHY THE APPLICATION LAYER NEEDS THIS AT ALL.
//
// B1a established that the invariant "no active normal user is left without an active branch" spans two
// databases and therefore cannot be held by a transaction or a constraint. It is held by serialising every
// mutation that can change branch topology onto one per-tenant resource.
//
// Branch deactivation (B1a) takes that resource in Infrastructure. The OTHER half of the invariant — user
// creation and branch-assignment editing — lives in Application command handlers, which must take the SAME
// resource or the guard protects nothing. This abstraction is what lets them, without the Application layer
// knowing that the resource is a SQL Server application lock.
//
// ---- THE ORDER IS THE WHOLE POINT.
//
// Acquire, THEN read topology, THEN validate, THEN persist, THEN release. Validating before acquiring and
// trusting the result afterwards is precisely the race this closes: the facts a decision rests on must not
// be able to change between the decision and the write.
public interface IBranchTopologyGuard
{
  // Null means another topology operation owns this tenant right now. That is a RETRYABLE refusal, not a
  // failure: nothing was attempted and nothing was lost.
  Task<IBranchTopologyLease?> AcquireAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

// Ownership lives and dies with the lease. Disposing releases it; a dying process drops the underlying
// connection and releases it too, so there is no lease to expire and no stale owner to clean up.
public interface IBranchTopologyLease : IAsyncDisposable
{
  Guid TenantId { get; }
}
