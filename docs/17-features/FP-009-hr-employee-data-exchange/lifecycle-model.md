---
document_id: FP-009-LIFE
title: HR Employee Data Exchange — Lifecycle Model
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Lifecycle Model

> Two of the three things here have no lifecycle at all, and saying so is the useful part. An import run and
> an export run are **records of completed facts**: they are written once, in a terminal state, and never
> transition.

## Import run — terminal on creation

```
(submitted) ──validate──▶ Validated   dry run, nothing written  (FR-DOC-0101)
            ──import────▶ Applied     employees created         (FR-DOC-0102)
            ──refuse────▶ Refused     nothing written
```

There is **no `InProgress` state**, and that is a consequence of `DEC-DOC-0007`'s synchronous execution
rather than an oversight. A persisted `InProgress` row is a promise that something will come back and finish
it; when the process dies, that promise is broken and the row is a permanent lie. Under synchronous
execution the run record is written when the outcome is known, so every row in the table describes something
that actually completed.

**If the caps ever rise and execution becomes asynchronous**, this is the first thing that changes, and it
changes honestly: an `InProgress` state arrives together with the machinery that resolves it — a timeout, an
owner, and a reconciliation pass. That machinery is exactly what `ADR-022` §14 needed for backup runs, and
its absence is why V1 does not take on the state.

| Outcome | Meaning |
|---|---|
| `Validated` | The file was checked and **nothing was written**. Reachable only through `FR-DOC-0101` |
| `Applied` | Employees were created. Under all-or-nothing (`OD-DOC-003`) this means every row; under partial success it means `AcceptedCount` of `RowCount` |
| `Refused` | The submission was rejected as a whole — a bad header, a cap exceeded, or every row invalid under all-or-nothing. **The import key is consumed anyway** (see [`data-model.md`](data-model.md#tenantemployeeimportruns)) |

## Export run — one state

```
(requested) ──▶ Completed
```

An export that fails writes no run record, because no data left the system. That is the opposite of the
import rule above, and the asymmetry is deliberate: an import run records *an attempt to write*, while an
export run records *bytes having left*. A failed export has nothing to disclose.

## Employee document — two states, and a deliberate absence

```
        upload                withdraw
(none) ────────▶ Active ──────────────▶ Withdrawn
                   ▲                        │
                   └──────── ✗ ─────────────┘
                     no reinstatement route
```

**`Active` → `Withdrawn` is one-way, and there is no reinstate operation.** Every other lifecycle in HR is
reversible — a department reactivates, an employee reactivates, a position reactivates — so the difference
needs a reason rather than an assumption.

The reason is `OD-DOC-008`. If the owner rules that withdrawal destroys content, reinstatement is
*impossible*, not merely disallowed; a route that exists but fails for half of all rows is worse than one
that does not exist. If the owner rules soft-only, reinstatement becomes possible and can be added — an
additive change with no contract break. **Specifying the reversible route now would be specifying a route we
may be unable to build**, which is the failure `DEC-POS-0034` describes in FP-007: a documented capability
that never shipped.

**Withdrawal is a named `POST`** (`POST /{documentId}/withdraw`), never a `DELETE` (`DEC-DEP-0024`), and the
verb is `withdraw` rather than `delete` precisely because the two possible rulings differ in whether bytes
survive. The route name should not assert an answer the owner has not given.

| State | Metadata | Content | Appears in `FR-DOC-0302` list |
|---|---|---|---|
| `Active` | Readable | Downloadable with the content permission | Yes |
| `Withdrawn` | Readable — who uploaded it and when survives | **Depends on `OD-DOC-008`** | Only when explicitly asked for |

**Withdrawn metadata always survives.** Whatever happens to the bytes, the fact that a document existed, who
uploaded it and who withdrew it is audit data under `BR-PLT-0004`, and destroying it would erase the record
of the erasure.

## Employee lifecycle interactions

| Event | Effect here |
|---|---|
| Employee is **terminated** | Documents are untouched. A terminated employee's contract is exactly the document most likely to be needed afterwards. Exports exclude them by default (`BRULE-DOC-0606`) but the documents remain readable to a caller who can read the employee |
| Employee **transfers branches** | No effect. The document is company-owned and names no branch ([`domain-model.md`](domain-model.md)); scope is inherited from the employee, so visibility follows the employee automatically |
| Employee **changes department or position** | No effect. Classification is not custody |
| Employee is created **by import** | Identical to any other creation: initial branch assignment, initial department and position assignments, all through the ordinary domain path (`BRULE-DOC-0603`) |

## What has no lifecycle and must not be given one

* **The submitted file.** It is not stored. Storing it would mean keeping rejected PII indefinitely with no
  rule saying for how long, and the operator already has the file.
* **The generated export.** It is the response body. It is not persisted, not retrievable later, and not
  given a download token — an artifact with a retrieval URL is a second, weaker authorization surface for
  data that was already authorized once.
* **Rejected rows.** Reported in the response (`DEC-DOC-0003`), counted on the run, never persisted.
