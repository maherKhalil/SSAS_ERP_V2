# FP-012 — Data Model (RATIFIED)

**Seven** tables under a `payroll` schema, all in the **Tenant** database.

> **AMENDMENT 2026-08-24 — five, then six, and the truth is SEVEN.**
>
> The package first said five tables. The aggregate-split ruling added `PayrollRunDraftLine` and the
> amendment said six. **Both were wrong.** The migration scaffolded from the composed model creates
> **seven**, because the original five never listed `PayrollPeriod` at all — a table the package's own
> `lifecycle-model.md` and `OD-PAY-0002` require.
>
> Derived from the snapshot rather than counted by hand: `ToTable("Payroll…")` appears **7** times, and the
> whole composed tenant model is **27** entities.
>
> **This is the third wrong count in one document, and it is exactly what the "derive it, don't print it"
> note two sections down was written about — including when the author of that note is the one counting.**
> The numbers below are now derived; treat every one of them as needing re-derivation before use.

---

## Tables

| Table | Owns | Mutable | Append-only |
|---|---|---|---|
| `payroll.EmployeeCompensation` | tenant + company | inserts only after `OD-PAY-0003` opt. 2 | history is |
| `payroll.PayElementAssignment` | tenant | with its parent | — |
| `payroll.PayElement` | tenant + company | yes | — |
| `payroll.PayrollRun` | tenant + company | **its whole life** | no — see note |
| `payroll.PayrollRunDraftLine` | tenant | yes, replaced wholesale | no |
| `payroll.PayrollRunLine` | tenant | never | **yes** (`IAppendOnlyEntity`) |

**Seven new tenant-owned entities**, all carrying `ITenantOwnedEntity`.

**`PayrollRun` is mutable for its whole life and is deliberately not append-only**: it must record
`PostedUtc` and `JournalEntryId` after approval, and the context's append-only guard is unconditional, so
that write would be refused. Its immutability after Posted is a domain guard. That is acceptable because the
run is the wrapper — `PayrollRunLine` and the GL journal are both structurally append-only, so a wrapper
bug cannot rewrite what anyone was paid.

---

## Conventions, none of them optional

* **`nvarchar` for every persisted application string** (`DEC-PAY-0007`). No `varchar`, anywhere.
* **`decimal(19,4)` for every monetary column** (`ADR-027`, `DEC-PAY-0004`).
* **No cross-database foreign key.** `EmployeeId` and `GlAccountId`-style references within the Tenant
  database are ordinary FKs; nothing references the Platform database (`DEC-PAY-0008`).
* **`RowVersion`** on every mutable aggregate root (`DEC-PAY-0009`).
* **Collation** per `GlPersistenceConstants`' precedent — the schema and collation names are fixed by the
  database. *If Payroll needs its own constants file, that is a third module holding the same two literals,
  which is the promotion trigger FP-011 recorded for `SSAS.BuildingBlocks` (`ADR-027` decision 4). Flag it
  then; do not promote pre-emptively.*

---

## Column notes worth stating

**`EmployeeCompensation.EffectiveFromUtc`** — no `EffectiveToUtc`, and no `IsCurrent` flag. The end of one
record is the start of the next, derived by ordering. A stored end date and a stored current-flag are both
derived state that drifts, and both have to be maintained transactionally on every insert.

**`PayrollRun` period columns** — `PeriodStartUtc`, `PeriodEndUtc` and a separate `PayDateUtc`, because the
date that determines the **fiscal period for posting** is the pay date, not the period end, and they are
routinely in different months. Conflating them is a defect that only appears at a month boundary.

**`PayrollRunLine.Amount`** — stored **already rounded** per `OD-PAY-0008`. Under the recommended option 1
the run total is the sum of stored line amounts, so the payslip adds up by construction rather than by
recomputation.

**`PayrollRunLine.Sequence`** — retains the evaluation order the run actually used. If a `PayElement`'s
`CalculationOrder` is edited afterwards, the historical run still explains itself. Without it, a payslip
reconstructed later could be ordered differently from the one the employee saw.

**`PayrollRun.JournalEntryId`** — a plain `uniqueidentifier` with **no foreign key to GL's tables**. The
modules are separate; a database-level FK would couple their migrations and make the module boundary a
fiction at the schema layer even while `ADR-012` held at the assembly layer.

---

## Indexes and uniqueness

* `EmployeeCompensation` — index on `(CompanyId, EmployeeId, EffectiveFromUtc DESC)`. This is the *only*
  access path that matters: "what was in force for this employee on this date."
* `PayElement` — unique on `(CompanyId, NormalizedCode)`. Company-scoped, following `Account`'s
  tenant-scoped precedent adjusted for `OD-PAY-0005`.
* `PayrollRun` — unique on `(CompanyId, PeriodStartUtc, PeriodEndUtc)` **only if** `OD-PAY-0011` rules out
  superseding runs. Under the supersede option two runs may legitimately share a period and the constraint
  cannot exist — noted because it is a schema consequence of a lifecycle decision, and the two are easy to
  rule separately and inconsistently.
* `PayrollRunDraftLine` — index on `(PayrollRunId, EmployeeId, Sequence)`; rows are deleted in bulk on
  recalculation, so nothing else should reference them.
* `PayrollRunLine` — index on `(PayrollRunId, EmployeeId, Sequence)`, the payslip's access path. **The
  payslip reads this table only**, never the draft table.

---

## E3 manifest and the cutover inventory

**All seven entities join the E3 manifest** (`DEC-PAY-0010`).

`TenantCutoverCopyPlan.Build` derives the manifest by **reflecting over `ITenantOwnedEntity`**. A type
without the interface is not in the manifest and is **silently absent from cutover** — FP-011 shipped two
such types before catching them, and it is a data-loss defect that no compiler and no test of the entity
itself would reveal.

**The cutover guards will fail when these tables are added, and that is them working.**
`TenantCutoverCopySqlServerTests` holds deliberately exact entity-name lists whose own comment explains why:

> AN EXACT LIST, DELIBERATELY. The derivation guarantees the engine cannot MISS a table; this guarantees a
> human SEES a new one.

FP-011's build learned that these expectations are written in **several different shapes** — literal name
arrays, arithmetic against a derived count (`composed.Value.Count - 18`), and a `TablesCopied` literal — and
a sweep that looks for only one shape finds only some of them. The manifest count moves **20 → 27** (derived from the snapshot: 27 `ToTable` entries).

> **The numbers on this page are not the authority for the edit.** The package said five tables and 20 → 25, then the
> amendment said six, and BOTH were wrong. **Derive the real count
> at implementation from the composed model, then search for EVERY shape of expectation** — literal name
> arrays, arithmetic against a derived count, and `TablesCopied` literals. FP-011 derived correctly but
> searched for one shape, and the gate found the remainder.
>
> **A superseded number in a ratified document is more dangerous than no number at all**, because it reads
> as authority. That is why this is written as *derive it*, not as a figure to copy.

---

## Migration

One migration, produced by **`tools/SSAS.Tenant.MigrationTool`** and its `ComposedTenantDbContextFactory`,
whose contributor list gains a `PayrollTenantModelContributor` line.

**Never `dotnet ef migrations add` against `SSAS.Platform.Infrastructure`.** Its design-time factory passes
`modelContributors: null` while the snapshot holds every module's tables, so it scaffolds a migration that
**DROPS them all**. FP-011 hit this and was saved only by reading the scaffolded output; the migration
tool's own comment predicts the failure verbatim.
