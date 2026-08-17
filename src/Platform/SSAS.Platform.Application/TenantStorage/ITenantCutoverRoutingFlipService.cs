using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.TenantStorage;

// THE AUTHORITATIVE Shared → Dedicated ROUTING FLIP (ADR-020, TS-Storage Phase E4).
//
// One call, one Platform transaction, three facts moved together: the tenant's active assignment, its
// RoutingVersion, and the cutover operation's status. After it commits the tenant's database has changed
// for every instance in the estate — the ones that were told, and the ones that were not.
//
// IT IS THE POINT OF NO RETURN. There is deliberately no flip-back call on this interface: ADR-020 forbids
// a simple reversal once the target may have been written to, and an API that offered one would be used
// during exactly the incident where it is least safe.
public interface ITenantCutoverRoutingFlipService
{
  Task<Result<TenantCutoverFlipReport>> FlipAsync(
    long cutoverOperationId,
    CancellationToken cancellationToken = default);
}

// What the flip did, in the terms an operator needs during a cutover.
public sealed record TenantCutoverFlipReport(
  long CutoverOperationId,
  Guid TenantId,
  long SourceTenantDatabaseId,
  long TargetTenantDatabaseId,
  long PreviousRoutingVersion,
  long RoutingVersion,
  TenantCutoverFlipOutcome Outcome,
  // Set when the flip committed but this process could not evict its own cached route. NOT a routing
  // failure: routing is authoritative and every instance converges on its next resolution through the
  // version check. Surfaced so "convergence here is by TTL rather than immediate" is visible rather than
  // inferred from a silence.
  Error? InvalidationError = null)
{
  public bool LocalInvalidationSucceeded => InvalidationError is null;
}

public enum TenantCutoverFlipOutcome
{
  // This call moved routing.
  Flipped = 1,

  // Routing had already been moved by an earlier call for this same operation and version. Reported rather
  // than refused: a retry after a committed flip has got what it asked for, and treating that as a failure
  // is what tempts a caller into flipping a second time.
  AlreadyFlipped = 2
}
