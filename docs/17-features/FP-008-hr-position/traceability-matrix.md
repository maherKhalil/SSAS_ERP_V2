---
document_id: FP-008-RTM
title: HR Position — Traceability Matrix
status: Draft — Owner Decision Required
version: 0.1
---

# FP-008 — Traceability Matrix

## Source requirement coverage

| Source requirement | Body text in the catalog | Coverage in FP-008 |
|---|---|---|
| `REQ-HR-0200` Position Management | **None — title only** | `FR-POS-0201`–`FR-POS-0205`, `FR-POS-0210`–`FR-POS-0213`. **Derived**, not read from a requirement statement |
| `REQ-HR-0201` Job Grades | **None — title only** | `FR-POS-0206`–`FR-POS-0208` — **OPEN, `OD-POS-002`.** Exists only under options (i)–(iii) |
| `REQ-HR-0202` Salary Grade | **None — title only** | `FR-POS-0206`–`FR-POS-0209` — **OPEN, `OD-POS-002` and `OD-POS-004`.** Under option (iii) it is transferred to Payroll; under (i)/(ii) its money content is `OD-POS-004` |
| `REQ-HR-0006` Employee History | — | **Extended.** Position-change history realized (`DEC-POS-0008`) |
| `REQ-HR-0100`–`REQ-HR-0102` Department | — | Unchanged, subject to `OD-POS-003` |

**Three requirements are covered by inference from their titles.** That is stated here rather than hidden,
because a matrix that claims coverage of a requirement with no body text is claiming to have satisfied
something nobody wrote down. What FP-008 actually satisfies is `BR-HR-0006` plus a set of patterns; whether
that matches what `REQ-HR-0200` was meant to say is `OD-POS-002`'s question.

## Business rule coverage

| Rule | Decision | Acceptance criteria | Test scenarios | Status |
|---|---|---|---|---|
| `BR-HR-0006` — one active position | `DEC-POS-0009`, `DEC-POS-0012`, `DEC-POS-0021` | AC-POS-0030, AC-POS-0038, AC-POS-0049 | TS-POS-0034, TS-POS-0042, TS-POS-0057 | **OPEN — `OD-POS-001` and `OD-POS-005`.** Realizable for new employees; existing rows undecided, and the meaning of *active* undecided |
| `BR-HR-0007` — no self-management | `DEC-POS-0017` | AC-POS-0063 | TS-POS-0065 | **OPEN — transferred.** Unchanged from `DEC-DEP-0014` reading (iii) unless `OD-POS-006` brings a position hierarchy into scope |
| `BR-HR-0005` — one department per employee | `DEC-POS-0004` | AC-POS-0035 | TS-POS-0039 | **Untouched.** FP-007 realized it; FP-008 does not weaken it, subject to `OD-POS-003` |
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
| `BR-HR-0006` enforcement, including for pre-FP-008 employees | `DEC-EMP-0018`, `DEC-DEP-0020`, `BRULE-EMP-0026` | `DEC-POS-0009` | AC-POS-0038, AC-POS-0039, AC-POS-0040 | TS-POS-0042, TS-POS-0043 | **OPEN — `OD-POS-001`** |
| Position ownership decided **explicitly, not by copying** | `ADR-026` deferred obligations | `DEC-POS-0001` | AC-POS-0056 | TS-POS-0048 | **Proposed** — reasoning stated independently; awaits ratification |
| `BR-HR-0006` follows the retroactive-rule process | `ADR-026` decision 9 | `DEC-POS-0009` | AC-POS-0039 | TS-POS-0042 | **Process honoured** — the strategy is recorded (as open) before any migration is authored |
| Position must not be a placeholder on Employee | `DEC-EMP-0018`, `DEC-DEP-0020` | `DEC-POS-0008`, `DEC-POS-0010` | AC-POS-0031, AC-POS-0032 | TS-POS-0035, TS-POS-0036 | Realizable — `PositionId` is real and immutable outside the sanctioned channel |
| Position history, on the terms department history was reversed to | `DEC-DEP-0016` amendment | `DEC-POS-0008` | AC-POS-0032, AC-POS-0033, AC-POS-0037 | TS-POS-0036, TS-POS-0037, TS-POS-0041 | **Realized from the outset** — not re-raised as an owner decision, because the identical argument was already decided |
| `BR-HR-0007` personal reporting line | `DEC-EMP-0031`, `DEC-DEP-0014` | `DEC-POS-0017` | AC-POS-0063 | TS-POS-0065 | **OPEN — `OD-POS-006`**; transferred onward if deferred |
| Direct manager/incumbent FK only with a cycle-aware copy engine | `ADR-026` decision 7, `DEC-DEP-0022` | `DEC-POS-0002` | AC-POS-0006, AC-POS-0055 | TS-POS-0008, TS-POS-0044 | **Honoured** — no employee reference introduced |

## Obligations FP-008 transfers onward

| Obligation | To | Terms |
|---|---|---|
| An employee's actual pay | The Payroll module | A Salary Grade is a band on a job; individual compensation is Payroll's. No placeholder column introduced (`DEC-POS-0023`) |
| Salary-range **enforcement** | The Payroll module | Only if `OD-POS-004` selects the money reading. FP-008 has nothing to enforce against, so the enforcement cannot be claimed here (`ADR-026` d.10) |
| `REQ-HR-0202` in full | The Payroll module | Only under `OD-POS-002` option (iii), on exactly the terms FP-006 used for Department |
| `BR-HR-0007` personal reporting line | The package introducing an employee reporting line — **which no current requirement asks for** | Unchanged from `DEC-DEP-0014`, unless `OD-POS-006` brings it here |
| Position reporting **history** | The package introducing a position hierarchy | If `OD-POS-006` defers, the period between FP-008 and that package carries no reporting context (`DEC-POS-0017`) |
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

- **Every acceptance criterion** `AC-POS-0001` … `AC-POS-0064` is referenced by at least one test scenario.
- **Every test scenario** `TS-POS-0001` … `TS-POS-0066` names at least one acceptance criterion.
- **Every functional requirement** `FR-POS-0201` … `FR-POS-0213` is covered by at least one acceptance
  criterion.
- **Every business rule** `BRULE-POS-0001` … `BRULE-POS-0022` is either covered above or is a definitional
  statement supporting one that is.

## What this matrix does not claim

`BR-HR-0006` is recorded as **OPEN**, not as covered. FP-008 cannot claim to satisfy it until `OD-POS-001`
answers what happens to employees that already exist and `OD-POS-005` answers what the word *active* means.
`BR-HR-0007` is recorded as **OPEN and transferred**, unchanged. `REQ-HR-0201` and `REQ-HR-0202` are recorded
as **OPEN**, because whether they describe one aggregate, two, or none of FP-008's is `OD-POS-002`.

Stating otherwise would be the exact failure this matrix exists to prevent — the one `ADR-026` decision 10
named when it wrote that *where a rule cannot be enforced, the honest record is that it is open.*
