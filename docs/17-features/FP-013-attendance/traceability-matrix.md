# FP-013 — Traceability matrix

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Requirement → business rule → acceptance criterion → test scenario → governing decision.

**Every count in this document was derived by a script over the package's own files, not typed from
memory.** FP-012 stated its entity count wrong **four times** — including once inside the very section
warning about miscounts — which is why counting is now mechanical here.

---

## Inventory, derived

| Space | Count | Numbering | Orphans |
|---|---|---|---|
| `REQ-ATT` | **25** | 0001–0025 contiguous | none |
| `BR-ATT` | **12** | 0001–0012 contiguous | none |
| `AC-ATT` | **45** | 0001–0045 contiguous | none |
| `TS-ATT` | **52** | 0001–0052 contiguous | none |
| `DEC-ATT` | **14** | 0001–0014 contiguous | none |
| `OD-ATT` | **16** | 0001–0016 contiguous | none |

"Orphan" means an identifier cited somewhere in the package that is not defined in its home file.

---

## The chain

| Requirement | Rules | Criteria | Tests | Decisions |
|---|---|---|---|---|
| `REQ-ATT-0001` calendar + weekend as data | `BR-ATT-0001` | `AC-ATT-0001` | `TS-ATT-0001` | `OD-ATT-0004`, `OD-ATT-0011` |
| `REQ-ATT-0002` holiday list | — | `AC-ATT-0002`, `AC-ATT-0004` | `TS-ATT-0002`, `TS-ATT-0004` | `OD-ATT-0004` |
| `REQ-ATT-0003` working-day count | `BR-ATT-0002` | `AC-ATT-0002`, `AC-ATT-0003`, `AC-ATT-0005` | `TS-ATT-0002`, `TS-ATT-0003` | `OD-ATT-0004` |
| `REQ-ATT-0004` record time worked | — | `AC-ATT-0006` | `TS-ATT-0040` | `OD-ATT-0003`, `OD-ATT-0011` |
| `REQ-ATT-0005` recorded within authority | — | `AC-ATT-0028`, `AC-ATT-0029`, `AC-ATT-0030` | `TS-ATT-0017`, `TS-ATT-0033`, `TS-ATT-0044` | `DEC-ATT-0008` |
| `REQ-ATT-0006` employment window | `BR-ATT-0004`, `BR-ATT-0005` | `AC-ATT-0007`, `AC-ATT-0008`, `AC-ATT-0009` | `TS-ATT-0005`, `TS-ATT-0006`, `TS-ATT-0020` | `DEC-ATT-0013` |
| `REQ-ATT-0007` overtime quantity | — | `AC-ATT-0011` | `TS-ATT-0007` | `OD-ATT-0008` |
| `REQ-ATT-0008` paid/unpaid absence | — | `AC-ATT-0039` | `TS-ATT-0046` | `OD-ATT-0008` |
| `REQ-ATT-0009` no money | `BR-ATT-0008` | `AC-ATT-0010` | `TS-ATT-0023`, `TS-ATT-0027` | `DEC-ATT-0004` |
| `REQ-ATT-0010` leave type catalog | `BR-ATT-0009`, `BR-ATT-0010` | `AC-ATT-0021`, `AC-ATT-0022` | `TS-ATT-0014`, `TS-ATT-0015`, `TS-ATT-0019` | `OD-ATT-0005` |
| `REQ-ATT-0011` balance per type | — | `AC-ATT-0040` | `TS-ATT-0047` | `OD-ATT-0006` |
| `REQ-ATT-0012` submit request | — | `AC-ATT-0041` | `TS-ATT-0048` | `OD-ATT-0007` |
| `REQ-ATT-0013` consumes working days | `BR-ATT-0002`, `BR-ATT-0003` | `AC-ATT-0016`, `AC-ATT-0017`, `AC-ATT-0019` | `TS-ATT-0010`, `TS-ATT-0012` | `OD-ATT-0004` |
| `REQ-ATT-0014` approved by a non-requester | `BR-ATT-0007` | `AC-ATT-0020` | `TS-ATT-0013`, `TS-ATT-0022` | `OD-ATT-0007` |
| `REQ-ATT-0015` approval decrements | — | `AC-ATT-0018` | `TS-ATT-0011` | `OD-ATT-0006` |
| `REQ-ATT-0016` cancellation | — | `AC-ATT-0042` | `TS-ATT-0049` | `OD-ATT-0012` |
| `REQ-ATT-0017` leave employment window | `BR-ATT-0004`, `BR-ATT-0005` | `AC-ATT-0043` | `TS-ATT-0050` | `DEC-ATT-0013` |
| `REQ-ATT-0018` periods and close | `BR-ATT-0006` | `AC-ATT-0012`, `AC-ATT-0013` | `TS-ATT-0008`, `TS-ATT-0009`, `TS-ATT-0021` | `OD-ATT-0010`, `OD-ATT-0012` |
| `REQ-ATT-0019` corrections are dated | `BR-ATT-0006` | `AC-ATT-0014`, `AC-ATT-0015` | `TS-ATT-0024`, `TS-ATT-0028`, `TS-ATT-0029` | `DEC-ATT-0009`, `OD-ATT-0012` |
| `REQ-ATT-0020` summary contract | `BR-ATT-0011` | `AC-ATT-0023`, `AC-ATT-0026`, `AC-ATT-0027` | `TS-ATT-0032`, `TS-ATT-0037`, `TS-ATT-0043` | `DEC-ATT-0002`, `OD-ATT-0009` |
| `REQ-ATT-0021` inspect the period | — | `AC-ATT-0024`, `AC-ATT-0025` | `TS-ATT-0041`, `TS-ATT-0042` | `OD-ATT-0010` |
| `REQ-ATT-0022` **Payroll follow-up** | — | `AC-ATT-0044` | `TS-ATT-0038`, `TS-ATT-0039`, `TS-ATT-0051` | `DEC-ATT-0001`, `DEC-ATT-0012` |
| `REQ-ATT-0023` self-service — **BLOCKED** | — | `AC-ATT-0032` | `TS-ATT-0036` | `OD-ATT-0013` |
| `REQ-ATT-0024` read within authority | `BR-ATT-0012` | `AC-ATT-0031` | `TS-ATT-0045` | `OD-ATT-0011`, `OD-ATT-0013` |
| `REQ-ATT-0025` leave type sensitivity | — | `AC-ATT-0045` | `TS-ATT-0052` | `OD-ATT-0013` |

**Every requirement has at least one criterion, and every criterion has at least one test.** Both verified by
script, not by reading.

## Criteria carrying no requirement

`AC-ATT-0033`, `AC-ATT-0034`, `AC-ATT-0035`, `AC-ATT-0036`, `AC-ATT-0037` and `AC-ATT-0038` trace to **platform rules rather than to a feature requirement** —
`nvarchar` (`ADR-018`), no cross-database FK (`ADR-022`), the E3 manifest, branch classification, the
migration tool, and the JSON binding rule. They are listed with `—` in the Req column deliberately: inventing
a requirement to give them a parent would make the matrix look tidier and mean less.

They map to `DEC-ATT-0005`, `DEC-ATT-0006`, `DEC-ATT-0007`, `DEC-ATT-0009`, `DEC-ATT-0010`, `DEC-ATT-0011`
and `DEC-ATT-0014`, and to `TS-ATT-0018`, `TS-ATT-0025`, `TS-ATT-0026`, `TS-ATT-0030`, `TS-ATT-0034` and
`TS-ATT-0035` — written out rather than given as ranges, for the reason the orphan-check section states.

---

## Decision impact — what each open ruling moves

| Decision | Moves |
|---|---|
| **`OD-ATT-0001` scope** | **the whole package** — 16, 20 or 25 requirements |
| **`OD-ATT-0002` identifier space** | every `REQ-ATT`/`BR-ATT` id here, and the prefix itself |
| `OD-ATT-0003` capture model | `REQ-ATT-0004`; the `AttendanceRecord` aggregate; the punch-sensitivity permission question |
| `OD-ATT-0004` calendar ownership | `REQ-ATT-0001`, `REQ-ATT-0002`, `REQ-ATT-0003`, `REQ-ATT-0013`; `WorkingCalendars` |
| `OD-ATT-0005` leave types | `REQ-ATT-0010`; `LeaveBehaviour` membership |
| `OD-ATT-0006` accrual | `REQ-ATT-0011`, `REQ-ATT-0015`; `LeaveBalances` becomes one table or two |
| `OD-ATT-0007` approval | `REQ-ATT-0012`, `REQ-ATT-0014`; three unwritten rules |
| `OD-ATT-0008` overtime | `REQ-ATT-0007`, `REQ-ATT-0008`; the contract's tier shape |
| `OD-ATT-0009` contract shape | `REQ-ATT-0020`; period alignment |
| `OD-ATT-0010` close discipline | `REQ-ATT-0021`; whether Payroll can read an open period |
| **`OD-ATT-0011` branch** | every table, the read scope, the calendar, the contract grain |
| **`OD-ATT-0012` corrections** | `REQ-ATT-0019`; **the aggregate split**, and the attendance-record unique index |
| `OD-ATT-0013` read permissions | `REQ-ATT-0023`, `REQ-ATT-0024`, `REQ-ATT-0025`; the self-service blocker |
| `OD-ATT-0014` module home | project layout; moves with `OD-ATT-0002`(1) |
| `OD-ATT-0015` `OD-PAY-0007` reopen | nothing in this package — **it moves Payroll** |
| `OD-ATT-0016` device integration | `OD-ATT-0003`'s shape if ruled in |

---

## Orphan check — method and result

Run over the package's own markdown:

1. For each of the six spaces, collect identifiers **defined** in the home file and identifiers
   **referenced** anywhere in the package.
2. Report anything referenced but never defined.
3. Check numbering is contiguous from 0001 — a gap means an identifier was renumbered and a citation was
   left pointing at the old number.
4. Check every requirement has a criterion and every criterion has a test.

**Result: no orphans, all six spaces contiguous, full coverage in both directions.**

**That was not the first result.** The initial run found **seven requirements with no criterion** and **nine
criteria with no test**. Three of the nine were an artifact of writing `AC-ATT-0002/0003` as an abbreviated
range, which a mechanical check cannot see — so the ranges were expanded. The other thirteen were **real
gaps**, and they are closed by `AC-ATT-0039` to `AC-ATT-0045` and `TS-ATT-0040` to `TS-ATT-0052`, kept in their own sections so
the closure stays visible rather than blending in.

**The abbreviation finding is the one worth carrying forward.** `AC-ATT-0002/0003` reads perfectly well to a
person and is invisible to a script — the same class of defect as FP-012's cutover list, where one of three
name lists used `nameof(...)` while the others used string literals and an adjacency search skipped it in
silence. **A notation that only a human can resolve cannot be checked mechanically, and what cannot be
checked mechanically eventually drifts.**

---

## Corrections made to this package during its own drafting

Three claims were checked against the repository and **two of them were wrong**. Recorded because the
package's credibility rests on which of its statements were verified.

| Claim as first drafted | What the code says |
|---|---|
| "Branch is descriptive in HR; **nothing in the codebase scopes access by it**" | **Wrong.** `UserBranchAccess`, `ITenantBranchAccessResolver`, `ICurrentBranchResolver`, `IBranchTransferScope` and a session-held active branch are a complete authorization stack. `OD-ATT-0011` is rewritten and `DEC-ATT-0014` added |
| "This package has not verified whether HR models a manager relationship" | **Understated.** There is no employee→manager edge, but there **is** a `DepartmentManager` — one seat per department — over **nesting** departments, with `ManagerNotAssigned` and `ManagerTerminated` both modelled. `OD-ATT-0007` now carries three sub-questions instead of one |
| "Self-service is the obvious answer and is still a decision" | **Understated, and materially.** It is not a preference — **the identity→employee mapping does not exist**, verified. `OD-PAY-0016` deferred payroll self-service for the same reason. `REQ-ATT-0023` is marked BLOCKED and `OD-ATT-0013` now asks whether creating the mapping is a **prerequisite** of FP-013 |

**All three were found by checking rather than by review**, which is the standing lesson: *"the design is
right" is not "the code is right"*, and *"my approach is plausible" is not "this repo does it that way"*.

---

## Readiness

**Not ready to build.** Sixteen owner decisions are open, and two of them — `OD-ATT-0001` (scope) and
`OD-ATT-0011` (branch) — move the majority of this package between them.

One further item is not a decision but a **prerequisite question**: whether the missing identity→employee
mapping must be created before FP-013 can deliver anything an employee touches directly.
