import re

fp001 = 'docs/17-features/FP-001-identity-access/api-contracts.md'
c = open(fp001).read()

replacements = {
    'GET /api/platform/roles/{roleId}   [NOT ROUTED - handler: GetRoleByIdQueryHandler]': 'GET /api/platform/roles/{roleId}   Platform.Roles.View',
    'POST /api/platform/roles   [NOT ROUTED - handler: CreateCustomRoleCommandHandler]': 'POST /api/platform/roles   Platform.Roles.Create',
    'PUT /api/platform/roles/{roleId}   [NOT ROUTED - handler: UpdateCustomRoleCommandHandler]': 'PUT /api/platform/roles/{roleId}   Platform.Roles.Update',
    'POST /api/platform/roles/{roleId}/request-retirement   [NOT ROUTED - handler: RequestRoleRetirementCommandHandler]': 'POST /api/platform/roles/{roleId}/request-retirement   Platform.Roles.RequestRetirement',
    'POST /api/platform/roles/{roleId}/retire   [NOT ROUTED - handler: RetireRoleCommandHandler]': 'POST /api/platform/roles/{roleId}/retire   Platform.Roles.Retire',
    'POST /api/platform/roles/{roleId}/permissions   [NOT ROUTED - handler: AssignPermissionToRoleCommandHandler]': 'POST /api/platform/roles/{roleId}/permissions   Platform.RolePermissions.Assign',
    'DELETE /api/platform/roles/{roleId}/permissions/{permission}   [NOT ROUTED - handler: RemovePermissionFromRoleCommandHandler]': 'POST /api/platform/roles/{roleId}/permissions/{permission}/remove   Platform.RolePermissions.Remove',
    'POST /api/platform/users/{userId}/roles   [NOT ROUTED - handler: AssignRoleToTenantUserCommandHandler]': 'POST /api/platform/users/{userId}/roles   Platform.UserRoles.Assign',
    'DELETE /api/platform/users/{userId}/roles/{roleId}   [NOT ROUTED - handler: RemoveRoleFromTenantUserCommandHandler]': 'POST /api/platform/users/{userId}/roles/{roleId}/remove   Platform.UserRoles.Remove',
}

for k, v in replacements.items():
    c = c.replace(k, v)

open(fp001, 'w').write(c)

fp003 = 'docs/17-features/FP-003-tenant-lifecycle/api-contracts.md'
c3 = open(fp003).read()
c3 = c3.replace('GET /api/platform/support/tenants/{tenantId}/users   [NOT ROUTED - handler: ListTenantUsersQueryHandler; no support-scoped route exists]', 'GET /api/platform/support/tenants/{tenantId}/users   Platform.Support.Administer')
c3 = c3.replace('POST /api/platform/support/tenants/{tenantId}/users/invitations   [NOT ROUTED - handler: IssueTenantUserInvitationCommandHandler; no support-scoped route]', 'POST /api/platform/support/tenants/{tenantId}/users/invitations   Platform.Support.Administer')
c3 = c3.replace('POST /api/platform/support/tenants/{tenantId}/users/{userId}/deactivate   [NOT ROUTED - handler: DeactivateTenantUserCommandHandler; no support-scoped route]', 'POST /api/platform/support/tenants/{tenantId}/users/{userId}/deactivate   Platform.Support.Administer')
open(fp003, 'w').write(c3)

