---
document_id: FP-009-LIFE
title: HR Employee Import and Export — Lifecycle Model
status: Approved for Implementation
version: 1.0
---

# FP-009 — Lifecycle Model

> **Approved 2026-08-22.** Neither thing left in this package has a lifecycle, and saying so is the useful
> part. An import run and an export run are **records of completed facts**: written once, in a terminal
> state, never transitioned.
>
> The employee-document lifecycle — the one-way `Active` → `Withdrawn` transition and the reasoning behind
> refusing a reinstatement route — travelled to [FP-010](../FP-010-hr-employee-documents/) with
> `OD-DOC-008`.

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
| `Applied` | Employees were created. Under `OD-DOC-003`'s all-or-nothing ruling this means **every** row — `AcceptedCount` always equals `RowCount` on an applied run, which is a property a test can assert rather than a convention to remember |
| `Refused` | The submission was rejected as a whole — a bad header, a cap exceeded, or **any** row invalid, which under `OD-DOC-003` is the same thing. **The import key is consumed anyway** (see [`data-model.md`](data-model.md#tenantemployeeimportruns)) |

## Export run — one state

```
(requested) ──▶ Completed
```

An export that fails writes no run record, because no data left the system. That is the opposite of the
import rule above, and the asymmetry is deliberate: an import run records *an attempt to write*, while an
export run records *bytes having left*. A failed export has nothing to disclose.

## Employee document lifecycle — transferred to FP-010

The two states, the deliberately absent reinstatement route, and the argument for naming the operation
`withdraw` rather than `delete` while `OD-DOC-008` is open all moved to
[FP-010](../FP-010-hr-employee-documents/carried-analysis.md) under the `OD-DOC-001` split.

## Employee lifecycle interactions

| Event | Effect here |
|---|---|
| Employee is **terminated** | Exports exclude them by default and include them when asked for by name (`BRULE-DOC-0606`). An import cannot create a terminated employee: creation produces `Active`, and status is not a column (`AC-DOC-0002`) |
| Employee **transfers branches** | No effect on either run record — both are historical facts about an operation, not about a person. A **later** export by the same caller may return a different row set, which is scope working rather than history changing |
| Employee **changes department or position** | No effect. A run record names no employees |
| Employee is created **by import** | Identical to any other creation: initial branch assignment, initial department and position assignments, all through the ordinary domain path (`BRULE-DOC-0603`) |

## What has no lifecycle and must not be given one

* **The submitted file.** It is not stored. Storing it would mean keeping rejected PII indefinitely with no
  rule saying for how long, and the operator already has the file.
* **The generated export.** It is the response body. It is not persisted, not retrievable later, and not
  given a download token — an artifact with a retrieval URL is a second, weaker authorization surface for
  data that was already authorized once.
* **Rejected rows.** Reported in the response (`DEC-DOC-0003`), counted on the run, never persisted.
