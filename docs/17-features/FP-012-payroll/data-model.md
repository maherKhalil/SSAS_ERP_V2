# FP-012 — Data Model (PROPOSED)

Five tables under a `payroll` schema, all in the **Tenant** database. Shapes are proposals; the
compensation table in particular cannot be fixed until `OD-PAY-0003` is ruled.

---

## Tables

| Table | Owns | Mutable | Append-only |
|---|---|---|---|
| `payroll.EmployeeCompensation` | tenant + company | inserts only after `OD-PAY-0003` opt. 2 | history is |
| `payroll.PayElementAssignment` | tenant | with its parent | — |
| `payroll.PayElement` | tenant + company | yes | — |
| `payroll.PayrollRun` | tenant + company | until Posted | after Posted |
| `payroll.PayrollRunLine` | tenant | never | yes |

**Five new tenant-owned entities**, all carrying `ITenantOwnedEntity`.

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
* `PayrollRunLine` — index on `(PayrollRunId, EmployeeId, Sequence)`, the payslip's access path.

---

## E3 manifest and the cutover inventory

**All five entities join the E3 manifest** (`DEC-PAY-0010`).

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
a sweep that looks for only one shape finds only some of them. The count moves **20 → 25** entities.

**Do not attempt to update these from memory. Derive them, and search for every shape.**

---

## Migration

One migration, produced by **`tools/SSAS.Tenant.MigrationTool`** and its `ComposedTenantDbContextFactory`,
whose contributor list gains a `PayrollTenantModelContributor` line.

**Never `dotnet ef migrations add` against `SSAS.Platform.Infrastructure`.** Its design-time factory passes
`modelContributors: null` while the snapshot holds every module's tables, so it scaffolds a migration that
**DROPS them all**. FP-011 hit this and was saved only by reading the scaffolded output; the migration
tool's own comment predicts the failure verbatim.
