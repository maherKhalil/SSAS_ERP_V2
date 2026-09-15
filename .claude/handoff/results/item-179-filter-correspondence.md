# item 179 — is any filter WRONG rather than absent?

**Measurement only. Nothing built.** Item 177's guard asks whether a filter *exists*, never whether it is
*right* — the `AC-SUB-0020` shape.

## ⚠ The answer: none is wrong. No live data defect.

All 16 filters were read from the model and checked against their own index's columns and each column's
model nullability. **Every nullable column under a unique index is covered.**

## The population that CAN be wrong is 11, not 16

**`IConventionIndex.GetFilterConfigurationSource()` returns nothing on a finalised model** — the runtime
model drops build-time provenance — so "written or convention" is classified **by shape**, which item 177
established empirically: the provider generates exactly `[Col] IS NOT NULL` over precisely the index's
nullable columns, and nothing else.

**Five are convention-shaped and therefore cannot be wrong** — they are either the provider's own or
textually identical to what it would produce:

| index | filter |
|---|---|
| `Platform.RefreshTokenRecord [ReplacedByRefreshTokenRecordId?]` | `[ReplacedByRefreshTokenRecordId] IS NOT NULL` |
| `Platform.PlatformRefreshTokenRecord [ReplacedByRefreshTokenRecordId?]` | `[ReplacedByRefreshTokenRecordId] IS NOT NULL` |
| `Platform.TenantDatabaseRestoreVerificationRun [VerificationDatabaseName?]` | `[VerificationDatabaseName] IS NOT NULL` |
| `Tenant.JournalEntry [TenantId, ReversesJournalEntryId?]` | `[ReversesJournalEntryId] IS NOT NULL` |
| `Tenant.Employee [TenantId, CompanyId, NormalizedNationalId?]` | `[NormalizedNationalId] IS NOT NULL` |

**The other 11 are necessarily WRITTEN**, because the convention never generates a state predicate, an
`IN` list, an equality, or a conjunction. **So the fallible population is 31% smaller than the filter
count.**

## The 11 written filters, each checked

| index | filter | verdict |
|---|---|---|
| `Platform.AccountActionToken [Purpose, AuthenticationAccountId]` | `[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NULL` | correct — no nullable index column; scopes uniqueness to *live, non-tenant* tokens |
| ⚠ `Platform.AccountActionToken [Purpose, TenantId?, TenantUserId?]` | `… AND [TenantUserId] IS NOT NULL` | **correct, but see below** |
| `Platform.PlatformPermissionAssignment [PlatformSupportPrincipalId, PermissionName]` | `[RemovedUtc] IS NULL` | correct — no nullable index column |
| `Platform.RolePermissionAssignment [TenantId, RoleId, PermissionName]` | `[RemovedUtc] IS NULL` | correct |
| `Platform.TenantUserRoleAssignment [TenantId, TenantUserId, RoleId]` | `[RemovedUtc] IS NULL` | correct |
| `Platform.TenantCutoverOperation [TenantId]` | `[Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped')` | correct — one in-flight cutover per tenant |
| `Platform.TenantDatabaseAssignment [TenantId]` | `[EndedUtc] IS NULL` | correct — one live assignment per tenant |
| `Platform.TenantDatabaseRestoreVerificationRun [TenantDatabaseId]` | `[Status] IN (N'Admitted', N'Restoring')` | correct |
| `Tenant.LeaveRequest [TenantId, EmployeeId, StartDate, EndDate]` | `[Status] IN ('Submitted', 'Approved')` | correct — see the literal note below |
| `Tenant.PayrollRun [TenantId, CompanyId, PayrollPeriodId]` | `[ReversedUtc] IS NULL` | correct |
| `Tenant.Branch [TenantId]` | `[IsMainBranch] = 1 AND [IsActive] = 1` | correct — one active main branch per tenant |

## ⚠ The one worth naming: its correctness lives three files away

`AccountActionToken [Purpose, TenantId?, TenantUserId?]` is filtered on `[TenantUserId] IS NOT NULL`.
**`TenantId?` is nullable and the filter never mentions it.** Read alone, that is the defect shape — two
rows with `TenantId` null and the same `(Purpose, TenantUserId)` would collide.

**It is safe because the domain refuses the combination.** `AccountActionToken` throws unless the binding
is *Invitation* (`TenantId` non-empty **and** `TenantUserId > 0`) or *PasswordReset* (**both** null). So
`TenantUserId IS NOT NULL` implies `TenantId IS NOT NULL`, and the uncovered column cannot be null in any
row the filter admits. A composite foreign key on `(TenantId, TenantUserId)` says the same thing again.

**The index is correct because of an invariant the filter does not state.** Nothing connects the two, and
a change to that binding rule would make the index wrong silently.

**Observation, not a defect:** `LeaveRequest`'s filter uses non-Unicode literals (`'Submitted'`) where
every Platform filter uses `N'…'`. SQL Server converts implicitly and the filter still matches; it is an
inconsistency worth knowing rather than a fault.

## ⚠ Can the guard check correspondence cheaply? Partly — and I recommend against it

**A substring check needs no parsing**: every nullable column of an index must appear in an
`IS NOT NULL` clause of its filter. That reproduces the analysis above mechanically.

⚠ **But it would fire on `AccountActionToken`, which is correct.** One false positive in 16 today — and
per item 168's limit clause, **a guard that fires on correct code is the guard someone deletes.** It could
be suppressed with a named exception, which is the hand-written list problem item 169 was opened to
remove.

**Checking the other ten requires knowing what each index is FOR** — that `[RemovedUtc] IS NULL` is the
right scope for a role assignment is a domain judgement, not a property of the string. **That needs
re-parsing T-SQL and modelling intent, and per the ruling I did not attempt it.**

## Scope

- **Correctness was judged by reading, not by execution.** No filter was run against a database; the
  verdicts are arguments from the index's columns, the filter text, and in one case a domain invariant.
- **Provenance is inferred from shape**, because the finalised model does not carry configuration source.
  A hand-written filter identical to the convention's output is indistinguishable from it — and harmless,
  since both are correct.
- Non-unique indexes were not examined; a filter on one cannot cause this defect.
