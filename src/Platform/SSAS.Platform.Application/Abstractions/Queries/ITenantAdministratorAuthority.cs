namespace SSAS.Platform.Application.Abstractions.Queries;

// IS THIS USER A TENANT ADMINISTRATOR? (Branch foundation B0/B1.)
//
// ONE PREDICATE, ONE PLACE. Tenant-administrator authority decides branch scope in several unrelated
// places — login, branch selection, user-creation exemption, branch deactivation safety, and the
// branch-owned write boundary — and the failure mode of duplicating it is that one of them disagrees and
// either strands an administrator or grants a normal user the whole tenant.
//
// IT TAKES THE TENANT AND USER EXPLICITLY rather than reading an ambient context, because the earliest
// caller is authentication itself: the answer is needed while deciding whether a login completes, before
// any request-scoped tenant context exists.
//
// AUTHORITY IS A PERMISSION, NOT A FLAG OR A ROLE NAME. It resolves Platform.Tenant.Administer through the
// user's active roles, so tenant administration is granted and revoked by the same mechanism as every
// other authority in the system, and a renamed role cannot silently change who governs a tenant.
public interface ITenantAdministratorAuthority
{
  Task<bool> IsTenantAdministratorAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default);
}
