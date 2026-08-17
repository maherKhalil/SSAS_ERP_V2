namespace SSAS.Platform.Application.TenantUsers;

// REPLACE-SET, NOT ADD/REMOVE (Branch foundation B1b).
//
// The invariant being protected is about the FINAL set — "at least one active branch" — so the operation
// that must satisfy it is the one that states the final set. A pair of independent add and remove calls
// would each have to guess at the other's outcome, and the last remove in a sequence is where a user
// silently ends up with nothing.
public sealed record SetTenantUserBranchesCommand(long TenantUserId, IReadOnlyList<Guid> BranchIds);
