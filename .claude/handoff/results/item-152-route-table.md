# item 152 (with 153â€“155 corrections) â€” every documented route against the running route surface

**Measurement only. Nothing built, nothing edited.**

200 route strings extracted from the 15 `docs/17-features/*/api-contracts.md`, checked against 155 routes
mapped in `src/`. The per-package table is at the end; this header carries the scope, because **the scope
is the part most likely to be lost when the numbers are reused.**

## The answer

| | rows |
|---|---|
| **BUILT** â€” a mapped route matches, measured | **122** |
| **carries the document's own marker**, printed verbatim | **74** |
| **DENIED** â€” documented only to state its own absence | **2** |
| **UNMATCHED** | **2** |

The two UNMATCHED are `POST /records/bulk` and `POST /grants` â€” **relative fragments whose absolute forms
appear elsewhere in the same documents and already carry markers.** They are duplicates written in a second
form, not unexplained rows. **So every one of the 200 is accounted for.**

## âš  What this population EXCLUDES

- **Capabilities documented in prose rather than as `METHOD /path`.** That is most of them. This measures
  ROUTE STRINGS, not capability.
- **Routes documented by an INVARIANT rather than a row.** FP-008 documents five salary-grade routes with
  one sentence â€” *"`/api/hr/salary-grades` mirrors this exactly under `HR.SalaryGrades.*`"* â€” and this
  table cannot see them. See `item-155-undocumented-routes.md`.
- **Anything outside `api-contracts.md`** â€” ADRs, authorization models, traceability matrices.
- **Routes served by a shared handler, a seeded migration, or a login response** rather than their own
  endpoint. The document's `SERVED BY` markers name the ones already known.

## âš  Two defects were removed from the instrument that produced the first version

1. **The noun-match fallback is gone.** It emitted `[BUILT as <first route sharing the last noun>]`
   whenever exact match FAILED â€” so it fired on 2 routes that were built, 4 that were not, and 1 that does
   not exist, pairing tenant `activate` with leave-type `activate`. **A fallback's output describes the
   MATCHER, not the subject.** There is no fallback now: matched, or `UNMATCHED`.
2. **`/me/` is no longer treated as an absolute template.** A rule added to stop `/api/...` being
   double-prefixed swept in `/me/`, which is NOT absolute â€” all three `/me/` routes are
   `group.MapGet("/me/â€¦")` under a prefix. `me/records` and `me/leave-requests` read as unbuilt in every
   earlier table and are **built**. *A fix scoped by a prefix test inherits every prefix that merely looks
   like the one it was written for.*

**Handler names are not inferred.** An earlier version guessed handlers by noun-matching class names and
invented `AssignDepartmentManagerCommandHandler` for a route nobody wrote. Where a marker names a handler,
that is the DOCUMENT's own text, reproduced.

## Four classes that must not be flattened into "not routed"

- **DENIED (2)** â€” documented only to deny. `DELETE /api/hr/departments/{id}` (*"There is no DELETEâ€¦
  Departments are not deleted"*, `BRULE-DEP-0016`) and `POST /api/gl/journals` (*"does not existâ€¦ The
  phantom row is removed"* â€” a journal is posted by posting a draft). **Marking these `[NOT ROUTED]` would
  publish, as authored text, that a route is pending when its document says it will never exist.**
- **RETAINED INTENTION (1)** â€” `POST /api/attendance/records/bulk`, already marked
  `[SPECIFIED, NEVER BUILT â€” see below]` and kept deliberately, *"because deleting it would retire a
  recorded intention silently."* A phantom says *never*; this says *not yet, and we are keeping the record.*
- **PARTIAL DENIAL (1)** â€” `GET /api/hr/departments/{id}/hierarchy` carries `[BUILT as â€¦/children]`, but
  the document also says the acceptance criteria *"describe a route that does not exist"*: the path was
  replaced AND the ancestors-and-descendants semantics were refused (`DEC-DEP-0024`). **Carry the marker
  without the sentence and you publish "built" for a capability that was declined.**
- **DENIALS THAT PRODUCED NO ROW (3)** â€” FP-012 *"there is no update route, because `BR-PAY-0002` makes a
  change a new record"*; FP-014 *"There is no `PUT` and no `DELETE`â€¦ the append-only ruling showing
  through"*; FP-011 *"no route exists"* on journal mutation. None writes `METHOD /path` adjacently, so none
  is in the 200. âš  **Adding rows for them "for completeness" would manufacture three phantoms.**

## âš  This table did not need to be built

`.claude/handoff/results/T-201.md` (item 48) already decomposed the original 67 rows with members, method
and two self-corrections â€” including the correction from "9 documentation errors" to **10**, and the
explicit withdrawal of the recommendation to edit them: *"The documents are not wrong. The instrument
was."* An earlier item declared that measurement unrecoverable after searching traceability matrices,
route strings and contract bullets â€” **but not `.claude/handoff/results/`.**

**Grep the results trail before measuring anything.**

  UNMATCHED                            2

### FP-001-identity-access — 26 rows

| method | path | status |
|---|---|---|
| `DELETE` | `/api/platform/roles/{}/permissions/{}` | NOT ROUTED - handler: RemovePermissionFromRoleCommandHandler |
| `DELETE` | `/api/platform/users/{}/roles/{}` | NOT ROUTED - handler: RemoveRoleFromTenantUserCommandHandler |
| `GET` | `/api/platform/me/tenant-memberships` | SERVED BY THE LOGIN RESPONSE - TenantMembershipResponse |
| `GET` | `/api/platform/permissions` | BUILT 2026-08-29, T-203 |
| `GET` | `/api/platform/roles` | BUILT |
| `GET` | `/api/platform/roles/{}` | NOT ROUTED - handler: GetRoleByIdQueryHandler |
| `GET` | `/api/platform/support/tenants/{}/users` | NOT ROUTED - handler: ListTenantUsersQueryHandler; no support-scoped route exists |
| `GET` | `/api/platform/users` | NOT ROUTED - handler: ListTenantUsersQueryHandler |
| `GET` | `/api/platform/users/{}` | NOT ROUTED - handler: GetTenantUserByIdQueryHandler |
| `GET` | `/me/tenant-memberships` | SERVED BY ... |
| `POST` | `/api/platform/auth/select-tenant` | BUILT as ... |
| `POST` | `/api/platform/me/select-tenant` | BUILT as POST /api/platform/auth/select-tenant |
| `POST` | `/api/platform/roles` | NOT ROUTED - handler: CreateCustomRoleCommandHandler |
| `POST` | `/api/platform/roles/{}/permissions` | NOT ROUTED - handler: AssignPermissionToRoleCommandHandler |
| `POST` | `/api/platform/roles/{}/request-retirement` | NOT ROUTED - handler: RequestRoleRetirementCommandHandler |
| `POST` | `/api/platform/roles/{}/retire` | NOT ROUTED - handler: RetireRoleCommandHandler |
| `POST` | `/api/platform/support/tenants/{}/users/invitations` | NOT ROUTED - handler: IssueTenantUserInvitationCommandHandler; no support-scoped route |
| `POST` | `/api/platform/support/tenants/{}/users/{}/deactivate` | NOT ROUTED - handler: DeactivateTenantUserCommandHandler; no support-scoped route |
| `POST` | `/api/platform/tenant-users/{}/deactivation` | BUILT as POST /api/platform/tenant-users/{tenantUserId}/deactivation |
| `POST` | `/api/platform/tenant-users/{}/reactivation` | BUILT as POST /api/platform/tenant-users/{tenantUserId}/reactivation |
| `POST` | `/api/platform/users/invitations` | NOT ROUTED - handler: IssueTenantUserInvitationCommandHandler |
| `POST` | `/api/platform/users/{}/deactivate` | BUILT as POST /api/platform/tenant-users/{tenantUserId}/deactivation |
| `POST` | `/api/platform/users/{}/reactivate` | BUILT as POST /api/platform/tenant-users/{tenantUserId}/reactivation |
| `POST` | `/api/platform/users/{}/roles` | NOT ROUTED - handler: AssignRoleToTenantUserCommandHandler |
| `PUT` | `/api/platform/roles/{}` | NOT ROUTED - handler: UpdateCustomRoleCommandHandler |
| `PUT` | `/api/platform/users/{}` | NOT ROUTED - handler: UpdateTenantUserProfileCommandHandler |

### FP-002-authentication-token-lifecycle — 4 rows

| method | path | status |
|---|---|---|
| `POST` | `/api/platform/auth/login` | BUILT |
| `POST` | `/api/platform/auth/logout` | BUILT |
| `POST` | `/api/platform/auth/refresh` | BUILT |
| `POST` | `/api/platform/auth/select-tenant` | BUILT as ... |

### FP-003-tenant-lifecycle — 8 rows

| method | path | status |
|---|---|---|
| `DELETE` | `/api/platform/tenants/{}` | SUPERSEDED - no delete exists, by DEC-TEN-0007 |
| `GET` | `/api/platform/tenants` | DEFERRED - AC-TEN-0020 |
| `GET` | `/api/platform/tenants/{}` | DEFERRED - AC-TEN-0020 |
| `POST` | `/api/platform/tenants` | DEFERRED - AC-TEN-0020 |
| `POST` | `/api/platform/tenants/{}/activate` | DEFERRED - AC-TEN-0020 |
| `POST` | `/api/platform/tenants/{}/archive` | DEFERRED - AC-TEN-0020 |
| `POST` | `/api/platform/tenants/{}/reactivate` | DEFERRED - AC-TEN-0020 |
| `POST` | `/api/platform/tenants/{}/suspend` | DEFERRED - AC-TEN-0020 |

### FP-004-localization — 9 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/platform/localization/effective` | BUILT |
| `GET` | `/api/platform/localization/resources` | BUILT |
| `GET` | `/api/platform/localization/resources/{}` | BUILT |
| `GET` | `/api/platform/localization/resources/{}/history` | BUILT |
| `POST` | `/api/platform/localization/effective/batch` | BUILT |
| `POST` | `/api/platform/localization/preview` | BUILT |
| `POST` | `/api/platform/localization/resources/{}/overrides/{}/restore-default` | BUILT |
| `POST` | `/api/platform/localization/resources/{}/overrides/{}/undo` | BUILT |
| `PUT` | `/api/platform/localization/resources/{}/overrides/{}` | BUILT |

### FP-005-company-legal-entity — 7 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/platform/companies` | BUILT |
| `GET` | `/api/platform/companies/{}` | BUILT |
| `POST` | `/api/platform/companies` | BUILT |
| `POST` | `/api/platform/companies/{}/activate` | BUILT |
| `POST` | `/api/platform/companies/{}/archive` | BUILT |
| `POST` | `/api/platform/companies/{}/deactivate` | BUILT |
| `PUT` | `/api/platform/companies/{}` | BUILT |

### FP-006-hr-employee — 9 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/hr/employees` | BUILT |
| `GET` | `/api/hr/employees/{}` | BUILT |
| `GET` | `/api/hr/employees/{}/branch-history` | BUILT |
| `POST` | `/api/hr/employees` | BUILT |
| `POST` | `/api/hr/employees/{}/activate` | BUILT |
| `POST` | `/api/hr/employees/{}/deactivate` | BUILT |
| `POST` | `/api/hr/employees/{}/terminate` | BUILT |
| `POST` | `/api/hr/employees/{}/transfer` | BUILT |
| `PUT` | `/api/hr/employees/{}` | BUILT |

### FP-007-hr-department — 27 rows

| method | path | status |
|---|---|---|
| `DELETE` | `/api/hr/departments/{}` | DENIED |
| `DELETE` | `/api/hr/departments/{}/manager` | BUILT as POST /api/hr/departments/{departmentId}/manager/remove |
| `GET` | `/api/hr/departments` | BUILT |
| `GET` | `/api/hr/departments/{}` | BUILT |
| `GET` | `/api/hr/departments/{}/children` | BUILT as GET /api/hr/departments/{departmentId}/children |
| `GET` | `/api/hr/departments/{}/hierarchy` | BUILT as GET /api/hr/departments/{departmentId}/children |
| `GET` | `/api/hr/employees` | BUILT |
| `GET` | `/api/hr/employees/{}` | BUILT |
| `GET` | `/api/hr/employees/{}/branch-history` | BUILT |
| `GET` | `/{}/children` | BUILT as GET /api/hr/departments/{departmentId}/children |
| `POST` | `/api/hr/departments` | BUILT |
| `POST` | `/api/hr/departments/{}/activate` | BUILT as POST /api/hr/departments/{departmentId}/activate |
| `POST` | `/api/hr/departments/{}/deactivate` | BUILT |
| `POST` | `/api/hr/departments/{}/manager` | BUILT as POST /api/hr/departments/{departmentId}/manager |
| `POST` | `/api/hr/departments/{}/manager/remove` | BUILT as POST /api/hr/departments/{departmentId}/manager/remove |
| `POST` | `/api/hr/departments/{}/move` | BUILT as POST /api/hr/departments/{departmentId}/move + /move-to-root |
| `POST` | `/api/hr/departments/{}/parent` | BUILT as POST /api/hr/departments/{departmentId}/move + /move-to-root |
| `POST` | `/api/hr/departments/{}/reactivate` | BUILT as POST /api/hr/departments/{departmentId}/activate |
| `POST` | `/api/hr/employees` | BUILT |
| `POST` | `/api/hr/employees/{}/change-department` | BUILT as POST /api/hr/employees/{id}/change-department |
| `POST` | `/api/hr/employees/{}/department` | BUILT as POST /api/hr/employees/{id}/change-department |
| `POST` | `/{}/activate` | BUILT as POST /api/hr/departments/{departmentId}/activate |
| `POST` | `/{}/manager` | BUILT as POST /api/hr/departments/{departmentId}/manager |
| `POST` | `/{}/manager/remove` | BUILT as POST /api/hr/departments/{departmentId}/manager/remove |
| `PUT` | `/api/hr/departments/{}` | BUILT |
| `PUT` | `/api/hr/departments/{}/manager` | BUILT as POST /api/hr/departments/{departmentId}/manager |
| `PUT` | `/api/hr/employees/{}` | BUILT |

### FP-008-hr-position — 20 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/hr/employees` | BUILT |
| `GET` | `/api/hr/employees/{}` | BUILT |
| `GET` | `/api/hr/employees/{}/branch-history` | BUILT |
| `GET` | `/api/hr/employees/{}/position-history` | BUILT |
| `GET` | `/api/hr/job-grades` | BUILT |
| `GET` | `/api/hr/job-grades/{}` | BUILT |
| `GET` | `/api/hr/positions` | BUILT |
| `GET` | `/api/hr/positions/{}` | BUILT |
| `POST` | `/api/hr/employees` | BUILT |
| `POST` | `/api/hr/employees/{}/change-department` | BUILT as POST /api/hr/employees/{id}/change-department |
| `POST` | `/api/hr/employees/{}/change-position` | BUILT |
| `POST` | `/api/hr/job-grades` | BUILT |
| `POST` | `/api/hr/job-grades/{}/activate` | BUILT |
| `POST` | `/api/hr/job-grades/{}/deactivate` | BUILT |
| `POST` | `/api/hr/positions` | BUILT |
| `POST` | `/api/hr/positions/{}/activate` | BUILT |
| `POST` | `/api/hr/positions/{}/deactivate` | BUILT |
| `PUT` | `/api/hr/employees/{}` | BUILT |
| `PUT` | `/api/hr/job-grades/{}` | BUILT |
| `PUT` | `/api/hr/positions/{}` | BUILT |

### FP-009-hr-employee-import-export — 5 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/hr/employees/export` | BUILT |
| `GET` | `/api/hr/employees/export-runs` | BUILT |
| `GET` | `/api/hr/employees/import-runs` | BUILT |
| `POST` | `/api/hr/employees/import` | BUILT |
| `POST` | `/api/hr/employees/import/validate` | BUILT |

### FP-011-gl-foundation — 25 rows

| method | path | status |
|---|---|---|
| `GET` | `/accounts/{}` | BUILT |
| `GET` | `/api/gl/accounts` | BUILT |
| `GET` | `/api/gl/accounts/{}` | BUILT |
| `GET` | `/api/gl/accounts/{}/balance` | BUILT |
| `GET` | `/api/gl/fiscal-periods` | BUILT |
| `GET` | `/api/gl/journal-drafts` | BUILT |
| `GET` | `/api/gl/journal-drafts/{}` | BUILT |
| `GET` | `/api/gl/journals` | BUILT |
| `GET` | `/api/gl/journals/{}` | BUILT |
| `GET` | `/api/gl/reports/trial-balance` | BUILT |
| `POST` | `/accounts/{}/activation` | BUILT |
| `POST` | `/api/gl/accounts` | BUILT |
| `POST` | `/api/gl/accounts/{}/activation` | BUILT |
| `POST` | `/api/gl/accounts/{}/deactivation` | BUILT |
| `POST` | `/api/gl/fiscal-periods/{}/closure` | BUILT |
| `POST` | `/api/gl/fiscal-periods/{}/reopening` | BUILT |
| `POST` | `/api/gl/fiscal-years` | BUILT |
| `POST` | `/api/gl/journal-drafts` | BUILT |
| `POST` | `/api/gl/journal-drafts/{}/discard` | BUILT |
| `POST` | `/api/gl/journal-drafts/{}/posting` | BUILT |
| `POST` | `/api/gl/journals` | DENIED |
| `POST` | `/api/gl/journals/{}/reversals` | BUILT |
| `POST` | `/fiscal-periods/{}/reopening` | BUILT |
| `PUT` | `/api/gl/accounts/{}` | BUILT |
| `PUT` | `/api/gl/journal-drafts/{}` | BUILT |

### FP-012-payroll — 17 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/payroll/elements` | BUILT |
| `GET` | `/api/payroll/elements/{}` | BUILT |
| `GET` | `/api/payroll/employees/{}/compensation` | BUILT |
| `GET` | `/api/payroll/employees/{}/compensation/current` | BUILT |
| `GET` | `/api/payroll/employees/{}/payslips` | BUILT |
| `GET` | `/api/payroll/runs` | BUILT |
| `GET` | `/api/payroll/runs/{}` | BUILT |
| `GET` | `/api/payroll/runs/{}/payslips/{}` | BUILT |
| `POST` | `/api/payroll/elements` | BUILT |
| `POST` | `/api/payroll/elements/{}/activation` | BUILT |
| `POST` | `/api/payroll/elements/{}/deactivation` | BUILT |
| `POST` | `/api/payroll/employees/{}/compensation` | BUILT |
| `POST` | `/api/payroll/runs` | BUILT |
| `POST` | `/api/payroll/runs/{}/approval` | BUILT |
| `POST` | `/api/payroll/runs/{}/calculation` | BUILT |
| `POST` | `/api/payroll/runs/{}/posting` | BUILT |
| `PUT` | `/api/payroll/elements/{}` | BUILT |

### FP-013-attendance — 31 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/attendance/calendars` | BUILT |
| `GET` | `/api/attendance/calendars/working-days` | BUILT |
| `GET` | `/api/attendance/leave-balances` | BUILT |
| `GET` | `/api/attendance/leave-requests` | BUILT |
| `GET` | `/api/attendance/leave-types` | BUILT |
| `GET` | `/api/attendance/me/leave-requests` | BUILT |
| `GET` | `/api/attendance/me/records` | BUILT |
| `GET` | `/api/attendance/periods` | BUILT |
| `GET` | `/api/attendance/records` | BUILT |
| `POST` | `/api/attendance/calendars` | BUILT |
| `POST` | `/api/attendance/calendars/{}/holidays` | BUILT |
| `POST` | `/api/attendance/calendars/{}/holidays/remove` | BUILT |
| `POST` | `/api/attendance/leave-requests` | BUILT |
| `POST` | `/api/attendance/leave-requests/{}/approve` | BUILT |
| `POST` | `/api/attendance/leave-requests/{}/cancel` | BUILT |
| `POST` | `/api/attendance/leave-requests/{}/reject` | BUILT |
| `POST` | `/api/attendance/leave-types` | BUILT |
| `POST` | `/api/attendance/leave-types/{}/activate` | BUILT |
| `POST` | `/api/attendance/leave-types/{}/deactivate` | BUILT |
| `POST` | `/api/attendance/periods` | BUILT |
| `POST` | `/api/attendance/periods/{}/close` | BUILT |
| `POST` | `/api/attendance/periods/{}/reopen` | BUILT |
| `POST` | `/api/attendance/records` | BUILT |
| `POST` | `/api/attendance/records/bulk` | SPECIFIED, NEVER BUILT — see below |
| `POST` | `/api/attendance/records/{}/adjustments` | BUILT |
| `POST` | `/api/payroll/elements` | BUILT |
| `POST` | `/leave-requests` | BUILT |
| `POST` | `/records/bulk` | UNMATCHED |
| `PUT` | `/api/attendance/calendars/{}` | BUILT |
| `PUT` | `/api/attendance/leave-balances` | BUILT |
| `PUT` | `/api/attendance/leave-types/{}` | BUILT |

### FP-014-subscription — 26 rows

| method | path | status |
|---|---|---|
| `GET` | `/api/platform/invoices` | NOT BUILT - nothing exists |
| `GET` | `/api/platform/invoices/{}` | NOT BUILT - nothing exists |
| `GET` | `/api/platform/invoices/{}/attempts` | NOT BUILT - nothing exists |
| `GET` | `/api/platform/modules/enabled` | NOT BUILT - capability exists, wrong shape |
| `GET` | `/api/platform/plans` | NOT BUILT - domain only |
| `GET` | `/api/platform/plans/{}` | NOT BUILT - domain only |
| `GET` | `/api/platform/subscriptions` | NOT BUILT - domain + write-only repo |
| `GET` | `/api/platform/tenants/{}/grants` | NOT BUILT - domain + internal read |
| `GET` | `/api/platform/tenants/{}/invoices` | NOT BUILT - nothing exists |
| `GET` | `/api/platform/tenants/{}/subscriptions` | NOT BUILT - domain + write-only repo |
| `GET` | `/api/platform/tenants/{}/subscriptions/current` | NOT BUILT - domain + write-only repo |
| `POST` | `/api/payroll/elements` | BUILT |
| `POST` | `/api/platform/invoices` | NOT BUILT - nothing exists |
| `POST` | `/api/platform/invoices/{}/issue` | NOT BUILT - nothing exists |
| `POST` | `/api/platform/invoices/{}/void` | NOT BUILT - nothing exists |
| `POST` | `/api/platform/plans` | NOT BUILT - domain only |
| `POST` | `/api/platform/plans/{}/retire` | NOT BUILT - domain only |
| `POST` | `/api/platform/tenants/{}/grants` | NOT BUILT - domain + internal read |
| `POST` | `/api/platform/tenants/{}/grants/revoke` | NOT BUILT - domain + internal read |
| `POST` | `/api/platform/tenants/{}/subscriptions` | NOT BUILT - domain + write-only repo |
| `POST` | `/grants` | UNMATCHED |
| `PUT` | `/api/platform/invoices/{}` | NOT BUILT - nothing exists |
| `PUT` | `/api/platform/plans/{}` | NOT BUILT - domain only |
| `PUT` | `/api/platform/plans/{}/limits` | NOT BUILT - domain only |
| `PUT` | `/api/platform/plans/{}/modules` | NOT BUILT - domain only |
| `PUT` | `/api/platform/plans/{}/prices` | NOT BUILT - domain only |

