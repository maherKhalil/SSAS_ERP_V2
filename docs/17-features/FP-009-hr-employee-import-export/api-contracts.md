---
document_id: FP-009-API
title: HR Employee Import and Export — API Contracts
status: Approved for Implementation
version: 1.0
---

# FP-009 — API Contracts

> **Approved 2026-08-22. Five routes**, `OD-DOC-001` having split the four document routes out to FP-010.
> Routes are justified by scope, not generated from handlers, and the 1:1 route-to-handler property
> `DEC-DEP-0023` established holds here too.
>
> This surface breaks one convention the module has held since FP-006 — it accepts and returns something
> other than JSON — and that break is argued rather than assumed, below.

## Routes

| Method | Route | Permission | Requirement |
|---|---|---|---|
| `POST` | `/api/hr/employees/import/validate` | `HR.Employees.Import` | `FR-DOC-0101` |
| `POST` | `/api/hr/employees/import` | `HR.Employees.Import` | `FR-DOC-0102` |
| `GET` | `/api/hr/employees/import-runs` | `HR.Employees.View` | `FR-DOC-0103` |
| `GET` | `/api/hr/employees/export` | `HR.Employees.Export` | `FR-DOC-0201` |
| `GET` | `/api/hr/employees/export-runs` | `HR.Employees.View` | `FR-DOC-0202` |

*(The four document routes `FR-DOC-0301`–`0304` travelled to [FP-010](../FP-010-hr-employee-documents/).)*

**No `DELETE` verb anywhere** (`DEC-DEP-0024`), and every state change is a named `POST`.

**Why `import/validate` is a `POST` and not a `GET`.** It writes nothing, which is usually the test for a
`GET` — but it *carries a file*, and a request body on a `GET` is outside what the platform's transports and
intermediaries handle predictably. `POST` here means "this is a request with a payload", not "this mutates".
The run record it writes with outcome `Validated` is the honest exception, and it is audit rather than state.

**Route inventory obligation.** The HR surface is **41 routes** today, asserted exactly by
`HrRouteInventoryTests` beside the full list. This package takes it to **46**. The count and the list are both
updated, or the guard fails — which is the point of it, and FP-008 paid for learning that a count alone goes
vacuously green.

## The convention this surface breaks

Every HR route to date accepts and returns `application/json` with strict binding. These do not:

| Route | Request | Response |
|---|---|---|
| `POST .../import`, `.../import/validate` | **`text/csv`** — the file **is** the body | `application/json` |
| `GET .../export` | — | `text/csv` |

> **SUPERSEDED 2026-08-22 by `DEC-DOC-0014`, and recorded rather than rewritten.** This table said
> `multipart/form-data` — one file part plus an `importKey` field — and the row above is the as-built answer.
> Multipart is browser-form machinery serving no API-first need, and it drags in form-parsing limits and a
> test-harness apparatus for nothing.
>
> The argument the original made is untouched and is quoted below because it is still right: it is an
> argument against putting a file in JSON, which is not the same as an argument for multipart. The property
> it was protecting survives — see `DEC-DOC-0014` for the mechanism, and for the one question the change
> leaves open (where `importKey` now travels, which Phase 2 settles).

**The break is unavoidable and is contained.** A file cannot be a JSON field without base64, which inflates
it by a third and forces the whole payload into memory before parsing. What is preserved is the part that
matters: **the metadata half stays strict.** ~~The form's non-file fields are~~ **The request's declared
parameters are** validated against a declared set and an unrecognized field is `400 request.invalid`, exactly
as an undeclared JSON property is. The strictness was never about JSON; it was about never silently ignoring
input.

**No `Content-Disposition` filename echo from caller input on the export.** The export file name is
server-generated. Reflecting a caller-supplied name into a response header is a header-injection surface for
no benefit.

> **THE SERVER-GENERATED FORM, RECORDED 2026-08-22 (`R10`): `employees-{yyyyMMdd-HHmmss}.csv`**, stamped from
> the clock at execution and from nothing else.
>
> The contract above states the PROPERTY — server-generated, no caller input — and was silent on the value. A
> bare constant `employees.csv` was considered and declined, and the reason is that the name and the run
> record do two different jobs:
>
> * **Identification lives in the run record.** Who exported, when, under which scope, and which column set
>   left are `EmployeeExportRun`'s fields (`DEC-DOC-0006`, `SEC-DOC-0404`). The filename carries none of that
>   and does not need to — which is the argument for a constant, and it is correct as far as it goes.
> * **Collision-avoidance lives in the name**, and the run record cannot do it. Two exports in one session
>   under a constant name silently overwrite in the operator's downloads folder, or become
>   `employees (1).csv` and lose the order they were taken in. That is a usability failure the audit trail
>   never sees, because nothing about it reaches the server.
>
> The timestamp satisfies the stated property exactly as a constant would: it is derived from the server's
> clock, and no caller input reaches it.

## Import

```http
POST /api/hr/employees/import
X-Company-Id: …
Content-Type: text/csv; charset=utf-8
```

*(Superseded shape, `DEC-DOC-0014`: this read `Content-Type: multipart/form-data` with a `file` part and an
`importKey` field.)*

| Input | Type | Required |
|---|---|---|
| body | CSV, UTF-8, ≤ 10 MB, ≤ 5,000 data rows | Yes |
| `importKey` | text, ≤ 128 chars — **where it travels is Phase 2's to settle** (`DEC-DOC-0014`) | Yes (`DEC-DOC-0004`) |

**`charset` is checked, not ignored.** `text/csv` and `text/csv; charset=utf-8` are the same contract; a
request declaring any *other* charset is refused rather than decoded as UTF-8 anyway, and so is a body whose
bytes are not valid UTF-8. A UTF-8 BOM is stripped, because `DEC-DOC-0008` has exports emit one and requires
an exported file to re-import.

### Column contract

Matched case-insensitively, order-independent; a missing required column or an unrecognized column is
refused before any row is read (`DEC-DOC-0002`).

| Column | Required | Notes |
|---|---|---|
| `employeeNumber` | Yes | Unique per company, normalized |
| `fullName` | Yes | |
| `employmentDate` | Yes | ISO-8601 |
| `departmentCode` | Yes | Resolved per `OD-DOC-004`; never created (`BRULE-DOC-0601`) |
| `positionCode` | Yes | Same |
| `nationalId` | No | Optional in FP-006, and **kept optional here precisely because `OD-DOC-006` made it export-absent** — an exported file must re-import, and it will never carry this column |
| `status` | No | **`OD-DOC-010`, ruled 2026-08-22.** The mirror image of `nationalId`: the one column exports *add*. Empty or `Active` passes; any other value is a **row error** naming the remedy. Recognized, never ignored — it sets nothing, and creation still produces `Active` |

**No `companyId`, `branchId` or `tenantId` column exists.** They are not validated away — they are absent
from the contract, so a file carrying one is refused by the unknown-column rule. This is `FP-006`'s "absent
by construction, not merely validated" applied to a header row.

*(`status` was a fourth until `OD-DOC-010`. It is now declared and constrained by VALUE rather than refused
by name, which is what lets a re-imported export be told **why** its status column cannot be honoured.)*

### Response — the per-row report

```jsonc
{
  "importRunId": "…",
  "outcome": "Applied",              // Validated | Applied | Refused
  "rowCount": 1000,
  "acceptedCount": 998,
  "rejectedCount": 2,
  "errors": [
    { "rowNumber": 14,  "column": "employeeNumber", "code": "employee.number_conflict",
      "message": "…" },
    { "rowNumber": 902, "column": "departmentCode", "code": "department.not_found",
      "message": "…" }
  ]
}
```

`rowNumber` is the **1-based line number in the submitted file, header included** — the number the operator's
editor shows them. `code` comes from the module's existing problem-code namespaces, because a row failing
uniqueness fails it for exactly the reason a single create would, and inventing import-specific codes for the
same conditions would give one failure two names.

**`outcome` under `OD-DOC-003`, now ruled:** all-or-nothing, so `acceptedCount` is either `rowCount` or `0`
and never anything between. The sample above shows `Applied` with 998 of 1000 — **that response is no longer
reachable**, and it is kept as written with this note rather than quietly corrected, because the shape it
illustrates is the one the report still has. A file with two bad rows now answers `Refused`, `acceptedCount`
`0`, and the same two errors.

## Export

```http
GET /api/hr/employees/export?status=Terminated&branchScope=AllAuthorizedBranches
X-Company-Id: …
```

Accepts **the same query parameters as employee search**, with the same strict allowlist, the same defaults
and the same refusals. An export is a search that leaves the system, so it must not grow a second, subtly
different filter vocabulary — and the FP-009 audit-of-the-audit lesson applies: a parameter implemented below
the transport and unreachable above it is a capability nobody can use.

> **ONE EXCEPTION, ANNOTATED 2026-08-22 (`R7`): `pageNumber` and `pageSize` are REFUSED** with
> `400 request.invalid` naming the reason. An export is not paged — a file with a page 2 is not a file — and
> the row **ceiling** governs its size instead.
>
> **Accept-and-ignore was forbidden by `OD-DOC-010`'s own logic**, taken two days earlier: silently
> discarding a declared parameter is the behaviour this contract refuses, and a caller who sent
> `pageSize=50` and received five thousand rows would have been told nothing about what happened to their
> request. The filter vocabulary is genuinely shared — the parsing core is extracted, not copied, so the two
> surfaces cannot drift — and paging is the one input where "the same allowlist" would have meant accepting
> something the operation cannot honour.

Response: `text/csv`, UTF-8 with BOM (Excel opens UTF-8 without a BOM as mojibake, and this file exists to be
opened in Excel), ordered by full name then identifier — the same total order search uses, so paging and
export agree.

**Columns: `employeeNumber`, `fullName`, `employmentDate`, `departmentCode`, `positionCode`, `status`.**
**`nationalId` is not among them and cannot be** (`OD-DOC-006`) — there is no parameter, permission or caller
for which it appears. It is absent from the contract rather than filtered out of it, which is the distinction
`FP-006` draws between a field that does not exist and a field that is validated away.

> **AS BUILT. `OD-DOC-010` RULED 2026-08-22 and the round trip closes — with one stated limit.**
>
> These six ship exactly as listed, and `status` is now a **recognized optional import column** whose only
> accepted value is `Active`. An export of active employees re-imports unmodified; a `status=Terminated`
> export refuses with a named **row** error rather than a header rejection, which is correct — create-only
> cannot recreate a terminated person's employment history.
>
> **The limit, stated because the ruling's premise was narrower than it read:** a default export is `Active`
> **and `Inactive`**, not all-Active, so a default export containing an inactive employee also refuses. The
> round trip closes for an export whose rows are all `Active`. See `OD-DOC-010`.

**Permissions: `HR.Employees.Export` AND `HR.Employees.View`.** `OD-DOC-005` settles that neither implies the
other; `DEC-DOC-0015` records that an export is a read and therefore takes the read authority as its floor,
with the export authority granted on top. A caller holding `View` and not `Export` is refused and writes no
run record.

**Bounded, and refused rather than truncated.** An export is not paged — a file with a page 2 is not a file —
but it is capped at `DEC-DOC-0005`'s row limit, and a request whose result would exceed it is **refused**
naming the limit. Returning the first N of a larger set would hand the operator a file that looks complete.
The bound is also what makes the buffered response honest: everything a caller can ask for fits in memory by
construction.

## Documents — transferred to FP-010

The upload, list, content and withdraw contracts, the metadata representation, and the reasoning for
refusing a content URL in that representation moved to
[FP-010](../FP-010-hr-employee-documents/carried-analysis.md) under the `OD-DOC-001` split.

## Problem codes

Own namespaces (`DEC-DEP-0026`), reusing existing codes where the condition is genuinely the existing one.

| Condition | Status | Code |
|---|---|---|
| Malformed file, bad header, unknown column, cap exceeded, unparsable row | `400` | `request.invalid` |
| Unsupported file format | `400` | `employee_import.format_unsupported` |
| A row naming a status an import cannot create *(added 2026-08-22, `R9`)* | *(in the report)* | `employee_import.status_not_creatable` |
| Import key already used | `200` | — the original run's result (`DEC-DOC-0004`) |
| Row-level uniqueness, transition and reference failures | *(in the report)* | `employee.number_conflict`, `employee.national_id_conflict`, `department.not_found`, `position.not_found` |
| Missing functional permission | `403` | `authorization.forbidden` |
| Company / branch scope refusals | `403` | `company.scope_denied`, `branch.scope_denied` |

> **WHY `employee_import.*` AND NOT `employee.*` (annotated 2026-08-22).** Every other row-level code above
> reuses an EMPLOYEE-domain code, on the stated ground that *"a row failing uniqueness fails it for exactly
> the reason a single create would"*. `status_not_creatable` is the one row error with **no single-create
> counterpart**: a `POST` carrying a status is refused by the JSON contract's declared field set, because
> `status` is not a field there at all. It is a rule of the IMPORT CONTRACT — which columns a file may carry
> and what values they may hold — rather than a rule of the employee domain, so it takes the namespace the
> contract already opened for exactly that class of failure (`employee_import.format_unsupported`).
>
> It is a **row** error, reported inside the per-row report rather than as a ProblemDetails, because
> `OD-DOC-010` chose a named row refusal over a header rejection precisely so the message could name the
> remedy.

**No `409` appears on this surface**, and the absence is worth a sentence: `DEC-DEP-0030` fixed `409` as the
answer for a **state-conflict** refusal, and this package has no state to conflict with. An import creates or
refuses; an export reads. The one condition that looks like a conflict — a re-used import key — is
deliberately a `200` carrying the original result, because the caller asking "did my import happen?" is asking
a question the system can answer rather than making a request it must refuse.

## Rowversion

**No route on this surface carries a rowversion**, and every one of them is entitled not to. An import
creates — and creation has never carried an expected version, because there is nothing to have changed
underneath the caller. An export changes nothing. Both run records are append-only and have no concurrency
token at all, which is the `EmployeeBranchAssignment` precedent.

*(`FR-DOC-0304`, the one operation here that would have carried one, travelled to FP-010.)*
