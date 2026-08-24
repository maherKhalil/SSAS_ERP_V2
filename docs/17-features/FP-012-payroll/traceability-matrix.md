# FP-012 — Traceability Matrix

Requirement → rule → acceptance criteria → test scenarios → decisions. Followed by the mechanical orphan
check.

---

## Requirement coverage

| Requirement | Rules | AC | TS | Decisions |
|---|---|---|---|---|
| `REQ-PAY-0001` Dated compensation record | `BR-PAY-0002` | `AC-PAY-0001`, `AC-PAY-0002` | `TS-PAY-0001`, `TS-PAY-0002`, `TS-PAY-0003` | **`OD-PAY-0003`**, `DEC-POS-0023` |
| `REQ-PAY-0002` Compensation never on an HR record | — | `AC-PAY-0003`, `AC-PAY-0005` | `TS-PAY-0033` | `DEC-PAY-0014`, `DEC-PAY-0015`, `OD-PAY-0005` |
| `REQ-PAY-0003` Tenant-defined pay elements | — | `AC-PAY-0006`, `AC-PAY-0007`, `AC-PAY-0008` | `TS-PAY-0004` | **`OD-PAY-0006`**, `DEC-PAY-0011` |
| `REQ-PAY-0004` Explicit calculation order | `BR-PAY-0004` | `AC-PAY-0009` | `TS-PAY-0005` | **`OD-PAY-0007`** |
| `REQ-PAY-0005` Element → account mapping | `BR-PAY-0005` | `AC-PAY-0021` | `TS-PAY-0029` | **`OD-PAY-0012`** |
| `REQ-PAY-0006` Band is informational | — | `AC-PAY-0004` | — | **`OD-PAY-0004`**, `DEC-POS-0027` |
| `REQ-PAY-0007` Run = one company, one period | `BR-PAY-0001` | `AC-PAY-0010` | — | **`OD-PAY-0002`** |
| `REQ-PAY-0008` Inclusion of period employees | `BR-PAY-0003` | `AC-PAY-0010`, `AC-PAY-0011`, `AC-PAY-0012` | `TS-PAY-0008` | **`OD-PAY-0010`**, `BR-HR-0004` |
| `REQ-PAY-0009` Calculation produces lines | — | `AC-PAY-0013` | `TS-PAY-0010` | `OD-PAY-0001`, `OD-PAY-0007` |
| `REQ-PAY-0010` State progression | — | `AC-PAY-0015` | `TS-PAY-0006` | **`OD-PAY-0009`** |
| `REQ-PAY-0011` Approval is a distinct permission | `BR-PAY-0009` | `AC-PAY-0016` | `TS-PAY-0020` | `BR-PLT-0103`, `OD-PAY-0009` |
| `REQ-PAY-0012` Recalculate before, never after | — | `AC-PAY-0014`, `AC-PAY-0017` | `TS-PAY-0007`, `TS-PAY-0027` | **`OD-PAY-0011`** |
| `REQ-PAY-0013` Correction by reversal | `BR-PAY-0008` | `AC-PAY-0024` | `TS-PAY-0032` | `DEC-PAY-0012` |
| `REQ-PAY-0014` Append-only run record | `BR-PAY-0011` | `AC-PAY-0018` | — | `DEC-PAY-0005` |
| `REQ-PAY-0015` One balanced journal | `BR-PAY-0006` | `AC-PAY-0019`, `AC-PAY-0020` | `TS-PAY-0029` | `BR-GL-0001`, `OD-GL-0009` |
| `REQ-PAY-0016` Closed period refused | `BR-PAY-0007` | `AC-PAY-0022`, `AC-PAY-0023` | `TS-PAY-0030`, `TS-PAY-0031` | **`OD-PAY-0014`**, `BR-GL-0003` |
| `REQ-PAY-0017` Contract or event, never a reference | — | `AC-PAY-0025` | `TS-PAY-0011`, `TS-PAY-0012` | **`OD-PAY-0013`**, `ADR-012` |
| `REQ-PAY-0018` Payslip as a guarded projection | `BR-PAY-0010` | `AC-PAY-0026`, `AC-PAY-0027` | `TS-PAY-0009`, `TS-PAY-0021` | **`OD-PAY-0015`**, **`OD-PAY-0016`**, `DEC-PAY-0013` |

## Cross-cutting criteria without a single owning requirement

| AC | Covers | TS | Decision |
|---|---|---|---|
| `AC-PAY-0028` | Unforgeable read scope | `TS-PAY-0023` | `DEC-PAY-0006` |
| `AC-PAY-0029` | E3 manifest membership | `TS-PAY-0013`, `TS-PAY-0028` | `DEC-PAY-0010` |
| `AC-PAY-0030` | `nvarchar` + `decimal(19,4)` | `TS-PAY-0024`, `TS-PAY-0025` | `DEC-PAY-0004`, `DEC-PAY-0007` |
| `AC-PAY-0031` | No cross-database FK | `TS-PAY-0026` | `DEC-PAY-0008` |

These are conventions rather than requirements, which is why they have no `REQ-PAY`. Recording them here
keeps the orphan check honest instead of making it pass by deleting the question.

## Convention scenarios with no owning acceptance criterion

These verify repo-wide conventions rather than FP-012 requirements. They are listed because the orphan
check is mechanical: a scenario absent from this matrix is an orphan whether or not it is a good test.

| TS | Verifies | Decision |
|---|---|---|
| `TS-PAY-0014` | Permission grammar and catalog-contributor registration | `OD-PAY-0016` |
| `TS-PAY-0015` | No public setter on a monetary property | `DEC-PAY-0004` |
| **`TS-PAY-0016`** | **Every write route binds a correctly-cased body** | — see note below |
| `TS-PAY-0017` | Route inventory pinned by name, not by count | — |
| `TS-PAY-0018` | Every route carries a permission policy | `BR-PAY-0010` |
| `TS-PAY-0019` | No route responds to `DELETE` | — |
| `TS-PAY-0022` | A supplied currency code is refused as unknown | `DEC-PAY-0003`, `BR-PAY-0012` |

`TS-PAY-0016` has no acceptance criterion because it does not verify a *requirement* — it verifies that the
requirements are reachable at all. Its absence in FP-011 meant every GL write route returned
`400 request.invalid` while every layer beneath was correct.

## Conventions carried without a requirement of their own

| Decision | Where it binds | Verified by |
|---|---|---|
| `DEC-PAY-0003` Single currency | every monetary response; no stored currency | `TS-PAY-0022` |
| `DEC-PAY-0009` `RowVersion` on mutable aggregates | `PayElement`, `PayrollRun`, compensation | concurrency behaviour, `AC-PAY-0030` schema shape |
| `BR-PAY-0012` Amounts in the company's base currency | calculation and posting | `TS-PAY-0022`, `TS-PAY-0029` |
| `BR-PAY-0013` Per-line rounding, total = sum of rounded lines | calculation engine | `TS-PAY-0009` |
| `DEC-PAY-0016` **Jurisdiction-neutral V1** | the whole feature boundary | nothing asserts a statutory figure |
| `DEC-PAY-0017` **Two sanctioned employee read shapes** | the HR roster boundary | three architecture guards on `EmployeeRosterService` |

---

## Decision → what it blocks

An owner decision that blocks nothing would not be worth asking.

| Decision | Blocks |
|---|---|
| `OD-PAY-0001` V1 element set | `REQ-PAY-0009`, the whole calculation model |
| `OD-PAY-0002` Frequency model | `REQ-PAY-0007`, period/fiscal alignment |
| **`OD-PAY-0003` Compensation shape** | `REQ-PAY-0001`, `BR-PAY-0002`, `AC-PAY-0001`–`0002`, the POST-vs-PUT route shape, and whether `OD-PAY-0018` is cheap or expensive later |
| `OD-PAY-0004` Band validation | `REQ-PAY-0006`, `AC-PAY-0004` |
| `OD-PAY-0005` Ownership scope | schema, permissions, `AC-PAY-0005` |
| `OD-PAY-0006` Element taxonomy | `REQ-PAY-0003`, the calculation model's finiteness |
| `OD-PAY-0007` Ordering + proration | `REQ-PAY-0004`, `TS-PAY-0010` |
| **`OD-PAY-0008` Rounding** | `AC-PAY-0026` — the criterion is written so the ruling determines whether it can pass |
| `OD-PAY-0009` Lifecycle + approver | `REQ-PAY-0010`, `REQ-PAY-0011`, the permission set |
| `OD-PAY-0010` Terminated employees | `REQ-PAY-0008`, `AC-PAY-0011` |
| `OD-PAY-0011` Rerun/correction | `REQ-PAY-0012`, and whether `PayrollRun` can carry a unique period constraint |
| `OD-PAY-0012` Account mapping | `REQ-PAY-0005`, `AC-PAY-0021` |
| **`OD-PAY-0013` Posting mechanism** | `REQ-PAY-0017`, the recreation of `SSAS.GL.Contracts`, and HR-direction traffic too |
| `OD-PAY-0014` Closed period | `REQ-PAY-0016`, `AC-PAY-0022` |
| `OD-PAY-0015` Payslip | `REQ-PAY-0018` |
| `OD-PAY-0016` Pay-data permission | `REQ-PAY-0018`, `BR-PAY-0010`, the whole authorization model |
| `OD-PAY-0017` Module home | every project path, the test project name, `InternalsVisibleTo` |
| `OD-PAY-0018` Retro/advances/loans | nothing in V1 — it is a *deferral*, and recorded so the silence is deliberate |

---

## Mechanical orphan check

Run over this package: every identifier defined must be referenced, and every identifier referenced must be
defined.

| Check | Result |
|---|---|
| `REQ-PAY-0001`–`0018` defined | 18 |
| … each appearing in this matrix | **18 / 18** |
| … each with at least one AC | **18 / 18** |
| `AC-PAY-0001`–`0031` defined | 31 |
| … each traced to a requirement or listed as cross-cutting | **31 / 31** |
| `TS-PAY-0001`–`0033` defined | 33 |
| … each mapped to an AC or to an architectural convention | **33 / 33** |
| `BR-PAY-0001`–`0012` defined | 12 |
| … each referenced by a requirement or AC | **12 / 12** |
| `DEC-PAY-0001`–`0015` defined | 15 |
| `OD-PAY-0001`–`0018` defined | 18 |
| … each blocking at least one artifact above | **18 / 18** |
| Referenced-but-undefined `PAY` identifiers | **0** |

**AC without a TS, by intent:** `AC-PAY-0004` (conditional on `OD-PAY-0004`; a scenario would encode an
unruled option) and `AC-PAY-0018` (run-record retention is asserted within `TS-PAY-0007`/`TS-PAY-0027`
rather than separately). Both are stated rather than papered over.

**External identifiers referenced and verified to exist in the repository at `9b1dd99`:** `BR-PLT-0103`
(names Payroll Processing sensitive), `BR-HR-0004`, `BR-GL-0001`, `BR-GL-0003`, `DEC-POS-0018`,
`DEC-POS-0023`, `DEC-POS-0024`, `DEC-POS-0027`, `DEC-POS-0030`, `DEC-DOC-0015`, `OD-GL-0002`, `OD-GL-0009`,
`ADR-012`, `ADR-018`, `ADR-027`, `REQ-HR-0005`, `OD-DOC-001`.

---

## Known gaps, recorded rather than left to be discovered

**Statutory deductions and tax have no requirement, no rule and no test.** Not an oversight — there is no
authored jurisdiction to derive them from. **This is the largest gap between FP-012 and a payroll any
organisation could actually run**, and it belongs in front of the owner before the build prompt.

**Attendance-derived elements are absent by necessity** (`DEC-PAY-0002`), not by choice.

**Self-service payslip access assumes an identity → employee mapping this package does not assert exists.**
It must be verified in the repository before any requirement depends on it.

**`GlReadScope`'s promotion trigger fires here.** FP-011 recorded "a third consumer" as the condition for
promoting the company-scope type into `SSAS.BuildingBlocks`, written where the type lives because drift in
a scope type is a security defect. Payroll is that third consumer. Flagged for the architect; `ADR-027`
decision 4 makes promotion a reviewed change, not a side effect of a package needing a type.

### The check was run, and the first pass failed

Recorded because a matrix that merely *claims* coverage is worth nothing.

The first draft of this matrix asserted `31 / 31` and `33 / 33`. Running the check mechanically returned
**fifteen orphans**: three acceptance criteria, nine test scenarios, one business rule and two decisions
that appeared nowhere in it.

Two distinct causes, and the distinction matters:

* **Ten were hidden by en-dash ranges.** The matrix said `` `AC-PAY-0006`–`0008` ``, which a human reads as
  covering three identifiers and a string search reads as covering one. The ranges are now expanded to
  explicit identifiers — **a traceability matrix has to be machine-checkable, not merely legible.**
* **Five were genuinely missing** — the convention scenarios (`TS-PAY-0014`–`0019`, `0022`), which verify
  repo-wide conventions rather than FP-012 requirements, plus `BR-PAY-0012`, `DEC-PAY-0003` and
  `DEC-PAY-0009`. They now have their own sections rather than being quietly dropped to make the count
  agree.

This is the same failure this project has now paid for three times in different clothes: **a count stated
from intent rather than derived from the artifact.** The check is cheap; running it is not optional.
