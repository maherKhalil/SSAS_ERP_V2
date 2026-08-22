---
document_id: FP-009-DEC
title: HR Employee Import and Export — Decisions and Classification
status: Approved for Implementation
version: 1.0
---

# FP-009 — Decisions and Classification

> **Disposed 2026-08-22.** All thirteen proposals were **ratified as drafted**; all nine owner decisions were
> disposed — six ruled here, three deferred to FP-010 under `OD-DOC-001`. The classifications below are kept
> as they were written, because the classification is the record of *where each answer came from*: precedent,
> engineering, or the owner. Rulings are **appended** to the questions they closed, never written over them.
>
> | Classification | Meaning |
> |---|---|
> | **SETTLED-BY-PRECEDENT** | An existing ADR or ratified decision already answers this. The citation is given. Re-deciding it here would be a second opinion that could disagree with shipped code |
> | **PROPOSED** | An engineering decision, made here, ratifiable as drafted. It is labeled so nobody mistakes it for precedent |
> | **OWNER-DECISION-REQUIRED** | A business, product or compliance call. Raised, never made |
>
> **Nothing is settled by resemblance.** Where FP-006/007/008 did something similar but not the same, the
> difference is stated rather than smoothed over.

## Classification table

### Import

| # | Topic | Classification | Basis |
|---|---|---|---|
| 1 | File format | **PROPOSED** `DEC-DOC-0001` | No precedent — no file has ever crossed this boundary |
| 2 | Header and column contract | **PROPOSED** `DEC-DOC-0002` | Extends the strict-JSON convention (`FP-006` api-contracts) to a new medium |
| 3 | Validation-report shape | **PROPOSED** `DEC-DOC-0003` | ProblemDetails precedent does not reach a per-row report |
| 4 | Atomicity — partial vs all-or-nothing | **RULED** `OD-DOC-003` → **all-or-nothing** | A business call about what HR wants to happen to 998 good rows |
| 5 | Create-only vs upsert | **RULED** `OD-DOC-002` → **create-only** | Changes which permission the operation needs and what it can destroy |
| 6 | Idempotency / re-run semantics | **RATIFIED, RESTATED** `DEC-DOC-0004` | Its dependency closed with `OD-DOC-003`; restated below on the narrower job that remains |
| 7 | Resolving department / position / branch | **RULED** `OD-DOC-004` → **by code; never creates** | Whether a file may create organizational structure is not an engineering question |
| 8 | Import permission | **RULED** `OD-DOC-005` → **separate** | Sensitivity judgement; the engineering recommendation was adopted |
| 9 | Size and row caps | **PROPOSED** `DEC-DOC-0005` | Bounded-read precedent (`FP-006`, page size ≤ 200) extends naturally |
| 10 | Audit trail of who imported what | **PROPOSED** `DEC-DOC-0006` | `BR-PLT-0004` requires audit; the shape is engineering |
| 11 | Tenant/company/branch ownership of import records | **SETTLED-BY-PRECEDENT** | `ADR-023`, `ADR-025`; `ICompanyOwnedEntity` stamping |
| 12 | Synchronous vs asynchronous execution | **PROPOSED** `DEC-DOC-0007` | Follows from the row cap; revisitable without contract change |

### Export

| # | Topic | Classification | Basis |
|---|---|---|---|
| 13 | **Export runs under the caller's employee read scope** | **SETTLED-BY-PRECEDENT** | `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029`. Stated below in full because it is the one thing that must not be reopened |
| 14 | Export format | **PROPOSED** `DEC-DOC-0008` | Symmetry with import |
| 15 | PII surface — `nationalId` in an export | **RULED** `OD-DOC-006` → **never exported** | Compliance call; `DEC-EMP-0030` was the sensitivity precedent, not the answer |
| 16 | Export permission | **RULED** `OD-DOC-005` → **separate** | Same decision as import, taken once |
| 17 | Terminated employees in exports | **PROPOSED** `DEC-DOC-0009` | The search default (`FP-006` api-contracts) extends; the exception is stated |
| 18 | Export bounded by the same paging maxima | **SETTLED-BY-PRECEDENT** | `FP-006` api-contracts — "reads are bounded"; an export is a read |
| 19 | Audit trail of who exported what | **PROPOSED** `DEC-DOC-0006` | Same record as import; an export is the higher-risk half |

### Documents — **transferred to FP-010** (`OD-DOC-001`)

Topics 20 to 29 of the original classification — binary storage location, the `ADR-028` requirement, metadata
in the E3 manifest, the fact that a row copy does not move content, size and content-type ceilings, the
document-type taxonomy, retention against the no-physical-delete convention, document permissions, roadmap
ownership and malware scanning — **moved to
[FP-010](../FP-010-hr-employee-documents/decisions-open.md) with their classifications and options tables
intact**, together with `DEC-DOC-0010`–`0013` and `OD-DOC-007`–`009`.

They are not repeated here. A package that kept a copy of another package's open questions would give them
two homes and eventually two answers.

### Cross-cutting

| # | Topic | Classification | Basis |
|---|---|---|---|
| 30 | Concurrency — rowversion on every mutation | **SETTLED-BY-PRECEDENT** | `Development-Standards.md`; `FP-006` rowversion transport |
| 31 | Lifecycle — named `POST` routes, no `DELETE` verb | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0024` |
| 32 | Problem codes in a per-resource namespace | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0026` |
| 33 | State-conflict refusals answer `409` | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0030` |
| 34 | Uniqueness normalized, scoped per company | **SETTLED-BY-PRECEDENT** | `DEC-EMP` employee-number pattern; `DEC-DEP` code pattern |
| 35 | Searchable text needs a normalized column | **SETTLED-BY-PRECEDENT** | `DEC-POS-0030`, `DEC-POS-0031` |
| 36 | Persisted application strings are `nvarchar` | **SETTLED-BY-PRECEDENT** | Standing platform rule |
| 37 | No cross-database foreign key | **SETTLED-BY-PRECEDENT** | `ADR-017`, `DEC-EMP-0027` |
| 38 | E3 manifest movements and the **nine-site** inventory | **SETTLED-BY-PRECEDENT** | `DEC-DEP-0029`, `DEC-POS-0022` — the mechanism is reflection, the obligation is to update every dependent site |
| 39 | Migration obligations for required columns on populated tables | **SETTLED-BY-PRECEDENT** | `DEC-POS-0026` lineage — add nullable, backfill, tighten; never `defaultValue` |
| 40 | Module isolation — HR references only BuildingBlocks | **SETTLED-BY-PRECEDENT** | `ADR-012`, `DEC-DEP-0026` |

**The original analysis classified forty topics: 15 settled by precedent, 16 proposed, 9 owner decisions.**
After the split, **thirty remain in FP-009** — 15 settled by precedent, 9 proposed and ratified
(`DEC-DOC-0001`–`0009`), and 6 owner decisions, all now ruled. The ten documents topics, carrying
`DEC-DOC-0010`–`0013` and `OD-DOC-007`–`009`, are FP-010's.

---

## Settled by precedent — the two worth writing out

**Export runs under the caller's employee read scope, and this is not open.** `ADR-023` decision 22 and
`ADR-025` decision 10 require every employee read to carry a materialized scope; `DEC-EMP-0029` implements it
as a type (`EmployeeReadScope`) that only the resolver can construct. An export is a read — the widest one
the module will ever perform — so it obtains a scope exactly as search does, and a caller authorized for one
branch exports one branch. **A "full export" mode that bypasses scope would contradict every decision since
`ADR-023` and is not on the table at any classification.** If a tenant-wide extract is ever needed, it is a
platform operation with its own authority model, not an HR route.

**The second settled statement — that binary content does not move during Shared→Dedicated cutover —
travelled to [FP-010](../FP-010-hr-employee-documents/) with the documents material.** It settles nothing in
this package, because nothing here stores bytes: an import reads a file the caller uploads and keeps none of
it, and an export writes a response body and keeps none of that either.

---

## Proposed decisions

**`DEC-DOC-0001` — Import accepts UTF-8 CSV only in V1.** Comma-delimited, `CRLF` or `LF`, RFC 4180 quoting,
a UTF-8 BOM tolerated and stripped. **XLSX is rejected** — it is a zip container requiring a parsing
dependency with its own vulnerability surface, and the requirement says "import", not "import from
spreadsheets". The response to an unsupported format is `400`, naming CSV.

**`DEC-DOC-0002` — The header row is a strict contract, order-independent.** Column names are matched
case-insensitively against a fixed set; a **missing required column** and an **unrecognized column** are both
refusals, before any row is read. This is the strict-JSON rule (`FP-006` api-contracts: unknown fields are
`400 request.invalid`) applied to a new medium, and for the same reason — a silently ignored column is a
false belief about what was imported.

**`DEC-DOC-0003` — The validation report is per row, and every row is validated.** Not first-failure. A
report naming one bad row in a thousand costs the operator a thousand round trips to find the rest. The shape
is a list of `{ rowNumber, column, code, message }`, where `code` is a problem code from the module's own
namespace, and `rowNumber` is the **1-based line number in the submitted file including the header**, because
that is what the operator's editor shows them.

**`DEC-DOC-0004` — Idempotency by explicit import key.** *(Restated 2026-08-22, dependency closed.)* The
caller supplies an `importKey` (a client-chosen identifier, unique per company); a second submission carrying
a key already recorded returns the **original** run's result rather than importing again.

**What the key now protects against, precisely.** `OD-DOC-003`'s all-or-nothing ruling removed the hard case
this decision was originally written to survive. There are no partially applied files any more, so a re-run
is never a question of "which rows already landed":

* a **failed** run wrote nothing, so re-running the corrected file is an ordinary first import;
* a **successful** run created every employee in the file, so re-running it is a file of duplicate employee
  numbers — rejected rows under `OD-DOC-002`, and therefore a refused file under `OD-DOC-003`.

The remaining job is the **ambiguous timeout**: the operator whose connection dropped and who cannot tell
whether five thousand employees exist. Replaying the key answers that question exactly, and answers it
without a second import. That is narrower than the original framing and worth stating plainly rather than
leaving the decision looking like it does more than it does.

Note the deliberate asymmetry with the duplicate-rejection path: **a key replay returns the original
result** — a `200` — while a genuinely re-submitted *file* of existing employee numbers is a refusal. The two
are different questions ("did my import happen?" versus "please import these people"), and answering them
identically would tell the operator nothing about which one they asked.

**`DEC-DOC-0005` — Caps: 5,000 rows and 10 MB per file.** Aligned with the platform's bounded-read posture
(`FP-006` caps a page at 200) and with `DEC-DOC-0007`'s synchronous execution. Both are configuration values,
not architectural constants; exceeding either is `400` and names the limit and the actual size.

**`DEC-DOC-0006` — Every import and every export writes a durable run record.** `EmployeeImportRun` and
`EmployeeExportRun`: who, when, which company, the file name and byte count, the row counts (submitted,
accepted, rejected), the outcome, and — for exports — **the row count and the column set that left the
system**. `BR-PLT-0004` requires an audit trail; for export it is the *only* control that survives the data
leaving. Both are append-only and never updated.

**`DEC-DOC-0007` — Import and export execute synchronously in V1.** Within the caps above this is a
sub-second to few-second operation, and an asynchronous job introduces a queue, a status route, a retention
policy for results and a failure mode where the caller never learns the outcome. When the caps rise, this is
revisited — the contract is written so an async variant is additive.

**`DEC-DOC-0008` — Export produces UTF-8 CSV with the same column contract import accepts.** The
round-trip property is the point: an export, edited and re-imported, must be a legal import. Where
`OD-DOC-006` removes a column from exports, that column is *optional* on import, so the property holds.

**`DEC-DOC-0009` — Exports exclude terminated employees unless explicitly requested.** The employee search
already behaves this way (`FP-006` api-contracts: "search defaults to excluding Terminated"), and an export
is a search that leaves the building. `status=Terminated` includes them by name, so audit and payroll
extracts remain possible.

**`DEC-DOC-0010`–`DEC-DOC-0013`** — the `ADR-028` requirement, the content-type allowlist and size ceiling,
the document-type enum, and the split between metadata and content permissions — were **ratified as drafted
and annotated FP-010-scoped**. Their text lives in
[FP-010](../FP-010-hr-employee-documents/decisions-open.md#ratified-decisions-carried-into-fp-010).

---

## Owner decisions — disposed

**`OD-DOC-001` — Packaging.** Stated in full in the original analysis with both options, their consequences
and a labeled engineering recommendation (Option B).

> **RULED 2026-08-22 — SPLIT, Option B.** FP-009 is Employee Import and Export; Employee Documents becomes
> **FP-010**, gated on `ADR-028`. The recommendation was adopted on the blocking asymmetry: a storage ADR
> that import and export have no use for was made a precondition for shipping them, and nothing bought that
> cost. The [README](README.md#the-packaging-ruling--od-doc-001--split) records how the split was executed —
> along the line the analysis was already written along, with every identifier keeping its number.

**`OD-DOC-002` — Does import create only, or also update existing employees?**

| Option | Consequence |
|---|---|
| **Create-only** | An import naming an existing employee number is a rejected row. Simplest; needs only creation authority; cannot destroy data |
| **Upsert** | The file becomes an instrument of bulk *modification*. It needs update authority as well, and a mis-keyed column can overwrite a thousand records at once |

**Why it is not ours.** "Import employees" reads naturally as either. The difference is what an operator can
destroy with one mistake, and how much of the reversal burden falls on backups.

> **RULED 2026-08-22 — CREATE-ONLY.** A row naming an employee number that already exists in the company is a
> **rejected row** (`BR-HR-0001`), which under `OD-DOC-003` fails the file. Updates stay on the audited
> single-record routes, where each change carries its own rowversion, its own actor and its own audit entry.
>
> **Upsert is recorded as a possible future ADDITIVE mode and is deliberately not designed here.** Sketching
> a route nobody has approved is how a specification acquires a capability that never ships — the
> `DEC-POS-0034` failure, committed in advance. If bulk update is wanted later it arrives as its own
> decision, with its own permission question, and it does not silently inherit `HR.Employees.Import`.

**`OD-DOC-003` — All-or-nothing, or partial success?**

| Option | Consequence |
|---|---|
| **All-or-nothing** | 999 valid rows are rejected because of one bad row. Predictable: the import either happened or it did not, and re-running after a fix is unambiguous |
| **Partial success** | 999 rows land; the operator fixes one and re-submits. Faster in practice, but the system is now in a state no single file describes, and re-running the corrected file must not duplicate the 999 (see `DEC-DOC-0004`) |

**Engineering note, not a recommendation:** all-or-nothing is materially simpler to make correct, because it
needs no idempotency reasoning about partially applied files. Partial success is what most operators expect.

> **RULED 2026-08-22 — ALL-OR-NOTHING, WITH THE FULL REPORT.** Both halves are binding and the second is what
> makes the first tolerable: **every row is validated and every error named**, so the operator who is refused
> a thousand rows for two mistakes learns about both mistakes in one round trip rather than discovering the
> second after fixing the first. The report is not truncated at the first failure (`DEC-DOC-0003`).
>
> **This ruling reaches further than atomicity.** It closed `DEC-DOC-0004`'s open dependency: with no
> partially applied files, idempotency stops being a question about which rows survived and becomes a narrow
> guarantee about the ambiguous timeout. `DEC-DOC-0004` is
> [restated](#proposed-decisions) on those terms rather than left describing a problem that no longer
> exists.
>
> It also makes `OD-DOC-004`'s "unresolvable code is a row error" ruling consequential: one unknown
> department code fails the entire file. That is the intended behaviour — a file half of whose classifications
> are wrong is a file the operator wants back, not one they want half-applied.

**`OD-DOC-004` — How do `department`, `position` and `branch` resolve from file values, and may an import
create a missing one?**

Every employee requires all three (`BR-HR-0005`, `BR-HR-0006`, `ADR-023`). A file must name them somehow.

| Sub-question | Options |
|---|---|
| **By what** | By **code** (`FIN`, human-readable, unique per company) or by **identifier** (a GUID nobody types) |
| **Missing referent** | Reject the row, or **create** the department/position on the fly |

**Engineering recommendation (labeled):** resolve **by code**, and **never create**. Creating organizational
structure from a spreadsheet would let an import invent a department that no one approved, and the typo
`FINANCE`/`FINANCE ` becomes a permanent org unit. Rejection is recoverable; invention is not.

> **RULED 2026-08-22 — ADOPTED AS RECOMMENDED.** Department, position and branch resolve **by code, against
> existing records, under the importer's own authority** — a code the importer could not read is a code the
> importer cannot import against, so the resolution obeys the same scope every other read does. **An import
> never creates organizational structure.** An unresolvable code is a row error, and under `OD-DOC-003` a row
> error fails the file.
>
> The authority clause matters as much as the "never create" one: resolving by code *outside* the caller's
> scope would let a file discover which department codes exist in companies the caller cannot see, one
> rejection message at a time.

**`OD-DOC-005` — Are import and export separately-granted permissions?**

| Option | Consequence |
|---|---|
| **Reuse `HR.Employees.Create` / `.View`** | Anyone who may add one employee may add five thousand; anyone who may view an employee may extract the whole authorized set to a file |
| **Separate `HR.Employees.Import` / `.Export`** | Two more permissions to administer, and the sensitive capability can be granted to the few people who need it |

**Engineering recommendation (labeled): separate.** The precedent is `DEC-DEP-0025`'s reasoning —
`Deactivate` was separated from `Update` because it changes a materially different thing. Bulk creation and
bulk extraction are materially different from single-record work, and export in particular is the only
operation in the module that takes data *out* of it.

> **RULED 2026-08-22 — SEPARATE.** `HR.Employees.Import` and `HR.Employees.Export`, granted independently of
> each other and of `Create` and `View`.
>
> The sensitivity rationale is recorded in the `DEC-EMP-0030` lineage: that decision established that this
> module has fields and operations whose risk differs from the ordinary case, and that the difference is
> expressed in the authorization model rather than in guidance. **Export is the higher-risk half** — it is
> the only operation in the module that moves data outside the system's control, and once a file is
> downloaded no later permission change reaches it.

**`OD-DOC-006` — May `nationalId` leave the system in an export?**

`DEC-EMP-0030` already treats the national identifier as the module's sensitive field: it is on the employee
detail and deliberately **not** on the search list row. An export is closer to a list than to a detail, and
it persists outside the system's control the moment it is downloaded.

| Option | Consequence |
|---|---|
| **Never** | Exports cannot serve payroll or statutory filing, which are the usual reasons to export |
| **Always** | The most sensitive field in the module is one route away from a spreadsheet on a laptop |
| **Only under a distinct permission** | Two export shapes to specify and test; the capability exists where it is needed and nowhere else |

**This is a compliance decision, and the applicable law depends on the deployment.** It was raised, not
answered, and `DEC-DOC-0006`'s run record captures which column set left the system whatever the answer.

> **RULED 2026-08-22 — `nationalId` IS NEVER EXPORTED.** Unconditionally: there is no permission, parameter
> or caller for which the column appears. Not "excluded by default" — **absent from the contract**, which is
> the distinction `FP-006` draws between a field validated away and a field that does not exist.
>
> **Import keeps it optional**, so `DEC-DOC-0008`'s round-trip property survives with a concrete referent: an
> exported file re-imports because the one column exports omit is a column imports do not require. The
> property is now checkable rather than aspirational (`AC-DOC-0016`).
>
> **The run record makes the exclusion auditable.** `EmployeeExportRun.ColumnSet` records what actually left;
> if a future change ever added the column, the record would show it, which is a stronger guarantee than a
> rule nobody re-reads.

## Decisions taken during implementation

**`DEC-DOC-0014` — THE IMPORT ACCEPTS A RAW `text/csv` BODY. `multipart/form-data` IS NOT USED.**
*(Ruled 2026-08-22 during Phase 1. **Supersedes** `api-contracts.md` v1.0, which specified multipart.)*

A CSV file **is** a body. Multipart is browser-form machinery serving no API-first need, and it drags in
form-parsing limits and a test-harness apparatus for nothing.

**What the original decision was protecting is preserved.** `api-contracts.md` argued for multipart on the
ground that "a file cannot be a JSON field without base64", and that argument is correct — it is an argument
against putting the file in JSON, not an argument for multipart. It also promised that "the metadata half
stays strict": the form's non-file fields validated against a declared set, an unrecognised field answering
`400 request.invalid`. That property survives the change of transport, because the module already has
exactly that mechanism for **query parameters** — the strict allowlist `FR-DEP-0111`'s filter was blocked by
until the HR cleanup opened it. The strictness was never about the transport; it was about never silently
ignoring input.

**The mechanism.** `StrictCsvReader` is a **sibling** of `StrictRequestReader`, not a widening of it.
`ReadStrictJsonAsync` opens with `HasJsonContentType()`, and that line is its contract rather than a
precondition: everything it promises about strict binding is only true of a body it recognised. Teaching it a
second content type would make that first line a branch and its guarantees conditional. The sibling opens
with its own gate on `text/csv`, in the same register — an unrecognised content type is a **refusal**, never
a guess — and adds the one refusal JSON has no equivalent of: **bytes that are not valid UTF-8 are refused
rather than substituted**, because the permissive default replaces every undecodable byte with U+FFFD and
would import employees whose names are mojibake. A success that produced wrong data is worse than a refusal,
because nobody investigates it. A UTF-8 BOM is stripped rather than refused, so the file an export writes is
a file an import accepts (`DEC-DOC-0008`).

**What the reader deliberately does NOT do**: it does not know the column contract, count rows, or enforce a
size cap. Those belong to the handler, because the handler is what writes the run record — a bad header and
an exceeded cap are **`Refused` runs that consume the import key**, and a refusal that never reached the
handler could not have recorded one.

> **ONE QUESTION THIS RULING LEAVES OPEN, RECORDED RATHER THAN DECIDED.** With no multipart form there is no
> form field to carry `importKey`. Phase 1 does not answer it, because Phase 1 exposes no routes and the
> handler takes the key as a command parameter.
>
> **RULED 2026-08-22 — ADOPTED AS RECOMMENDED.** `importKey` travels as a **query parameter** under the
> existing strict allowlist. It is request data rather than ambient context, which is what separates it from
> `X-Company-Id`; and the allowlist already gives it the exact property multipart was promising — a declared
> set, with an unrecognised name answering `400 request.invalid`.
>
> **Implementation lands in Phase 2 with the routes.** Phase 1's handler takes the key as a command
> parameter, which is where it would come from under any answer, so nothing here depends on the mechanism.

**`DEC-DOC-0015` — EXPORT REQUIRES `HR.Employees.Export` **AND** `HR.Employees.View`.**
*(Ruled 2026-08-22 during Phase 1. An interpretation of `OD-DOC-005`, recorded because it is one.)*

`OD-DOC-005` ruled the two permissions "granted independently of each other and of `Create` and `View`". That
settles that neither **implies** the other. It does not, on its face, settle whether `Export` is *sufficient
alone*.

It is not. An export **is** a read of employee data, and it runs under the caller's own materialized employee
read scope — resolved by the same resolver every other employee read uses, which checks
`HR.Employees.View` as its first dimension. The read authority is therefore the **floor**, and the export
authority is the additional, deliberately-granted permission on top of it.

**The alternative was considered and rejected.** Making `Export` sufficient alone would mean a second scope
resolver keyed on a different permission — and under `DEC-POS-0018` that means a second, unforgeable scope
TYPE, because "the scope type is the permission" is how this module makes authorization unbypassable rather
than checkable. That is a real design, and it buys the ability to grant somebody bulk extraction of employee
data **without** the authority to look at one employee, which is not a capability anybody asked for and is
hard to argue is safer.

What the ruling **does** deliver either way is intact: anyone who may view an employee cannot thereby extract
the whole authorized set to a file. `X6` asserts exactly that — a caller holding `View` and not `Export` is
refused, and writes no run record.

> **RATIFIED 2026-08-22 — `Export` **AND** `View`, through the existing employee read scope.**
>
> **The additive `EmployeeExportScope` is DECLINED for V1.** The read scope already gates the DATA and
> `Export` gates the OPERATION; a third unforgeable type would add machinery without adding a boundary.
>
> **Revisit condition, stated so it is checkable rather than remembered:** only if an export ever reads MORE
> than a search can. It currently cannot, *by construction* — both run through one shared predicate over one
> criteria type, which is the same mechanism `AC-DOC-0014` rests on. The day that stops being true is the day
> this decision is worth reopening, and the shared predicate is what would have to change first.

**`OD-DOC-010` — WHAT DOES THE ROUND-TRIP PROPERTY ACTUALLY CLAIM?** *(Raised 2026-08-22 during Phase 1.
**Open.** Found by implementing the contract exactly as approved.)*

Three approved statements cannot all be true:

| # | Statement | Where |
|---|---|---|
| 1 | The export carries `employeeNumber`, `fullName`, `employmentDate`, `departmentCode`, `positionCode`, **`status`** | `api-contracts.md` |
| 2 | **`status` is not an import column** — creation produces `Active`, and an import cannot create a terminated employee | `AC-DOC-0002` |
| 3 | An exported file, edited and re-submitted, **is a legal import** | `DEC-DOC-0008`, `AC-DOC-0016` |

An import refuses **unknown** columns before reading a single row (`DEC-DOC-0002`), so a file the export
produces is refused for its `status` column — `HeaderColumnUnknown`. The package reasons about the round trip
**only for `nationalId`**, where the argument works: the one column exports omit is a column imports do not
require. Nothing was written about the one column exports **add**.

`X8` demonstrates the gap rather than describing it: the exported file is refused, and the same file with
`status` removed imports cleanly under a fresh employee number — so the format, the date rendering, the
quoting and the classification codes are all mutually compatible, and the failure is exactly one column wide.

| Option | Consequence |
|---|---|
| **Drop `status` from the export** | The round trip closes with no import change. An operator loses the ability to see, in the file, who is Active and who is Inactive — which is information the export is otherwise the only way to get in bulk |
| **Declare `status` an import column that is ACCEPTED AND IGNORED** | The round trip closes and `AC-DOC-0002` still holds in the sense that matters — creation still produces `Active` and no file can create a terminated employee. It costs the "absent by construction, not merely validated" property for one column |
| **Narrow the round-trip claim** to "an export re-imports **after the `status` column is removed**" | Honest and cheap, and turns a product property into a manual step the operator must know about. `AC-DOC-0016` would have to say so |

> **RULED 2026-08-22 — `status` BECOMES A RECOGNIZED OPTIONAL IMPORT COLUMN WITH ONE ACCEPTED VALUE.**
>
> Empty or `Active` passes. Any other value is a **row error** whose message names the remedy: *"An import
> creates only Active employees; remove the status value or the row."*
>
> **This is a fourth option, not one of the three above**, and the distinction is the whole ruling.
> Option 2 was *accepted and ignored*; this is **recognized and constrained**. Silently accepting and
> discarding a value is the behaviour this contract refuses everywhere else — an import that swallowed
> `status=Terminated` would report success while creating an Active employee, which is a lie the operator has
> no way to detect. Declaring the column keeps the allowlist strict **and** lets the file say what it means.
>
> None of the four owner rulings is reopened:
>
> * **Strict allowlist intact** — `status` is known, not silently ignored.
> * **`AC-DOC-0002` intact where it matters** — creation produces `Active`, and no file can create a
>   terminated employee. What changed is that the refusal is a named row error instead of a header rejection
>   that could not say why.
> * **A `status=Terminated` export honestly refuses.** Create-only cannot recreate a terminated person's
>   employment history — the dates, the reason, the branch they left from — and resurrecting them as new
>   Active hires would be worse than the refusal, because it would look like it worked.
> * **All-or-nothing and create-only unchanged.**
>
> ---
>
> **ONE CORRECTION TO THE RULING'S PREMISE, made during implementation and recorded rather than absorbed.**
>
> The ruling reasoned that "terminated are excluded (`DEC-DOC-0009`), so **every exported row is Active** and
> the file re-imports legally". The first clause is right; the second does not follow. **The employee
> search's default has always been `Active` AND `Inactive`** — both are current employment, and
> `DEC-DOC-0009` narrows only the terminated third of that. The export inherits that default.
>
> So a default export containing an **inactive** employee also refuses on re-import, with the same named row
> error. That is the ruling's own logic applied consistently, and it is the RIGHT answer for the same reason
> the terminated case is: creation produces `Active`, and an import that accepted `Inactive` and created an
> Active employee would report success while doing something else.
>
> **The accurate statement of what closes:** the round trip closes for an export whose rows are all `Active`
> — which is every export of active employees, and not every default export. `X9` asserts the inactive case
> so that fact is a property of the suite rather than a sentence in a report.
>
> If the owner wants a default export to round-trip unconditionally, the remaining lever is
> `DEC-DOC-0009`'s default status set — not this decision.

**`DEC-DOC-0016` — THE RUN-HISTORY WIRE SHAPE. THE SCOPE SNAPSHOT NEVER LEAVES THE TABLE.**
*(Ruled 2026-08-22 during Phase 2 planning. Raised because the package specifies `FR-DOC-0103` and
`FR-DOC-0202` in prose only — no response sample, no field list, no query contract.)*

Both run-history routes return: **who, when, the outcome, the counts**, and — for exports — **`ColumnSet`**.
`EmployeeExportRun.ScopeCompanyIds` and `ScopeBranchIds` are **omitted from the wire**.

**Why the omission, and why it is not fussiness.** Both routes are gated on `HR.Employees.View`, and the
listing is company-scoped — so caller A can read caller B's export run inside the same company. If the scope
snapshot shipped, A would read a **sorted list of company and branch identifiers that B's authority admitted
and A's may not**. That is exactly the disclosure this module collapses everywhere else: `EmployeeApiErrorMapper`
gives unauthorized, inactive, wrong-tenant and nonexistent companies one identical code, on the stated ground
that *"the difference is exactly the information they are not allowed to have"*. An audit surface that leaked
scope identifiers under a general read permission would undo that discipline **in the one place records are
designed to be read broadly** — which is the worst place to undo it.

**`FR-DOC-0202`'s stated point survives whole.** It asks for *"the record of what column set left"*, and
`ColumnSet` ships. The scope snapshot's value is **investigation-grade, not list-grade**: it stays durable in
the table, reachable by whoever investigates with database access or, later, a privileged surface.

**Exposing it on a privileged audit route later is ADDITIVE. Exposing it now and retracting later is not.**
That asymmetry is the whole argument for choosing omission while the question is still open.

**The half-measure was considered and declined.** Returning a *shape* instead of the values — "3 companies,
7 branches" — still discloses breadth information about another caller's authority, for no stated
requirement, and invites precisely the question omission answers.

**`FileName` and `ImportKey` DO ship.** Both are caller-supplied values echoed back to a company-scoped,
`View`-gated listing: they disclose nothing the listing does not already imply, and intra-company visibility
of who imported which file under which key is the audit trail working as intended rather than a leak.

*(All three fields are covered by this one record deliberately, so a future reader finds one decision rather
than three conventions.)*

**`DEC-DOC-0017` — THE IMPORTED FILE NAME TRAVELS AS AN OPTIONAL `X-File-Name` HEADER.**
*(Ruled 2026-08-22 during Phase 2. Raised because `DEC-DOC-0014` created the gap and nobody noticed.)*

`DEC-DOC-0006` has the run record store *"the file name"*, and `data-model.md` gives it an `nvarchar(260)`
column *"recorded for audit; never used to locate anything"*. Under the superseded multipart shape the file
part carried that name for free. **`DEC-DOC-0014` removed multipart and with it the only place a file name
could come from** — the identical gap `importKey` had, and this half was not noticed at the same time.

**Ruled: an optional `X-File-Name` request header, defaulting to `import.csv` when absent.** Recording a
caller-supplied name is right, because an audit column that always holds a constant is dead weight.

**The direction asymmetry is what makes this safe, and it is worth stating because it superficially resembles
the case `api-contracts.md` forbids.** That prohibition is about echoing caller input into a **response
header** — the export's `Content-Disposition` — which is a header-injection surface. This is the opposite
direction: caller input into a **stored audit field that is never reflected back**. Nothing renders it,
nothing routes on it, and the column's own definition says nothing locates anything with it.

**Three constraints, all read off that same definition:**

| # | Constraint | Why |
|---|---|---|
| 1 | **Path components stripped to the leaf.** A value like `..\..\x.csv` stores as `x.csv` | It is a NAME, not a location. Storing something shaped like a path invites a later reader to treat it as one |
| 2 | **Control characters REFUSED** as `400 request.invalid`, never stripped | A name containing them is a malformed request, and silently cleaning it would record a value the caller never sent |
| 3 | **Length capped to the column's own limit** (`EmployeeImportRun.FileNameMaximumLength`) | A value that passes validation must not then fail to persist — the rule `EmployeeNumber` already applies to itself |

**`OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` — DEFERRED to FP-010 (`OD-DOC-001`).**

Binary storage location, retention and erasure against the no-physical-delete convention, and whether V5
Document Management owns this capability are **OPEN-DEFERRED, not closed**. Their full statements, options
tables and consequences travelled unchanged to
[FP-010](../FP-010-hr-employee-documents/decisions-open.md#the-three-deferred-owner-decisions), where they
are that package's starting inventory.

`OD-DOC-007` is the one that gates `ADR-028`, and `ADR-028` is what gates FP-010. **Neither gates this
package**, which is the whole content of the `OD-DOC-001` ruling.
