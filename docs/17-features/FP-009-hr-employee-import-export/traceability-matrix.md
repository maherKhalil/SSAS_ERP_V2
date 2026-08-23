---
document_id: FP-009-RTM
title: HR Employee Import and Export — Traceability Matrix
status: Approved for Implementation
version: 1.0
---

# FP-009 — Traceability Matrix

> **Approved 2026-08-22.** Every identifier this package defines is traced below, and the orphan check at the
> end is mechanical rather than asserted — enumerated, never ranged, because a range hides its middle.
>
> **The as-built section is at the end and covers PHASE 1 ONLY.** FP-007 is the reason it is written the way
> it is: its as-built pass marked a document "matched" while the field it described had never shipped. So
> every row below names a test that exists, every identifier Phase 1 does **not** close is listed as not
> closed, and one criterion is recorded as **failing as specified** rather than quietly omitted.

## Source requirement coverage

| Source | Coverage |
|---|---|
| `REQ-HR-0009` Employee Import | `FR-DOC-0101`–`FR-DOC-0103` |
| `REQ-HR-0010` Employee Export | `FR-DOC-0201`–`FR-DOC-0202` |
| `REQ-HR-0005` Employee Documents | **Transferred to FP-010** (`OD-DOC-001` → SPLIT) |
| `REQ-HR-0001` Create Employee | Reused unchanged — import composes the existing path (`BRULE-DOC-0603`) |
| `REQ-HR-0008` Employee Search | Reused unchanged — export is a scoped search (`BRULE-DOC-0605`) |

## Functional requirement coverage

| Requirement | Rules | Criteria | Scenarios | Determined by |
|---|---|---|---|---|
| `FR-DOC-0101` validate a file | `BRULE-DOC-0604` | `AC-DOC-0005` | `TS-DOC-0006` | — |
| `FR-DOC-0102` import employees | `BRULE-DOC-0601`, `BRULE-DOC-0602`, `BRULE-DOC-0603`, `BRULE-DOC-0604` | `AC-DOC-0001`, `AC-DOC-0002`, `AC-DOC-0003`, `AC-DOC-0004`, `AC-DOC-0006`, `AC-DOC-0007`, `AC-DOC-0008`, `AC-DOC-0009`, `AC-DOC-0010`, `AC-DOC-0021`, `AC-DOC-0022` | `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003`, `TS-DOC-0004`, `TS-DOC-0005`, `TS-DOC-0007`, `TS-DOC-0008`, `TS-DOC-0009`, `TS-DOC-0010`, `TS-DOC-0011`, `TS-DOC-0012`, `TS-DOC-0025`, `TS-DOC-0026` | `OD-DOC-002`, `OD-DOC-003`, `OD-DOC-004`, `OD-DOC-005` |
| `FR-DOC-0103` import run history | — | `AC-DOC-0005`, `AC-DOC-0008` | `TS-DOC-0006`, `TS-DOC-0009` | — |
| `FR-DOC-0201` export employees | `BRULE-DOC-0605`, `BRULE-DOC-0606`, `BRULE-DOC-0609` | `AC-DOC-0011`, `AC-DOC-0012`, `AC-DOC-0013`, `AC-DOC-0014`, `AC-DOC-0016`, `AC-DOC-0023` | `TS-DOC-0013`, `TS-DOC-0014`, `TS-DOC-0015`, `TS-DOC-0016`, `TS-DOC-0018`, `TS-DOC-0027` | `OD-DOC-005`, `OD-DOC-006` |
| `FR-DOC-0202` export run history | `BRULE-DOC-0609` | `AC-DOC-0015` | `TS-DOC-0017` | `OD-DOC-006` |

## Business rule coverage

| Rule | Criteria | Scenarios | Status |
|---|---|---|---|
| `BRULE-DOC-0601` no structure created by import | `AC-DOC-0010`, `AC-DOC-0022` | `TS-DOC-0011`, `TS-DOC-0026` | **Ruled** — `OD-DOC-004` |
| `BRULE-DOC-0602` no ownership change by import | `AC-DOC-0010` | `TS-DOC-0011`, `TS-DOC-0012` | Ratified |
| `BRULE-DOC-0603` imported = created | `AC-DOC-0006` | `TS-DOC-0007` | Ratified |
| `BRULE-DOC-0604` a rejected row leaves nothing | `AC-DOC-0003`, `AC-DOC-0005`, `AC-DOC-0021` | `TS-DOC-0004`, `TS-DOC-0006`, `TS-DOC-0025` | Ratified; **strengthened** by `OD-DOC-003` |
| `BRULE-DOC-0605` export never widens scope | `AC-DOC-0011`, `AC-DOC-0012` | `TS-DOC-0013`, `TS-DOC-0014` | **Settled by precedent** — `ADR-023` d.22, `ADR-025` d.10, `DEC-EMP-0029` |
| `BRULE-DOC-0606` terminated excluded by default | `AC-DOC-0013` | `TS-DOC-0015` | Ratified (`DEC-DOC-0009`) |
| `BRULE-DOC-0609` no export carries `nationalId` | `AC-DOC-0023` | `TS-DOC-0027` | **Ruled** — `OD-DOC-006` |
| `BRULE-DOC-0607`, `BRULE-DOC-0608` | — | — | **Transferred to FP-010** |

## Security requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `SEC-DOC-0401` no writable ownership fields | `AC-DOC-0002` | `TS-DOC-0002` |
| `SEC-DOC-0402` export is scoped | `AC-DOC-0011`, `AC-DOC-0012` | `TS-DOC-0013`, `TS-DOC-0014` |
| `SEC-DOC-0403` import writes only in context | `AC-DOC-0010`, `AC-DOC-0022` | `TS-DOC-0011`, `TS-DOC-0012`, `TS-DOC-0026` |
| `SEC-DOC-0404` export column set recorded | `AC-DOC-0015`, `AC-DOC-0023` | `TS-DOC-0017`, `TS-DOC-0027` |
| `SEC-DOC-0405`, `SEC-DOC-0406` | — | **Transferred to FP-010** |

## Non-functional requirement coverage

| | Criteria | Scenarios |
|---|---|---|
| `NFR-DOC-0501` caps | `AC-DOC-0007` | `TS-DOC-0008` |
| `NFR-DOC-0502` synchronous within caps | — | *Measured, not asserted — see [`test-scenarios.md`](test-scenarios.md#scenarios-deliberately-not-written)* |
| `NFR-DOC-0503` durable run records | `AC-DOC-0015` | `TS-DOC-0017` |
| `NFR-DOC-0504`, `NFR-DOC-0505` | — | **Transferred to FP-010** — cutover and backup custody are properties of stored bytes, and this package stores none |

## Decision coverage

| Decision | Where it lands |
|---|---|
| `DEC-DOC-0001` CSV only | `AC-DOC-0001`; `employee_import.format_unsupported` |
| `DEC-DOC-0002` strict header | `AC-DOC-0001`, `AC-DOC-0002`; `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003` |
| `DEC-DOC-0003` per-row report | `AC-DOC-0003`, `AC-DOC-0004`; `TS-DOC-0004`, `TS-DOC-0005` |
| `DEC-DOC-0004` idempotency key *(restated)* | `AC-DOC-0008`, `AC-DOC-0009`; `TS-DOC-0009`, `TS-DOC-0010` |
| `DEC-DOC-0005` caps | `AC-DOC-0007`; `TS-DOC-0008` |
| `DEC-DOC-0006` run records | `AC-DOC-0005`, `AC-DOC-0015`; `TS-DOC-0006`, `TS-DOC-0017` |
| `DEC-DOC-0007` synchronous | Lifecycle — the absence of an `InProgress` state |
| `DEC-DOC-0008` export format and round trip | `AC-DOC-0016`; `TS-DOC-0018` |
| `DEC-DOC-0009` terminated excluded | `AC-DOC-0013`; `TS-DOC-0015` |
| `DEC-DOC-0010`, `DEC-DOC-0011`, `DEC-DOC-0012`, `DEC-DOC-0013` | **Transferred to FP-010**, ratified as drafted and annotated FP-010-scoped |

## Owner decisions — disposition

| | Ruling | What it determined |
|---|---|---|
| `OD-DOC-001` | **SPLIT** | This package's scope; FP-010's existence |
| `OD-DOC-002` | **Create-only** | `FR-DOC-0102`; the resolved note on the import criteria |
| `OD-DOC-003` | **All-or-nothing + full report** | `FR-DOC-0102`; `AC-DOC-0021`; `TS-DOC-0025`; the restatement of `DEC-DOC-0004`; the `outcome` semantics |
| `OD-DOC-004` | **By code, never creates, under the importer's authority** | `BRULE-DOC-0601`; `AC-DOC-0022`; `TS-DOC-0026`; the column contract |
| `OD-DOC-005` | **Separate permissions** | The authorization model; `FR-DOC-0101`, `FR-DOC-0102`, `FR-DOC-0201` |
| `OD-DOC-006` | **`nationalId` never exported** | `BRULE-DOC-0609`; `AC-DOC-0023`; `TS-DOC-0027`; the export column list |
| `OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` | **OPEN-DEFERRED** | FP-010's starting inventory; `ADR-028` |

## What transferred to FP-010

One table, so nothing is lost by being in two places at once.

| Kind | Identifiers |
|---|---|
| Requirements | `FR-DOC-0301`, `FR-DOC-0302`, `FR-DOC-0303`, `FR-DOC-0304` |
| Security | `SEC-DOC-0405`, `SEC-DOC-0406` |
| Non-functional | `NFR-DOC-0504`, `NFR-DOC-0505` |
| Business rules | `BRULE-DOC-0607`, `BRULE-DOC-0608` |
| Criteria | `AC-DOC-0017`, `AC-DOC-0018`, `AC-DOC-0019`, `AC-DOC-0020` |
| Scenarios | `TS-DOC-0019`, `TS-DOC-0020`, `TS-DOC-0021`, `TS-DOC-0022`, `TS-DOC-0023`, `TS-DOC-0024` |
| Decisions | `DEC-DOC-0010`, `DEC-DOC-0011`, `DEC-DOC-0012`, `DEC-DOC-0013` |
| Owner decisions | `OD-DOC-007`, `OD-DOC-008`, `OD-DOC-009` |

**Every one kept its number.** Neither package will reallocate an identifier the other used, and new
decisions in either continue the same sequence from `DEC-DOC-0014`.

## Precedent citations — what this package takes rather than decides

| Precedent | Taken as |
|---|---|
| `ADR-012` | HR references no Platform assembly |
| `ADR-017` | No cross-database FK; SQL Server only |
| `ADR-020` | E3 manifest by construction; the copy order |
| `ADR-023` d.8, d.22 | Branch execution context; scoped reads |
| `ADR-024` | A transfer is its own operation; a file cannot perform one |
| `ADR-025` d.8, d.10 | Permission and scope are independent; materialized predicates |
| `DEC-EMP-0011`, `DEC-EMP-0027`, `DEC-EMP-0029`, `DEC-EMP-0030`, `DEC-EMP-0032` | No number generation; no cross-database FK; scope as a type; national-ID sensitivity; the deferral this package answers |
| `DEC-DEP-0023`, `DEC-DEP-0024`, `DEC-DEP-0025`, `DEC-DEP-0026`, `DEC-DEP-0029`, `DEC-DEP-0030` | Route/handler 1:1; named POSTs and no DELETE; the separation test; own problem-code namespace; manifest by construction; `409` for state conflicts |
| `DEC-POS-0018`, `DEC-POS-0022`, `DEC-POS-0026`, `DEC-POS-0030`, `DEC-POS-0031`, `DEC-POS-0034` | Scope-type-is-the-permission; the nine-site inventory; no `defaultValue` on required columns; normalized search columns; specified-but-unshipped is a real failure mode |

## Orphan check

Every identifier this package defines appears at least once above, enumerated rather than ranged.

* **9 functional requirements** — 5 defined here: `FR-DOC-0101`, `FR-DOC-0102`, `FR-DOC-0103`,
  `FR-DOC-0201`, `FR-DOC-0202`; 4 transferred and listed in the transfer table.
* **6 security requirements** — 4 here: `SEC-DOC-0401`, `SEC-DOC-0402`, `SEC-DOC-0403`, `SEC-DOC-0404`;
  2 transferred.
* **5 non-functional requirements** — 3 here: `NFR-DOC-0501`, `NFR-DOC-0502`, `NFR-DOC-0503`; 2 transferred.
* **9 business rules** — 7 here: `BRULE-DOC-0601`, `BRULE-DOC-0602`, `BRULE-DOC-0603`, `BRULE-DOC-0604`,
  `BRULE-DOC-0605`, `BRULE-DOC-0606`, `BRULE-DOC-0609`; 2 transferred.
* **23 acceptance criteria** — 19 here: `AC-DOC-0001`, `AC-DOC-0002`, `AC-DOC-0003`, `AC-DOC-0004`,
  `AC-DOC-0005`, `AC-DOC-0006`, `AC-DOC-0007`, `AC-DOC-0008`, `AC-DOC-0009`, `AC-DOC-0010`, `AC-DOC-0011`,
  `AC-DOC-0012`, `AC-DOC-0013`, `AC-DOC-0014`, `AC-DOC-0015`, `AC-DOC-0016`, `AC-DOC-0021`, `AC-DOC-0022`,
  `AC-DOC-0023`; 4 transferred.
* **27 test scenarios** — 21 here: `TS-DOC-0001`, `TS-DOC-0002`, `TS-DOC-0003`, `TS-DOC-0004`,
  `TS-DOC-0005`, `TS-DOC-0006`, `TS-DOC-0007`, `TS-DOC-0008`, `TS-DOC-0009`, `TS-DOC-0010`, `TS-DOC-0011`,
  `TS-DOC-0012`, `TS-DOC-0013`, `TS-DOC-0014`, `TS-DOC-0015`, `TS-DOC-0016`, `TS-DOC-0017`, `TS-DOC-0018`,
  `TS-DOC-0025`, `TS-DOC-0026`, `TS-DOC-0027`; 6 transferred.
* **13 decisions** — 9 here: `DEC-DOC-0001`, `DEC-DOC-0002`, `DEC-DOC-0003`, `DEC-DOC-0004`,
  `DEC-DOC-0005`, `DEC-DOC-0006`, `DEC-DOC-0007`, `DEC-DOC-0008`, `DEC-DOC-0009`; 4 transferred.
* **9 owner decisions** — 6 ruled here: `OD-DOC-001`, `OD-DOC-002`, `OD-DOC-003`, `OD-DOC-004`,
  `OD-DOC-005`, `OD-DOC-006`; 3 open-deferred to FP-010.

**`AC-DOC-0021`, `AC-DOC-0022`, `AC-DOC-0023` and `TS-DOC-0025`, `TS-DOC-0026`, `TS-DOC-0027` are new**,
added by the rulings rather than carried from the analysis: all-or-nothing, authority-scoped code resolution
and the unconditional `nationalId` exclusion are each properties that nothing in the drafted set would have
caught failing.

**No identifier is referenced that is not defined**, and no defined identifier is left untraced. The lists
above are the check, not a summary of it.

## As built — Phase 1 (2026-08-22)

> **Phase 1 is the schema, the domain and the application layer. It exposes NO ROUTES**, so everything below
> is proven by driving handlers directly. Criteria whose statement is about a route, a status code or a
> header are listed as **Phase 2** rather than claimed.
>
> Every test name here was verified to exist in the test tree before this table was written, which is the
> FP-007 correction applied: a name in this column is a name `grep` finds.

### Requirements

| Requirement | Status | Where |
|---|---|---|
| `FR-DOC-0101` validate a file | **Shipped** — handler (Phase 1) + route `HrEmployeesImportValidate` (Phase 2) | `I8_A_validate_only_run_writes_a_record_and_no_employees` |
| `FR-DOC-0102` import employees | **Shipped** — handler + route `HrEmployeesImport` | `I1`, `I2`, `I3`, `I6`, `I7`, `I13`, `I14` |
| `FR-DOC-0103` import run history | **Shipped in Phase 2** — handler + route `HrEmployeesImportRuns` | `H1`, `H2`, `H3`, `H5` |
| `FR-DOC-0201` export employees | **Shipped** — handler + route `HrEmployeesExport` | `X1`, `X3`, `X4`, `X7` |
| `FR-DOC-0202` export run history | **Shipped in Phase 2** — handler + route `HrEmployeesExportRuns` | `H2`, `H4`, `H5` |

> **Phase 2 flipped the last three columns.** `FR-DOC-0103` and `FR-DOC-0202` had no application handler at
> all in Phase 1 — they are read *routes*, and Phase 1 exposed none — so Phase 2 built both the handlers and
> the routes. The five routes take the HR surface from **41 to 46**.

### Business rules

| Rule | Status | Where |
|---|---|---|
| `BRULE-DOC-0601` no structure created by import | **Proven** | `I4_An_unresolvable_code_is_a_row_error_and_creates_nothing` |
| `BRULE-DOC-0602` no ownership change by import | **Inherited, not separately asserted** — the import composes `CreateEmployeeCommandHandler`, so the tenant/company/branch stamping and its refusals are the ones FP-006 already proves. Recorded as inherited rather than claimed as tested here | `I1` (composition), FP-006's own boundary tests |
| `BRULE-DOC-0603` imported = created | **Proven from the database side** — four rows per employee, none special-cased | `I1_An_applied_import_creates_every_employee_through_the_ordinary_create_path` |
| `BRULE-DOC-0604` a rejected row leaves nothing | **Proven** | `I2`, `I3` |
| `BRULE-DOC-0605` export never widens scope | **Proven** | `X1_An_export_carries_only_the_employees_the_callers_scope_admits` |
| `BRULE-DOC-0606` terminated excluded by default | **Proven** | `X4_Terminated_employees_are_excluded_unless_the_caller_asks_for_them` |
| `BRULE-DOC-0609` no export carries `nationalId` | **Proven** | `X2_No_caller_shape_can_make_an_export_carry_a_national_identifier` |

### Security

| | Status | Where |
|---|---|---|
| `SEC-DOC-0401` no writable ownership fields | **Proven at the header** | `An_unrecognised_column_refuses_the_file` (`companyId`/`branchId`/`tenantId`/`status` as inline cases), `I11` |
| `SEC-DOC-0402` export is scoped | **Proven** | `X1`, `X6` |
| `SEC-DOC-0403` import writes only in context | **Partly proven** — code resolution is proven company-scoped (`I5`); the cross-company WRITE refusal is the create path's own boundary and is not re-asserted here | `I5`, FP-006's boundary tests |
| `SEC-DOC-0404` export column set recorded | **Proven** | `X5`, `X2` |

### Acceptance criteria

| Criterion | Status | Where |
|---|---|---|
| `AC-DOC-0001` header contract | ✅ | `A_missing_required_column_refuses_the_file`, `An_unrecognised_column_refuses_the_file` |
| `AC-DOC-0002` ownership columns absent by construction | ✅ | `An_unrecognised_column_refuses_the_file`, `I11` |
| `AC-DOC-0003` every row is validated | ✅ | `I3`, `Every_malformed_row_is_reported_rather_than_the_first` |
| `AC-DOC-0004` row numbers are file line numbers | ✅ | `Row_numbers_are_the_line_numbers_the_operators_editor_shows`, and `I2` names row **744** of 1,001 |
| `AC-DOC-0005` validation writes nothing | ✅ | `I8` |
| `AC-DOC-0006` imported = created | ✅ | `I1` |
| `AC-DOC-0007` caps enforced | ✅ at the application layer | `I12`. The **transport floor** (`IHttpMaxRequestBodySizeFeature`) is Phase 2, because it is set on a route |
| `AC-DOC-0008` idempotent replay | ✅ | `I9` |
| `AC-DOC-0009` a refusal still consumes its key | ✅ | `I10`, `A_refused_run_still_occupies_its_import_key` |
| `AC-DOC-0010` an import cannot cross a company boundary | ◐ partial — see `SEC-DOC-0403` | `I5` |
| `AC-DOC-0011` export scoped, narrower for a narrower caller | ✅ | `X1` |
| `AC-DOC-0012` there is no unscoped export path | ◐ partial — the handler half is proven; "no route or parameter" needs routes | `X1`, `X6` |
| `AC-DOC-0013` terminated excluded by default, includable by name | ✅ | `X4` |
| `AC-DOC-0014` export accepts exactly the search vocabulary | ✅ — and structurally: one shared predicate, so the **next** filter is inherited too | `X3` |
| `AC-DOC-0015` every export writes a run record naming the column set and the scope | ✅ | `X5` |
| `AC-DOC-0016` round trip | ✅ **for an all-`Active` export** — `OD-DOC-010` ruled 2026-08-22. A `Terminated` or `Inactive` row refuses with a NAMED row error, which is the correct behaviour rather than a residual gap | `X8_An_exported_file_re_imports_and_a_terminated_export_refuses_by_name`, `X9_A_default_export_containing_an_inactive_employee_also_refuses_on_re_import` |
| `AC-DOC-0021` all-or-nothing is observable | ✅ | `I2_One_bad_row_in_a_thousand_leaves_no_employees_at_all` |
| `AC-DOC-0022` codes resolve under the caller's own authority | ✅ | `I5_A_code_in_another_company_is_refused_identically_to_a_code_that_exists_nowhere` |
| `AC-DOC-0023` no export carries `nationalId` | ✅ | `X2` |

**The three observability criteria named at finalization are `AC-DOC-0021`, `AC-DOC-0022` and `AC-DOC-0023`.
All three are closed**, and each is asserted against the thing that would actually be wrong — zero employees
in the database, two rejections compared field by field, and the bytes plus the run record's column-set line.

### Decisions, as built

| Decision | Where |
|---|---|
| `DEC-DOC-0002` strict column contract | the parser tests, in full |
| `DEC-DOC-0003` per-row report, every row | `I3`, `Every_malformed_row_is_reported_rather_than_the_first` |
| `DEC-DOC-0004` idempotency by import key | `I9`, `I10`, `Two_runs_in_one_company_cannot_share_an_import_key`, `Two_companies_may_use_the_same_import_key` |
| `DEC-DOC-0005` caps | `I12`, `X7` |
| `DEC-DOC-0006` a durable run record for every run | `I14`, `X5`, `An_import_run_cannot_be_updated_after_it_is_written`, `An_export_run_cannot_be_deleted_after_it_is_written` |
| `DEC-DOC-0007` synchronous, so no `InProgress` state | `The_outcome_vocabulary_is_exactly_three_terminal_values`, `An_unknown_outcome_including_in_progress_is_refused_by_the_database` |
| `DEC-DOC-0008` UTF-8 CSV, same column contract | the writer tests; the round trip itself is `OD-DOC-010` |
| `DEC-DOC-0009` terminated excluded unless requested | `X4` |
| **`DEC-DOC-0014`** raw `text/csv`, no multipart *(new)* | `StrictCsvReaderTests`, in full |
| **`DEC-DOC-0015`** export needs `Export` **and** `View` *(new, ratified)* | `X6` |
| **`OD-DOC-010`** `status` recognized, one accepted value *(new, ruled)* | `X8`, `X9`, `The_status_column_is_recognised_and_optional`, `The_exported_header_parses_as_an_import_header` |

### What Phase 1 does not close

* ~~**Every route.**~~ **CLOSED IN PHASE 2** — all five ship, with the problem-code mapping, the
  `Content-Disposition` and BOM handling, the `nosniff` composition and the per-route transport ceiling.
* ~~**Where `importKey` travels.**~~ **CLOSED** — a query parameter under the strict allowlist
  (`DEC-DOC-0014`, adopted).
* **Where the imported FILE NAME comes from.** Newly open: `DEC-DOC-0014` removed the multipart part that
  carried it, the same way it removed `importKey`'s field, and that half was not noticed at the time. As
  built it is an optional `X-File-Name` header defaulting to `import.csv`; see `api-contracts.md`.
* **`AC-DOC-0016` beyond an all-`Active` export.** `OD-DOC-010` is ruled and implemented. What remains open
  is whether `DEC-DOC-0009`'s DEFAULT status set should narrow from `Active`+`Inactive` to `Active` so that a
  default export round-trips unconditionally. That is a different decision about what a default export is,
  and it was not taken here.
* **The route inventory.** `api-contracts.md` says the HR surface goes 41 → 46. It is still **41**, and
  `HrRouteInventoryTests` is unchanged, because no route was added.

## Phase 1 exit gate — GREEN, with a named and carried certification debt

**Verdict, 2026-08-22:** Phase 1 is **closed green**. Debug is clean at **2,995 / 2,995**. Release is
**2,994 / 2,995**, and the single failure is `CatalogLeakGuardTests.No_test_catalog_survived_a_previous_run`
— a hygiene guard that asserts on the *instance's* state, not on product behaviour.

| Suite | Debug | Release |
|---|---|---|
| Architecture.Tests | 404 | 404 |
| Platform.Tests | 963 | 963 |
| HR.Tests | 326 | 326 |
| API.Tests | 573 | 573 |
| Integration.Tests | **729** | 728 (+1 guard) |
| **Total** | **2,995 clean** | **2,994 + explained guard** |

### The failure, and why it is environmental

Three runs of the same binaries differ in exactly one recorded variable, and it is the precise condition the
guard asserts on:

| Run | Catalogs present at process start | Integration result |
|---|---|---|
| Gate, Debug (1 h 54 m) | **11** | Failed 1 / 729 |
| Gate, Release (1 h 56 m) | **3** | Failed 1 / 729 |
| Diagnostic, Debug (1 h 55 m) | **0** | **Passed 729 / 729** |

The guard queries `sys.databases WHERE name LIKE 'SSAS[_]%' AND create_date < @started` and fails on any
match. **Its own comment predicted this exact scenario** — *"This cannot tell a previous run's orphan from a
CONCURRENT sibling suite's live catalog… The correct response is to SERIALISE THE RUNS, never to weaken the
guard."*

The orphans were **self-inflicted**: a killed earlier gate plus filtered test runs during the waiting window
left catalogs behind, and the gate started on top of them. No product code is implicated.

**Mechanism proof, not inference.** One catalog was created deliberately and the guard run alone against it:

| Catalogs at process start | Guard |
|---|---|
| 1 (deliberate) | **FAILED** |
| 0 | Passed, 3.5 s |

### Classification, and a correction to the standing vocabulary

Class **(c) environmental** — but **not flaky**. The failure is *deterministic given its precondition*, which
is why it reproduced identically across two configurations two hours apart under different optimisation.
**"Deterministic-given-precondition" is a distinct shape from "flaky"** and is now named as such: a flake
would not have reproduced, and treating this as one would have invited a retry-for-green instead of a
diagnosis.

### The certification debt, carried explicitly

**Release has no clean run.** A ceremonial 2-hour Release pass was declined because Phase 2's exit gate
re-runs the entire suite in both configurations from a hygienic start within days.

> **THE CONDITION: if Phase 2's Release Integration is anything but clean, Phase 1's green is VOID and this
> record is where we return.** The debt is named and carried, not waived.

### Two defects in the gate script itself, found by this failure

Both are fixed; the previous script is preserved as `gate-old.sh.bak` (outside the repo, with the gate
tooling).

1. **The filter destroyed failure identities.** Output was piped through `grep -E "Passed!|Failed!|error"`,
   and that filter *was* the only record. xUnit names failures as `[xUnit.net …] Class.Method [FAIL]` followed
   by `Error Message:` — `[FAIL]` matches neither `Passed!` nor `Failed!`, and `Error` is capitalised while
   the pattern sought lowercase. A four-hour run produced a red result naming nothing. **Fix:** every
   invocation writes its complete output to a log file unfiltered, `--logger trx` per suite per
   configuration, and the grep became a view over that file.
2. **The pipe destroyed the automation verdict, and this is the worse one.** `$?` was *grep's* status, so the
   gate exited **0** while both configurations had a red Integration suite. **Fix:** `STATUS=$?` captured per
   suite, a `!!! Suite (Config) EXITED n` line, and an explicit `[GATE RED]` / `[GATE GREEN]` verdict.

**No historical GREEN is contaminated, and this was verified rather than asserted.** Every archived task
output was re-read: the grep pattern happened to match *both* summary shapes (`Passed!` and `Failed!`), so
the per-suite table always survived — every gate verdict in this feature line was read from those tables,
never from an exit code. The defect's total cost was one recoverable identity.

### The structural fix, so the operator cannot forget

`gate.sh` now **reaps catalogs to zero itself**, immediately before each configuration, with preconditions
verified in-script — no testhost running, the enumeration printed before anything is dropped, protected names
unmatched, and zero verified afterwards. Any precondition failure **aborts the gate loudly** rather than
reaping blind, because a blind sweep is exactly what the guard's comment rejects: it could not distinguish a
previous run's orphan from a sibling's live catalog either.

Hand-reaping before a gate is no longer a step anyone performs or forgets.

## Phase 2 exit gate — and the discharge of Phase 1's debt

**Run 2026-08-22/23, both configurations, from a zero-catalog start the gate enforces itself.**

| Suite | Debug | Release |
|---|---|---|
| Architecture.Tests | 404 | 404 |
| Platform.Tests | 963 | 963 |
| HR.Tests | 326 | 326 |
| API.Tests | 622 | 622 |
| Integration.Tests | 735 (+1 failure) | **736** |
| **Total** | 3,050 + 1 failure | **3,051 clean** |

Builds 0 warnings in both. Catalogs **0 before and 0 after** each configuration — the structural reap
(`gate.sh`) working, and the condition Phase 1's failure was caused by is now impossible to arrive at by
forgetting.

### PHASE 1'S CERTIFICATION DEBT IS DISCHARGED

Phase 1 closed green with one condition recorded: *"if Phase 2's Release Integration is anything but clean,
Phase 1's green is VOID."* **Release Integration is 736 / 736 clean.** The debt is settled and Phase 1's
green stands.

### The Debug failure — class (c), and not FP-009's

`TenantBackupSchedulerSqlServerTests.A_backup_started_outside_the_platform_makes_the_sweep_skip`, expected
`SkippedInFlightOperation`, observed `Succeeded`.

The test starts a real backup **outside** the platform, waits for it to become visible, then sweeps and
expects the sweep to skip. It already carries a 30-second wait and **five retries** — the author knew it was
timing-sensitive. It failed because the competing backup finished between the last visibility check and the
sweep's inspection, so the precondition the assertion depends on had lapsed. **Nothing shows the product
behaviour is wrong**; the case it exercises simply was not in flight when it was looked at.

Evidence it is neither a stale test (a) nor a product defect (b):

| Run | This test |
|---|---|
| Phase 1 gate, Debug (11 catalogs) | passed — the single failure there was the catalog guard |
| Phase 1 gate, Release (3 catalogs) | passed — same |
| Diagnostic, Debug (0 catalogs, 729/729) | **passed** (confirmed in its TRX) |
| Phase 2 gate, Release (736/736) | **passed** (confirmed in its TRX) |
| Phase 2 gate, Debug | failed |

**FP-009 touched no backup or scheduler code** — `git diff --name-only 0e0ea4e..HEAD` matches nothing under
either name.

Per the standing rules a class-(c) finding **gets no retry-for-green**, so none was run. It is reported with
its evidence and left for a ruling.

*(It is also a member of the serial collection the gate-economics task will examine — `TenantBackupScheduler`
is one of the thirteen no-stated-reason members, and this failure is a data point about what that chain's
timing actually depends on.)*

### The gate script's two fixes, proven by this run

Both defects found after the Phase 1 gate are demonstrably closed:

* the failing test's **identity was captured** — `[FAIL] …A_backup_started_outside_the_platform_makes_the_sweep_skip`
  appears in the console output, where Phase 1's was destroyed by the filter;
* the **exit code was honest** — `!!! Integration.Tests (Debug) EXITED 1` and a closing `[GATE RED]`, where
  Phase 1's doubly-red gate reported success.

## A correction to this record — the "BLOCKER" was not one

**Recorded 2026-08-23, after the Phase 2 gate.**

Stage C reported a BLOCKER: that `ApplyAsync` wrote the import refusal record inside the `await using var
transaction` scope it had just rolled back, and that `await using var` running to end-of-method meant the
save was issued against a dead transaction.

**The reasoning about `await using var` is right. The conclusion was wrong.**
`EfUnitOfWork.RollbackAsync` disposes the transaction in its `finally` and nulls its own field, which clears
it from the `DbContext` — so the following `SaveChangesAsync` opens its own transaction and commits normally.

It was checked rather than argued: the original shape was **reintroduced deliberately** and `I15` — the
integration test written to guard it, running against real SQL Server — **passed either way**. A regression
guard that passes in the presence of the defect it names is no guard at all, which is what that experiment
was for.

**What stands, and why:**

* The two-method split is **kept**, on an honest justification rather than the first one: it makes the
  transaction unreachable from the method that decides what to record, so the next reader does not have to
  re-derive the question. It is clarity, not a fix.
* `I15` is **kept**, on the contract rather than the scare: `DEC-DOC-0006` makes the run record the audit
  trail, and nothing else asserted that a refusal arriving after a rollback still leaves one. That property
  now has a test instead of an assumption.
* Both code comments and the API test's comment are **corrected in place**, because a comment asserting a
  defect that never existed would send a later reader looking for it — or worse, stop them simplifying the
  structure for a reason that is not true.

The commit message on `666df69` describes the change as a fix for a blocker. It is left as written and
corrected here, in the same spirit as every other superseded claim in this package: the record shows what was
believed, when, and what checking it produced.
