---
document_id: FP-008-RTM
title: HR Position — Traceability Matrix
status: Approved for Implementation
version: 1.0
---

# FP-008 — Traceability Matrix

## Source requirement coverage

| Source requirement | Body text in the catalog | Coverage in FP-008 |
|---|---|---|
| `REQ-HR-0200` Position Management | **None — title only** | `FR-POS-0201`–`FR-POS-0205`, `FR-POS-0210`–`FR-POS-0213`. **Derived**, not read from a requirement statement |
| `REQ-HR-0201` Job Grades | **None — title only** | `FR-POS-0206`–`FR-POS-0208` — **realized** as a first-class aggregate (`OD-POS-002`) |
| `REQ-HR-0202` Salary Grade | **None — title only** | `FR-POS-0206`–`FR-POS-0209` — **realized** as a first-class aggregate carrying informational bands (`OD-POS-002`, `OD-POS-004`). Range **enforcement** is transferred to Payroll, not claimed |
| `REQ-HR-0006` Employee History | — | **Extended.** Position-change history realized (`DEC-POS-0008`) |
| `REQ-HR-0100`–`REQ-HR-0102` Department | — | **Unchanged.** `OD-POS-003` ruled Position independent of Department |

**Three requirements are covered by inference from their titles, and the inference is now owner-ratified.**
That is stated here rather than hidden, because a matrix that claims coverage of a requirement with no body
text is claiming to have satisfied something nobody wrote down. What FP-008 satisfies from the written record
is `BR-HR-0006`; what it satisfies for `REQ-HR-0200`–`REQ-HR-0202` is the reading the owner confirmed in
`OD-POS-002` — three aggregates, one per line. That is a ruling on what the titles mean, not a discovery of
what they said.

## Business rule coverage

| Rule | Decision | Acceptance criteria | Test scenarios | Status |
|---|---|---|---|---|
| `BR-HR-0006` — one active position | `DEC-POS-0009`, `DEC-POS-0012`, `DEC-POS-0021`, `DEC-POS-0026` | AC-POS-0030, AC-POS-0038, AC-POS-0039, AC-POS-0049 | TS-POS-0034, TS-POS-0042, TS-POS-0043, TS-POS-0057 | **Realized.** `OD-POS-001` ruled `NOT NULL` from day one with a fail-loud precondition, and `OD-POS-005` ruled *active* to qualify the assignment. No cohort is exempt |
| `BR-HR-0007` — no self-management | `DEC-POS-0017` | AC-POS-0063 | TS-POS-0065 | **OPEN — transferred.** `OD-POS-006` deferred the position hierarchy, so `DEC-DEP-0014` reading (iii) stands and the personal reporting line still has no field |
| `BR-HR-0005` — one department per employee | `DEC-POS-0004` | AC-POS-0035 | TS-POS-0039 | **Untouched.** FP-007 realized it; `OD-POS-003` ruled Position independent, so nothing here moves it |
| `BR-PLT-0002` — company isolation | `DEC-POS-0001`, `DEC-POS-0003` | AC-POS-0007, AC-POS-0011 | TS-POS-0009, TS-POS-0013 | Realizable |
| `BR-PLT-0003` — soft delete | `DEC-POS-0011` | AC-POS-0027 | TS-POS-0031 | Realizable |
| `BR-PLT-0004` — audit trail | `DEC-POS-0003` | AC-POS-0001, AC-POS-0024 | TS-POS-0001, TS-POS-0027 | Realizable |
| `BR-PLT-0006` — numbering sequences | `DEC-POS-0024` | — | — | **Deferred**, unchanged |
| `BR-PLT-0013` — branch owns transactions | `DEC-POS-0001` | AC-POS-0056, AC-POS-0057 | TS-POS-0048, TS-POS-0049 | **Respected by exclusion**, guarded |

## Obligations carried into FP-008

Every obligation FP-006 and FP-007 transferred here is traceable to a first-class acceptance criterion **and**
a first-class test scenario, or is explicitly recorded as open pending an owner decision. **None exists only
in prose.**

| Carried obligation | Origin | Decision | AC | TS | Status |
|---|---|---|---|---|---|
| `BR-HR-0006` enforcement, including for pre-FP-008 employees | `DEC-EMP-0018`, `DEC-DEP-0020`, `BRULE-EMP-0026` | `DEC-POS-0009`, `DEC-POS-0026` | AC-POS-0038, AC-POS-0039, AC-POS-0040, AC-POS-0065 | TS-POS-0042, TS-POS-0043, TS-POS-0067 | **Discharged.** No pre-FP-008 employees exist, and the migration asserts it rather than assuming it |
| Position ownership decided **explicitly, not by copying** | `ADR-026` deferred obligations | `DEC-POS-0001` | AC-POS-0056 | TS-POS-0048 | **Discharged.** Reasoned independently from `ADR-024`, and ratified as an explicit decision on 2026-08-21. `ADR-026` revision 1.1 should record the discharge, since the obligation lives in that ADR |
| `BR-HR-0006` follows the retroactive-rule process | `ADR-026` decision 9 | `DEC-POS-0009`, `DEC-POS-0026` | AC-POS-0039, AC-POS-0040 | TS-POS-0043 | **Discharged.** The strategy was recorded before any migration was authored, and it produced the finding that no backfill is needed — plus the safeguard that verifies it |
| Position must not be a placeholder on Employee | `DEC-EMP-0018`, `DEC-DEP-0020` | `DEC-POS-0008`, `DEC-POS-0010` | AC-POS-0031, AC-POS-0032 | TS-POS-0035, TS-POS-0036 | Realizable — `PositionId` is real and immutable outside the sanctioned channel |
| Position history, on the terms department history was reversed to | `DEC-DEP-0016` amendment | `DEC-POS-0008` | AC-POS-0032, AC-POS-0033, AC-POS-0037 | TS-POS-0036, TS-POS-0037, TS-POS-0041 | **Realized from the outset** — not re-raised as an owner decision, because the identical argument was already decided |
| `BR-HR-0007` personal reporting line | `DEC-EMP-0031`, `DEC-DEP-0014` | `DEC-POS-0017` | AC-POS-0063 | TS-POS-0065 | **OPEN — deferred by `OD-POS-006`**, transferred onward unchanged |
| Direct manager/incumbent FK only with a cycle-aware copy engine | `ADR-026` decision 7, `DEC-DEP-0022` | `DEC-POS-0002` | AC-POS-0006, AC-POS-0055 | TS-POS-0008, TS-POS-0044 | **Honoured** — no employee reference introduced |

## Obligations FP-008 transfers onward

| Obligation | To | Terms |
|---|---|---|
| An employee's actual pay | The Payroll module | A Salary Grade is a band on a job; individual compensation is Payroll's. No placeholder column introduced (`DEC-POS-0023`) |
| Salary-range **enforcement** | The Payroll module | `OD-POS-004` chose informational bands. FP-008 stores no value to enforce against, so the enforcement is recorded as transferred and is not claimed (`ADR-026` d.10) |
| `BR-HR-0007` personal reporting line | The package introducing an employee reporting line — **which no current requirement asks for** | Unchanged from `DEC-DEP-0014`; `OD-POS-006` did not bring it here |
| Position reporting **history** | The package introducing a position hierarchy | `OD-POS-006` deferred it, so the period between FP-008 and that package carries no reporting context (`DEC-POS-0017`) |
| Headcount and establishment control | Any package that requires it | No placeholder introduced (`DEC-POS-0025`) |
| Money representation for General Ledger | `ADR-027` | GL inherits whatever `DEC-POS-0016` sets, which is why it is escalated to an ADR rather than settled in a feature package |

## Non-functional requirement coverage

| NFR | Subject | Where realized | AC | TS |
|---|---|---|---|---|
| `NFR-POS-0301` | Scoped reads are indexed | `IX_Positions_TenantId_CompanyId_Status` ([`data-model.md`](data-model.md)) | AC-POS-0051 | TS-POS-0059 |
| `NFR-POS-0302` | Optimistic concurrency | `RowVersion` transport ([`api-contracts.md`](api-contracts.md)) | AC-POS-0047, AC-POS-0048 | TS-POS-0056, TS-POS-0057 |
| `NFR-POS-0303` | Authorization resolves live | `PositionReadScope` ([`authorization-model.md`](authorization-model.md)) | AC-POS-0009, AC-POS-0010, AC-POS-0046 | TS-POS-0011, TS-POS-0012, TS-POS-0055 |
| `NFR-POS-0304` | Cutover coverage derived, not declared | `DEC-POS-0022` | AC-POS-0052, AC-POS-0053, AC-POS-0054 | TS-POS-0045, TS-POS-0046, TS-POS-0047 |
| `NFR-POS-0305` | No foreign-key cycle | `DEC-POS-0002`, `RISK-POS-001` | AC-POS-0006, AC-POS-0055 | TS-POS-0008, TS-POS-0044 |
| `NFR-POS-0306` | `BR-HR-0006` cardinality unrepresentable | `DEC-POS-0021` | AC-POS-0049 | TS-POS-0057 |

## Architecture decision coverage

| Concern | Authority | AC | TS |
|---|---|---|---|
| Ownership classification is explicit | Principle 11, `ADR-026` d.1–d.2, `DEC-POS-0001` | AC-POS-0056, AC-POS-0057 | TS-POS-0048, TS-POS-0049 |
| Ownership-adjacent fields change only through sanctioned channels | `ADR-026` d.6 | AC-POS-0031, AC-POS-0032 | TS-POS-0035, TS-POS-0036 |
| Association shape and the cutover cycle | `ADR-026` d.7, `ADR-020` | AC-POS-0006, AC-POS-0055 | TS-POS-0008, TS-POS-0044 |
| Org structure is not an authorization dimension | `ADR-026` d.8, `ADR-025` d.8 | AC-POS-0044, AC-POS-0046 | TS-POS-0053, TS-POS-0055 |
| Retroactive rules need a recorded strategy | `ADR-026` d.9 | AC-POS-0038, AC-POS-0039 | TS-POS-0042 |
| A rule with no field is recorded open, not satisfied | `ADR-026` d.10 | AC-POS-0063 | TS-POS-0065 |
| Module isolation | `ADR-012` | AC-POS-0060 | TS-POS-0062 |
| Company scope resolved live | `ADR-025` d.8, d.10 | AC-POS-0009, AC-POS-0010 | TS-POS-0011, TS-POS-0012 |
| Permissions actually reachable | FP-006P regression | AC-POS-0043 | TS-POS-0052 |
| Routes reachable in production, not only in tests | FP-007 Phase 4 regression | AC-POS-0058 | TS-POS-0060 |
| Money representation | **`ADR-027` (drafted, conditional)** | AC-POS-0018, AC-POS-0022 | TS-POS-0020, TS-POS-0025 |

## Orphan check

Re-verified after the 2026-08-21 rulings, mechanically rather than by inspection:

- **Every acceptance criterion** `AC-POS-0001` … `AC-POS-0065` is referenced by at least one test scenario.
  The range is contiguous — no number is skipped.
- **Every test scenario** `TS-POS-0001` … `TS-POS-0067` names at least one acceptance criterion. The range is
  contiguous. `TS-POS-0067` is grouped with the migration scenarios rather than placed last, because subject
  grouping is more useful to a reader than numeric position.
- **Every acceptance criterion and test scenario cited anywhere in this matrix is defined** in its own
  document. There are no dangling references in either direction.
- **Every functional requirement** `FR-POS-0201` … `FR-POS-0213` is covered by at least one acceptance
  criterion; **every non-functional requirement** `NFR-POS-0301` … `NFR-POS-0306` is covered above.
- **Every business rule** `BRULE-POS-0001` … `BRULE-POS-0022` is either covered above or is a definitional
  statement supporting one that is.

The rulings added `AC-POS-0065` (no synthetic residue) and `TS-POS-0043` / `TS-POS-0067`, and replaced the
three backfill criteria with the three precondition criteria. Nothing was left pointing at a removed
identifier.

## What this matrix does not claim

**`BR-HR-0007` remains OPEN and transferred.** `OD-POS-006` deferred the position hierarchy, so the rule still
has no field anywhere in the product to constrain, and its remainder passes to a package no current
requirement asks for. Recording it as covered because FP-008 shipped an org-structure aggregate would be the
exact failure this matrix exists to prevent — the one `ADR-026` decision 10 named when it wrote that *where a
rule cannot be enforced, the honest record is that it is open.*

**Salary-range enforcement is transferred, not realized.** `OD-POS-004` chose informational bands and FP-008
stores no value for them to constrain, so there is nothing here to enforce and nothing to claim.

**`BR-HR-0006` is recorded as realized in design, not in a database.** The rule becomes true when the
migration runs and `Employee.PositionId` is `NOT NULL` in a real catalog — and `DEC-POS-0026` exists because
the ruling that licenses it is an operational fact, which a migration must verify rather than trust. Until
`TS-POS-0043` is green against real SQL, this row records a design that satisfies the rule, not a system that
does.

---

## As built (FP-008 Phase 4)

> **`BR-HR-0006` IS NOW REALIZED IN A DATABASE, and the row above can be read in the past tense.**
> `TS-POS-0043` is green against real SQL Server: `AddEmployeePosition` refuses a database holding Employee
> rows with the `DEC-POS-0026` message, writes nothing, and re-evaluates on the next run. The four proofs
> live in `EmployeePositionMigrationSqlServerTests`.

| Added row | Where it is proven | Criteria |
|---|---|---|
| `FR-POS-0212` — read an employee's position history | `GetEmployeePositionHistoryQueryHandler`, route `GET /api/hr/employees/{id}/position-history`, inventory row | AC-POS-0068 |
| `DEC-POS-0034` — `employeeCount` semantics | `TS-POS-0070` | AC-POS-0066 |
| `DEC-POS-0035` — the currency seam | `TS-POS-0071` | AC-POS-0067, AC-POS-0022 |
| `DEC-POS-0030` — search over normalized columns | `TS-POS-0072`-adjacent search tests in `PositionApplicationSqlServerTests` and `DepartmentApplicationSqlServerTests` | — |
| `DEC-POS-0031` — the FP-007 search fix | `DepartmentApplicationSqlServerTests` search set | — |

**`FR-POS-0212` was missing from the implementation until Phase 4 and this matrix did not show it.** The
matrix maps requirements to tests, and a requirement with no test row reads as untested rather than as
unbuilt — the two are different failures and only one of them is visible here. Step 0's route reconciliation
found it by pairing handlers against routes, which is a check this document cannot perform. Recorded so the
next package's matrix is not trusted to catch the same shape of gap.
