using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// RE-VALIDATES THE OPEN TRANSFER AT SAVE TIME (FP-006C2, ADR-024 decisions 3 and 6).
//
// OPENING THE SCOPE IS NOT PROOF THAT THE AUTHORIZATION STILL HOLDS. A handler authorizes, declares, and
// then does other work; access can be revoked, a destination deactivated and administrator authority
// withdrawn in between. So the boundary asks again, here, on every save — the same reason
// IBranchWriteAuthorizer re-reads the session and re-asks the resolver rather than trusting a captured
// answer.
//
// ---- WHAT IT RE-VALIDATES, AND WHAT IT DELIBERATELY DOES NOT.
//
// It re-asks the DESTINATION through ITenantBranchAccessResolver, which intersects with active branches, so
// a destination that has been deactivated or whose access was revoked refuses the transfer. It never
// re-implements that decision: duplicating the resolver's logic here is how the read path and the write
// path come to disagree about what a branch means.
//
// For InactiveSourceRecovery it additionally re-asks the two facts that make the exception narrow — that
// the actor still holds Platform.Tenant.Administer, and that the source branch is genuinely INACTIVE. The
// second matters as much as the first: without it, an administrator could use recovery mode to modify an
// entity in an ACTIVE branch without being in that branch's execution context, which is a widening rather
// than a recovery.
//
// The SOURCE for the ordinary mode is not checked here. It is the caller's execution branch, and the write
// boundary already proves that through IBranchWriteAuthorizer and then requires the declared source to
// equal it — one fact, established in one place.
//
// IT FAILS CLOSED. No open declaration is not a failure; it is the answer "no transfer is authorized", and
// every BranchId modification is then refused by the ordinary rules.
public interface IBranchTransferAuthorizer
{
  // Returns the declaration that is authorized right now, or null when none is open. A failure means a
  // declaration IS open and is no longer valid, which must refuse the save rather than fall back to the
  // ordinary rules — falling back would turn a revoked transfer into an ordinary refusal and hide why.
  Task<Result<BranchTransferDeclaration?>> AuthorizeOpenTransferAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);
}
