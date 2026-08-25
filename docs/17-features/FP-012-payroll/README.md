# FP-012 — Payroll Foundation (analysis package)

**Status:** **RATIFIED 2026-08-24.** All eighteen `OD-PAY` decisions are ruled and `DEC-PAY-0001`–`0015`
are ratified as drafted; `DEC-PAY-0016` is new. `Requirement-Catalog/PAY.md` carries `REQ-PAY-0001`–`0018`
and `REQ-PAY` is indexed in the catalog README. See [`decisions-approved.md`](decisions-approved.md).

---

# ⚠ THE BOUNDARY OF THIS FEATURE — read this before anything else

> **FP-012 V1 IS JURISDICTION-NEUTRAL. IT SHIPS NO TAX TABLES AND NO STATUTORY DEDUCTIONS.**
>
> The net figure it produces is **gross minus configured deductions**. It is **not** a legally compliant
> net pay in any jurisdiction, and nothing built on it should imply that it is.

This is `DEC-PAY-0016`, ruled knowingly rather than overlooked. Income tax, social insurance and mandated
contributions are legal facts of a jurisdiction, not product choices; no jurisdiction is named anywhere in
the specification, and inventing one would encode a guess as a requirement and ship it as authority.

A tenant **can** define a deduction element with an amount or a rate. A tenant **cannot** have the product
compute a statutory liability, apply a bracket, or produce a filing. Adding a jurisdiction later is
additive under `OD-PAY-0006`'s ruling — a behaviour bound to elements — so nothing here has to be undone to
accommodate it. That is what makes the boundary affordable rather than merely accepted.

---

## The sweep came first, and it found nothing

Before a line of this package was written, the solution, `src/Modules` (including the `Finance` folder),
`tools/`, `tests/`, every local and remote branch, and every object in every ref were swept for a
pre-existing Payroll skeleton, project, test home or document.

| Swept | Result |
|---|---|
| `SSAS.ERP.sln` | **No Payroll project** |
| `src/Modules/` | `Finance/SSAS.GL.*` and `HR/SSAS.HR.*` only — **no Payroll tree** |
| `tools/`, `tests/` | `SSAS.Localization.CatalogTool`, `SSAS.Tenant.MigrationTool`; test projects API / Architecture / Finance / HR / Integration / Performance / Platform / TestSupport / UI — **no Payroll test home** |
| Branches (local + remote) | **No branch matching `pay`** |
| Every path in every ref | **No file path matching `payroll`, `SSAS.Pay*`, or `/Pay*`** |
| Commit messages | Payroll appears in **prose only** (GL, FP-008 and tenant-storage docs) |

**Payroll is genuinely unbuilt.** This is the opposite of FP-011, where a GL skeleton already existed and
`Program.cs` already called `AddGlModule()`. Nothing here is being adopted; the module home is therefore a
real proposal rather than a discovery — see `OD-PAY-0014`.

*Incidental finding, reported and not acted on:* `src/Modules/Finance/SSAS.GL.Contracts/` still exists on
disk, containing **only gitignored `bin/` and `obj/` build residue** from before FP-011 removed it. It is
untracked, absent from the solution, and referenced by nothing. It is not a skeleton and it is not this
package's to delete.

---

## What authority actually exists

This is the **thinnest authority base of any feature package so far**, and saying so plainly is part of the
analysis rather than a complaint.

| Source | What it actually says |
|---|---|
| `Requirement-Numbering.md` | `REQ-PAY-0001` — a **bare prefix reservation** in a list. No text, no requirement |
| `Requirement-Catalog/` | Holds `HR.md`, `GL.md`, `Platform.md`, `Constraints.md`, NFRs, README, matrix. **No `PAY.md`** |
| `Business-Rules.md` | **No `BR-PAY-####` rule exists.** Payroll appears under *"Future Modules — Business Rules … will be added in future releases"* |
| `BR-PLT-0103` | Sensitive operations require elevated permissions. Examples: Delete, Reverse Journal, Close Fiscal Year, **Payroll Processing** |
| `Glossary.md` | Lists **"Payroll"** as a bare term between *General Ledger* and *Inventory*. No definition body |
| `Product-Roadmap.md` | **Payroll is the first Version 2 line**, above Attendance, Recruitment, Performance |

So: a reserved number, one platform rule naming payroll processing sensitive, a bare glossary term, and a
roadmap position. **Everything else in this package is proposed and traced, or raised for the owner.**

The `GL.md` precedent governs the catalog gap. FP-011 met the same absence and drafted `REQ-GL-0001`–`0014`
as proposals ratified into the catalog by owner decision. `REQ-PAY-0001`–`0018` here are drafted the same
way: **proposed, every one owner-decision-required**, traced to whatever authority exists — and where none
exists, saying so.

---

## Sequencing — closed, not asked

Payroll is a **Roadmap Version 2** line. The owner's **2026-08-24** instruction pulls it forward ahead of
the remaining V2 items.

**This is recorded as `DEC-PAY-0001`, a closed sequencing decision, and is not raised as a question.** The
owner sets scope order; the Roadmap records a plan, not a constraint on the owner. What the pull-forward
*does* create is a hard technical boundary that is not negotiable by sequencing — see the Attendance
boundary below, and `DEC-PAY-0002`.

---

## The inherited section — recorded verbatim, none reopened

These are settled facts from earlier packages. **This package does not reopen any of them.** They are
reproduced here because a reader of FP-012 alone would otherwise re-litigate them.

### `DEC-POS-0023` — Payroll's birth certificate

> FP-008 introduces **no employee compensation field**. No salary, wage, rate, or pay column is added to
> `Employee`, and no such value is stored anywhere in this package. A Salary Grade is a band attached to a
> job; **what an individual is paid is Payroll.**

HR deliberately holds salary **structure** — grade bands, informational — and no compensation value.
Payroll now owns the actual-pay record. **Where that record lives is this package's central data question**
(`OD-PAY-0003`).

### `BR-HR-0004` — Termination

> A terminated employee cannot be assigned new business transactions.

Payroll's inclusion rules must state what this means for **final-pay runs**, which are by nature a
transaction for someone who has just been terminated (`OD-PAY-0006`).

### `OD-GL-0009` — the door this package opens

> **RULED: option 1 — manual journal entry only.** Nothing posts to GL in V1; the first inbound poster will
> be **Payroll in V2.** GL therefore depends on no module, `ADR-012` is untouched, and there is no
> cross-module contract to design.

**FP-012 is that poster.** The GL posting contract is the product's **first cross-module integration**.
`ADR-012` constrains it: a promoted contract or an event, **never an assembly reference**. And FP-011's
recorded recreate-condition for `SSAS.GL.Contracts` — *"returns when Payroll consumes it, shaped by its
consumer"* — **fires here** (`OD-PAY-0008`).

### GL facts payroll posting must respect

None of these are open:

* Fiscal calendars are **company-owned**; the chart of accounts is tenant-wide.
* **`BR-GL-0003`** — posting into a **closed period is prohibited**.
* **`BR-GL-0001`** — journals **balance**; debits equal credits.
* Posted journals are **append-only**. A payroll correction is a **reversal, never an edit**.
* **`GL.Journals.Post`** is a separately-granted sensitive permission, deliberately split from
  `GL.Drafts.Manage` so preparation and posting can be different people.

### Money and currency

* **`ADR-027`** — money is `decimal(19,4)`.
* **`OD-GL-0002`'s closure** — single currency; the company's `BaseCurrencyCode` is projected on read.
* **A payroll in another currency is the multi-currency trigger, and pulling it is NOT this package's to
  do.** Recorded as `DEC-PAY-0003`.

### Pattern stack inherited without argument

* Permission grammar `<Plane>.<Resource>.<Action>` with **sensitivity splits**. Pay data is the most
  sensitive read surface this product will have, so the `DEC-POS-0018` pay-band separation precedent
  applies **with more force**, not less.
* **Unforgeable read scopes** — a scope is constructed by a resolver from the caller's grants, never
  supplied by the caller.
* **Append-only run records**, in the `EmployeeImportRun` / `EmployeeExportRun` shape.
* **E3 manifest + inventory** — every tenant-owned entity carries `ITenantOwnedEntity`, or it is silently
  absent from cutover.
* **`nvarchar`** for every persisted application string.
* **No cross-database foreign key** between the Platform and Tenant databases.
* **`RowVersion`** on every mutable aggregate.

---

## The boundary the pull-forward creates

**Attendance is unbuilt, and it is a separate Roadmap V2 line.** Therefore:

> **Attendance-driven pay components cannot exist in FP-012.** Overtime computed from worked hours,
> absence deductions derived from an attendance register, shift differentials, and late penalties all
> require a source of truth that **does not exist in this product**.

This is not a scoping preference — it is an absence of input. A V1 payroll can accept a **manually entered
quantity or amount** for such an element, but it cannot *derive* one. `DEC-PAY-0002` records the boundary,
and `OD-PAY-0001` asks the owner to confirm the V1 element set inside it.

---

## Package contents

| File | What it holds |
|---|---|
| `README.md` | This: the sweep, the authority base, the inherited section, the boundary |
| `requirements.md` | Proposed `REQ-PAY-0001`–`0018`, each traced or marked unauthored |
| `business-rules.md` | Proposed `BR-PAY-####`, and what they would need from the master rule set |
| `domain-model.md` | Aggregates, entities, and the compensation-record question |
| `data-model.md` | Tables, keys, money columns, E3 manifest membership |
| `lifecycle-model.md` | The payroll run state machine, rerun and correction semantics |
| `authorization-model.md` | Permissions and the pay-data read scope |
| `api-contracts.md` | Proposed route surface |
| `acceptance-criteria.md` | `AC-PAY-####` |
| `test-scenarios.md` | `TS-PAY-####` |
| `decisions-approved.md` | `DEC-PAY-####` (settled/proposed) and `OD-PAY-####` (owner-decision-required) |
| `traceability-matrix.md` | Requirement → rule → AC → test → decision, plus the orphan check |

**All decisions are ruled; the package is buildable.** One build-site question is recorded OPEN in
[`domain-model.md`](domain-model.md) — the aggregate split for run lines. See the note there.
