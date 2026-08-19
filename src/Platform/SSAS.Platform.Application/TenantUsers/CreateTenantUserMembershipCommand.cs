namespace SSAS.Platform.Application.TenantUsers;

// ROLES AND BRANCHES ARE PART OF CREATION, NOT FOLLOW-UP STEPS (Branch foundation B1b).
//
// RoleIds joins the command because whether the new user is a TENANT ADMINISTRATOR decides whether branches
// are mandatory, and that cannot be known if roles are granted afterwards: a user created first and
// promoted second would have to pass the normal-user branch rule on the way through, which is unsatisfiable
// for the first administrator of a tenant that has no branches yet.
//
// BranchIds is REQUIRED for a normal user and must name at least one active branch of this tenant. Empty is
// legal only when the granted roles confer tenant administration, whose scope is every active branch and
// needs no rows at all.
public sealed record CreateTenantUserMembershipCommand(
  long IdentityId,
  string Email,
  string DisplayName,
  IReadOnlyList<long> RoleIds,
  IReadOnlyList<Guid> BranchIds);
