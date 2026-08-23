---
document_id: FP-009-TS
title: HR Employee Import and Export — Test Scenarios
status: Approved for Implementation
version: 1.0
---

# FP-009 — Test Scenarios

> **Approved 2026-08-22. Eighteen scenarios** — `TS-DOC-0019`–`0024` travelled to FP-010 with the documents
> material — **plus three added by the rulings** (`TS-DOC-0025`–`0027`), because all-or-nothing and the
> `nationalId` exclusion are properties nothing in the drafted set would have caught failing.
>
> **Layer** says where each one can actually be proven: `D` domain, `A` application,
> `S` SQL Server integration, `H` HTTP. The column exists because FP-008 paid for getting it wrong — a
> scope-containment property cannot be proven against a stub, and a stub returning its seed will agree with a
> predicate that filters nothing.

## Import

| | Layer | Scenario | Criteria |
|---|---|---|---|
| `TS-DOC-0001` | H | Submit a file missing `positionCode` → `400`, the response names the column, and no row was parsed | AC-DOC-0001 |
| `TS-DOC-0002` | H | Submit a file with an extra `companyId` column → `400` by the unknown-column rule, **not** an accepted import that ignored it | AC-DOC-0001, AC-DOC-0002 |
| `TS-DOC-0003` | H | Reorder the columns and upper-case the header → imports identically | AC-DOC-0001 |
| `TS-DOC-0004` | A | A file with errors in rows 14 and 902 → both appear in the report; `rejectedCount` is 2 | AC-DOC-0003 |
| `TS-DOC-0005` | A | An error in the first data row reports `rowNumber: 2` | AC-DOC-0004 |
| `TS-DOC-0006` | S | Dry-run a wholly valid 100-row file → employee count unchanged, **no** `EmployeeBranchAssignments` rows, run outcome `Validated`; then import the same file → 100 employees | AC-DOC-0005 |
| `TS-DOC-0007` | S | Import one employee, create one through the ordinary route, compare the two rows: same normalization, same stamped branch, one initial assignment each, audit fields populated identically in shape | AC-DOC-0006 |
| `TS-DOC-0008` | H | A 5,001-row file → `400` naming both the limit and 5,001, proven by the parser never being entered | AC-DOC-0007 |
| `TS-DOC-0009` | S | Import under key `K`; import a **different** file under `K` → the original run's result, and the second file's employees do not exist | AC-DOC-0008 |
| `TS-DOC-0025` | **S** | A 1,000-row file with **one** invalid row → zero employees created, company employee count identical before and after, outcome `Refused`, `acceptedCount` 0, and the one error reported | AC-DOC-0021 |
| `TS-DOC-0026` | S | A `departmentCode` that exists only in a company the caller cannot see → unresolvable, and the message is byte-identical to the one for a code that exists nowhere | AC-DOC-0022 |
| `TS-DOC-0010` | S | A refused import under key `K`, then a valid file under `K` → the refusal is returned; the valid file did not import | AC-DOC-0009 |
| `TS-DOC-0011` | S | A file whose `departmentCode` exists **in another company** → the row is rejected as not-found, and no department is created in either company | AC-DOC-0010, BRULE-DOC-0601 |
| `TS-DOC-0012` | S | Import as a caller in company A while company B holds an identical `employeeNumber` → succeeds; the number is unique per company, not per tenant | AC-DOC-0006 |

## Export

| | Layer | Scenario | Criteria |
|---|---|---|---|
| `TS-DOC-0013` | **S** | Two employees in two branches; a caller authorized for one branch exports → one row. The **same** company exported by a two-branch caller → two rows. *Must be SQL: a stub returns its seed regardless of the predicate* | AC-DOC-0011 |
| `TS-DOC-0014` | S | A caller whose authorized branch set is empty → `403`, never an unfiltered file | AC-DOC-0012 |
| `TS-DOC-0015` | S | Export with no `status` → terminated employees absent; with `status=Terminated` → present | AC-DOC-0013 |
| `TS-DOC-0016` | H | Every query parameter employee search accepts is accepted by export with the same meaning; every parameter search rejects is rejected. **Enumerated from the search allowlist, not hand-listed** | AC-DOC-0014 |
| `TS-DOC-0017` | S | A successful export writes one run record carrying the column set and the resolved company and branch sets; a **failed** export writes none | AC-DOC-0015 |
| `TS-DOC-0018` | H | Export a company, feed the response bytes back to `import/validate` → zero errors | AC-DOC-0016 |
| `TS-DOC-0027` | **H** | **`nationalId` never appears.** Enumerate every filter combination and scope mode the export accepts; assert the header row of each response, and assert that no response body contains the seeded national identifier of an employee known to have one | AC-DOC-0023 |

## Documents — transferred to FP-010

`TS-DOC-0019`–`TS-DOC-0024`, including the cutover-custody scenario and the manifest-arithmetic scenario for
document tables, moved to [FP-010](../FP-010-hr-employee-documents/) keeping their identifiers.

**One of them changed meaning on the way**: `TS-DOC-0024`'s manifest arithmetic is now FP-009's own concern
for the two run tables (11 → 13) and FP-010's for the document tables. This package asserts its own
arithmetic; FP-010 asserts the rest.

## Scenarios deliberately not written

| Not written | Why |
|---|---|
| Concurrent imports of the same key from two connections | The unique index on `(CompanyId, NormalizedImportKey)` decides it, and asserting "exactly one loser" would fail on correct behaviour — the `DEC-DEP` concurrency note makes the same point about the manager primary key |
| Performance of a 5,000-row import | A latency budget (`NFR-DOC-0502`) is not a correctness property, and a timing assertion in a suite that runs on shared hardware is a flake generator. Measured, not asserted |
| Export of every column combination `OD-DOC-006` might produce | The ruling produced **one** column set, so there are no combinations left to enumerate — `TS-DOC-0027` asserts the single outcome across every *caller*, which is the axis that can actually vary |
| Rejected-row persistence | Nothing persists them (`DEC-DOC-0006`), so there is nothing to assert beyond `TS-DOC-0006`'s "nothing written" |

## What the suite must not do

**No test may construct a read scope directly.** Every scope in every scenario above comes from the resolver,
because a test that hand-builds one proves the read filters — which was never in doubt — while proving
nothing about whether the *route* obtains the right scope. That is the FP-008 lesson recorded at
`PositionFixture.FixtureTenant`, and it applies unchanged.
