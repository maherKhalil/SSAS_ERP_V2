---
document_id: FP-009-AUTH
title: HR Employee Data Exchange — Authorization Model
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Authorization Model

> One thing here is settled and must not be reopened; one is an owner decision with a recorded
> recommendation; the rest follows precedent mechanically.

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
| `HR.Employees.Import` | `FR-DOC-0101`, `FR-DOC-0102` | **`OD-DOC-005`** — proposed as separate; alternative is reuse of `HR.Employees.Create` |
| `HR.Employees.Export` | `FR-DOC-0201` | **`OD-DOC-005`** — proposed as separate; alternative is reuse of `HR.Employees.View` |
| `HR.Employees.View` | `FR-DOC-0103`, `FR-DOC-0202` — reading run history | Settled by pattern: history of employee operations is an employee read |
| `HR.EmployeeDocuments.View` | `FR-DOC-0302` — metadata listing | Proposed (`DEC-DOC-0013`) |
| `HR.EmployeeDocuments.Download` | `FR-DOC-0303` — content | Proposed (`DEC-DOC-0013`) |
| `HR.EmployeeDocuments.Upload` | `FR-DOC-0301` | Proposed |
| `HR.EmployeeDocuments.Withdraw` | `FR-DOC-0304` | Proposed |

**The recommendation for `OD-DOC-005`, restated with its precedent.** `DEC-DEP-0025` separated `Deactivate`
from `Update` because deactivation changes a materially different thing — whether a department can receive
employees — and granting it under ordinary edit authority would let someone who may rename a department undo
a closure. The same test applied here: bulk creation and bulk extraction are materially different from
single-record work, and export is the only operation in the module that removes data from the system's
control. It passes the same test that separation passed.

**The argument against, honestly stated:** two more permissions to administer, and every role that already
has `View` will probably be granted `Export` anyway, at which point the separation cost administration
overhead and bought nothing. That is a real possibility and it is the owner's call.

## Functional permission and scope stay independent dimensions

`ADR-025` decision 8: holding `Platform.Tenant.Administer` widens *scope* and grants **no** functional
permission. An administrator with no `HR.Employees.Export` cannot export, and an HR user with `Export` and a
single-branch scope exports a single branch. Neither dimension substitutes for the other, and the export
route checks both in the established order — permission first, then scope.

## Document content — the scope type is the permission

`DEC-POS-0018` established the mechanism this borrows: three distinct read-scope types with private
constructors and internal factories, so that a caller who did not pass the salary-grade check has **no code
path** to salary data — not a check they might bypass, a type they cannot construct.

Applied here:

```
EmployeeDocumentScope         ← resolver checked HR.EmployeeDocuments.View
EmployeeDocumentContentScope  ← resolver checked HR.EmployeeDocuments.Download
```

`IEmployeeDocumentReadService.GetContentAsync` takes an `EmployeeDocumentContentScope` and nothing else.
A metadata-only caller cannot reach content by any route, including a future one written by someone who
never read this document — which is the whole point of encoding it as a type rather than as a rule.

**Both scopes are derived from the employee's scope, not from a document scope of their own.** A document is
company-owned and names no branch ([`domain-model.md`](domain-model.md)), so its visibility is inherited: the
read proves the **employee** is in scope first and returns not-found if not, exactly as
`GetEmployeeBranchHistoryAsync` does for branch history. Without that step, a document read keyed by
`DocumentId` would be an unscoped read of employee data.

## Refusal semantics

| Condition | Answer | Precedent |
|---|---|---|
| No functional permission | `403 authorization.forbidden` | `FP-006` |
| Company unauthorized, inactive, unknown, wrong tenant | `403 company.scope_denied`, indistinguishable | `FP-006`, `ADR-025` |
| Branch selection missing for a branch-owned operation | `409 branch.selection_required` | `ADR-023` d.8 |
| Employee outside scope, for a document operation | `404 employee.not_found` | `FP-006` — existence is never disclosed |
| Document outside scope or unknown | `404 employee_document.not_found` | Same rule, own namespace (`DEC-DEP-0026`) |
| Content requested without the download permission | `403 authorization.forbidden` | Never `404` — the caller can see the document exists; concealing the refusal would tell them nothing they do not know, which is `DEC-DEP-0026`'s own reasoning for `PermissionDenied` |
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
