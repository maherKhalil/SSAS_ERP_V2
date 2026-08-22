---
document_id: FP-009-TS
title: HR Employee Data Exchange — Test Scenarios
status: Analysis — Owner Decisions Required
version: 0.1
---

# FP-009 — Test Scenarios

> Twenty-four scenarios. **Layer** says where each one can actually be proven: `D` domain, `A` application,
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

## Documents *(conditional on `OD-DOC-001`)*

| | Layer | Scenario | Criteria |
|---|---|---|---|
| `TS-DOC-0019` | H | Upload a PDF renamed `.png` and declared `image/png` → `400 employee_document.content_type_rejected` | AC-DOC-0017 |
| `TS-DOC-0020` | H | A caller with `View` and without `Download` lists a document, then requests its content → `403`, and the metadata response contains no URL that would serve as an alternative route to the bytes | AC-DOC-0018 |
| `TS-DOC-0021` | S | A document belonging to an out-of-scope employee → `404`, byte-identical to the answer for a document that never existed | AC-DOC-0019 |
| `TS-DOC-0022` | A | Withdraw, then withdraw again → `409 employee_document.transition_invalid`; uploader and timestamp still readable afterwards | AC-DOC-0020 |
| `TS-DOC-0023` | **S** | **Cutover custody.** A tenant holding one document is copied Shared→Dedicated. Under the in-database option: the content table copies and the destination's `ContentHash` matches the source bytes. Under any other option: the copy **fails fast**, naming the tenant and the document count, and writes nothing at the destination | NFR-DOC-0504, AC-DOC-0020 |
| `TS-DOC-0024` | S | **Manifest arithmetic.** The E3 manifest count and exact set match the new inventory, and the topological order places `Employees` before `EmployeeDocuments` and `EmployeeDocuments` before `EmployeeDocumentContents` | NFR-DOC-0504 |

## Scenarios deliberately not written

| Not written | Why |
|---|---|
| Concurrent imports of the same key from two connections | The unique index on `(CompanyId, NormalizedImportKey)` decides it, and asserting "exactly one loser" would fail on correct behaviour — the `DEC-DEP` concurrency note makes the same point about the manager primary key |
| Performance of a 5,000-row import | A latency budget (`NFR-DOC-0502`) is not a correctness property, and a timing assertion in a suite that runs on shared hardware is a flake generator. Measured, not asserted |
| Export of every column combination `OD-DOC-006` might produce | The ruling produces one column set; enumerating hypotheticals tests the spec, not the code |
| Rejected-row persistence | Nothing persists them (`DEC-DOC-0006`), so there is nothing to assert beyond `TS-DOC-0006`'s "nothing written" |

## What the suite must not do

**No test may construct a read scope directly.** Every scope in every scenario above comes from the resolver,
because a test that hand-builds one proves the read filters — which was never in doubt — while proving
nothing about whether the *route* obtains the right scope. That is the FP-008 lesson recorded at
`PositionFixture.FixtureTenant`, and it applies unchanged.
