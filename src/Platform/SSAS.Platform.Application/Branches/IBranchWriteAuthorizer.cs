using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Branches;

// THE AUTHORITATIVE BRANCH FOR A BRANCH-OWNED WRITE (Branch foundation B1c).
//
// ONE CALL ANSWERS BOTH QUESTIONS — which branch, and may this user still write to it — because answering
// them separately is how they drift apart. A cached "current branch" read from one place and an
// authorization check made against another is exactly the shape in which a revoked user keeps writing.
//
// EVERY INPUT IS RE-READ FROM AUTHORITATIVE STATE:
//
//   * the ACTIVE BRANCH comes from the durable AuthenticationSession, never from a request header, a form
//     field, or a token claim. A client naming its own branch would make branch scope self-asserted.
//   * the AUTHORIZATION comes from ITenantBranchAccessResolver, which re-reads the assignment rows, the
//     administrator authority, and whether the branch is still active — all of which can change inside a
//     session's lifetime.
//
// IT FAILS CLOSED. No branch selected, session unusable, access revoked, authority revoked, branch
// deactivated: every one of them refuses the write rather than falling back to a previously valid answer.
public interface IBranchWriteAuthorizer
{
  Task<Result<Guid>> AuthorizeCurrentBranchAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
