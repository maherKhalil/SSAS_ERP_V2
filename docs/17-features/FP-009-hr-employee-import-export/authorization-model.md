---
document_id: FP-009-AUTH
title: HR Employee Import and Export — Authorization Model
status: Approved for Implementation
version: 1.0
---

# FP-009 — Authorization Model

> **Approved 2026-08-22.** One thing here was settled from the start and must not be reopened — export runs
> under the caller's scope. One was an owner decision, and `OD-DOC-005` ruled it as recommended. The rest
> follows precedent mechanically.

## Settled — export runs under the caller's employee read scope

`ADR-023` decision 22 and `ADR-025` decision 10 require every employee read to carry a **materialized** scope
— an explicit `TenantId = @tenant AND CompanyId IN (…) AND BranchId IN (…)` predicate, never an omitted
condition. `DEC-EMP-0029` implements this as a type: `EmployeeReadScope` cannot be constructed outside the
resolver, and no read method exists that does not take one.

**An export is a read**, and the widest one the module performs. It obtains a scope exactly as search does.
Consequences, stated so they cannot be quietly traded away later:

* A caller authorized for one branch exports one branch's employees.
* "All authorized branches" is a **materialized identifier list**, never the absence of a predicate
  (`BRULE-EMP-0025`).
* An empty authorized set is `403`, not an unfiltered extract.
* **There is no administrative or full-tenant export mode.** If a tenant-wide extract is ever required it is
  a platform operation with its own authority model, not an HR route — the same separation `ADR-020` draws
  between an ERP write and a migration copy.

This is the reason `EmployeeExportRun.ScopeSummary` exists: the scope in force *at the time* is recorded,
because scope changes and `AC-EMP-0024`/`AC-EMP-0026` exist precisely because it does.

## Permissions

| Permission | Governs | Status |
|---|---|---|
| `HR.Employees.Import` | `FR-DOC-0101`, `FR-DOC-0102` | **RULED** (`OD-DOC-005`) — separate, granted independently |
| `HR.Employees.Export` | `FR-DOC-0201` | **RULED** (`OD-DOC-005`) — separate, granted independently |
| `HR.Employees.View` | `FR-DOC-0103`, `FR-DOC-0202` — reading run history | Settled by pattern: history of employee operations is an employee read |

**Two new permissions, taking the HR set from 21 to 23.** The permission catalog and its inventory guards are
updated with them, or the guards fail — which is what they are for.

**Holding `Import` does not imply `Create`, and holding `Export` does not imply `View`.** They are independent
grants, not tiers. A role may hold `Export` alone: such a caller can extract exactly the employees their scope
admits and cannot open one of them individually, which is odd-looking and correct — the two are different
capabilities over the same data, and inventing an implication between them would be inventing policy.

**But the import path still needs an employee read scope**, because it reads back what it created and it
resolves department and position codes under the caller's authority (`OD-DOC-004`). That is a *scope*
requirement, not a permission one, and the distinction is `ADR-025` decision 8's: scope and functional
permission are independent dimensions, and neither substitutes for the other.

*(The four `HR.EmployeeDocuments.*` permissions the analysis proposed travelled to FP-010 with `DEC-DOC-0013`.)*

**The recommendation `OD-DOC-005` adopted, restated with its precedent.** `DEC-DEP-0025` separated `Deactivate`
from `Update` because deactivation changes a materially different thing — whether a department can receive
employees — and granting it under ordinary edit authority would let someone who may rename a department undo
a closure. The same test applied here: bulk creation and bulk extraction are materially different from
single-record work, and export is the only operation in the module that removes data from the system's
control. It passes the same test that separation passed.

**The argument against, honestly stated:** two more permissions to administer, and every role that already
has `View` may be granted `Export` anyway, at which point the separation cost administration overhead and
bought nothing. **Ruled for separation regardless**, on the ground that the cost is paid once by
administrators while the risk is carried continuously by the data — and that a grant nobody had to make
deliberately is exactly the grant nobody reviews.

## Functional permission and scope stay independent dimensions

`ADR-025` decision 8: holding `Platform.Tenant.Administer` widens *scope* and grants **no** functional
permission. An administrator with no `HR.Employees.Export` cannot export, and an HR user with `Export` and a
single-branch scope exports a single branch. Neither dimension substitutes for the other, and the export
route checks both in the established order — permission first, then scope.

## Document content permissions — transferred to FP-010

`DEC-DOC-0013`'s split between metadata and content, and the `DEC-POS-0018` scope-type mechanism that makes
it structural rather than procedural, moved to
[FP-010](../FP-010-hr-employee-documents/decisions-open.md#ratified-decisions-carried-into-fp-010).

## Refusal semantics

| Condition | Answer | Precedent |
|---|---|---|
| No functional permission | `403 authorization.forbidden` | `FP-006` |
| Company unauthorized, inactive, unknown, wrong tenant | `403 company.scope_denied`, indistinguishable | `FP-006`, `ADR-025` |
| Branch selection missing for a branch-owned operation | `409 branch.selection_required` | `ADR-023` d.8 |
| A department or position code that does not resolve in the caller's scope | A **row error** naming the column, which under `OD-DOC-003` refuses the file | `OD-DOC-004` — and it is a row error rather than a `404` because the caller addressed a file, not a department |
| Import file malformed, header wrong, cap exceeded | `400 request.invalid` — with the per-row report as the body where rows were reachable | `DEC-DOC-0003` |
| Import key already used | `200` with the **original** run's result | `DEC-DOC-0004`; a conflict status would push callers into treating a successful idempotent replay as a failure |

## What an import may write, and what it may not

An import runs inside the caller's trusted company and branch execution context. It writes employees **into
that context** and nowhere else (`SEC-DOC-0403`). A file value naming another company or branch is refused,
never adopted — the same rule that makes `X-Company-Id` an expression of intent rather than authorization
proof, applied to a column instead of a header.

**An import cannot perform a transfer** (`BRULE-DOC-0602`). Moving an employee between branches crosses an
authorization boundary, carries its own permission and writes its own history record. A spreadsheet column
must not be able to do it silently.
