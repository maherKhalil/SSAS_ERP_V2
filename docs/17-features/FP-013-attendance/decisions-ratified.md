# FP-013 — Ratified decisions

**All sixteen `OD-ATT` rulings, owner-closed 2026-08-25. All fourteen `DEC-ATT` ratified.**
[`decisions-open.md`](decisions-open.md) remains as the record of what was asked and why; this file is what
was answered. **Where the two disagree, this file wins.**

---

## The rulings

| # | Ruling |
|---|---|
| **`OD-ATT-0001`** scope | **BOTH, sequenced.** Attendance core first, leave's entitlement ledger second. **One module, one calendar, one approval shape.** |
| **`OD-ATT-0002`** identifier space | **`REQ-ATT` created by this ratification** — the prefix added to `Requirement-Numbering.md` (functional and business-rule registers) and the catalog written as `Requirement-Catalog/ATT.md`, following GL.md and PAY.md. |
| **`OD-ATT-0003`** capture model | **Daily records**, the package's opinion. Not clock events. |
| **`OD-ATT-0004`** calendar | **Company-owned**, weekly pattern as data plus a dated holiday list. |
| **`OD-ATT-0005`** leave types | **Configurable catalog** with a closed behaviour enum, the `PayElement` precedent. |
| **`OD-ATT-0006`** accrual | **Balances ADMINISTERED. Accrual rules deferred.** |
| **`OD-ATT-0007`** approval | **Department-manager approval.** Self-approval **barred**. Parent-chain escalation for unmanaged and self-referential departments. **Permission-holder fallback at the root.** |
| **`OD-ATT-0008`** overtime | **RECORDED with a tier label.** Every rate stays in Payroll. |
| **`OD-ATT-0009`** contract | **Per-period summary.** The caller names a date, the module resolves the period. **No straddles.** |
| **`OD-ATT-0010`** close discipline | **(a).** Periods close, and **Payroll refuses an open one**, via an `Inspect` method returning a **closed-enum** outcome — the `InspectPostingWindowAsync` pattern. |
| **`OD-ATT-0011`** branch | **THE SPLIT** — see below. |
| **`OD-ATT-0012`** corrections | **New adjustment records, never edits.** |
| **`OD-ATT-0013`** self-service | **DEFERRED.** No identity→employee assumption anywhere. **Third time deferred; now a recorded future package.** |
| **`OD-ATT-0014`** module home | **Own module**, `src/Modules/Attendance/`. |
| **`OD-ATT-0015`** `OD-PAY-0007` | **Payroll proration UNCHANGED — calendar days.** The lever is recorded as untaken. |
| **`OD-ATT-0016`** devices | **OUT.** |

---

## `OD-ATT-0011` — the split, and why the hole in it is intended

The package raised an asymmetry it could not resolve: **branch-owning attendance protects supervisor reads
but risks payroll completeness**, and those pull opposite ways. The ruling takes both halves rather than
trading one away.

- **Attendance records are `IBranchOwnedEntity`.** The write boundary stamps `BranchId` from the execution
  context, and supervisor reads are branch-scoped exactly as `Employee` is.
- **The Payroll summary contract is DELIBERATELY BRANCH-BLIND and company-complete.** It aggregates every
  branch, because a payroll run that silently omitted a branch's employees is the failure `DEC-PAY-0017`
  refused.

**The hole is ruled INTENDED**: a caller who cannot read a branch's records through the HTTP surface can
still see that branch's hours reflected in a company payroll total. That is the point — payroll is a
company-level act and must be company-complete.

**Three obligations follow, and none is optional:**

1. **Stated at the site.** The contract implementation carries the reasoning where the branch filter is
   *not* applied, so the absence reads as a decision rather than an oversight.
2. **Guard-asserted.** An architecture test asserts the contract's query applies no branch predicate.
3. **Live resolution.** Branch authority is resolved live from `ITenantBranchAccessResolver` at scope
   construction — the `RosterScoped` pattern — never cached and never taken from a token.

### `DEC-ATT-0014` — classification asserted entity by entity

**Every** Attendance entity carries an explicit branch-classification assertion in the architecture tests,
**including the ones that are NOT branch-owned.** The HR pattern, not the Payroll one.

Payroll's entities are tenant-global **by omission** — no test says so. That is precisely what this
obligation forbids, because the failure mode is silent: an entity that should have been branch-scoped and
was not is readable by every branch in the tenant, and nothing about it looks wrong.

---

## Scope addition — the Payroll-side consumption ships in this feature

**Architect ruling.** FP-012 implemented the *provider* side inside GL when Payroll needed it; this feature
implements the *consumer* side inside Payroll. The mirror of the same precedent.

Delivered here:

- **New attendance-driven `PayElementBehaviour` members** — overtime by tier (× rates configured in
  Payroll) and unpaid-absence deduction.
- **Reading ONLY through the summary contract.** No Attendance assembly reference, no table access.
- **Refusing an open attendance period at approval**, per `OD-ATT-0010`.
- **`No_attendance_driven_behaviour_exists_because_attendance_is_unbuilt` is REPLACED**, per
  `DEC-ATT-0012`'s pattern. Its successor asserts the behaviours **exist** and reach attendance **only via
  the contract**.

**A green suite obtained by deleting the test that went red is not a green suite.**

This closes `REQ-ATT-0022` inside FP-013 rather than deferring it, and it makes the full
**HR → Attendance → Payroll → GL** chain executable for the first time.

---

## What the rulings changed in the package

| Package statement | Ruled outcome |
|---|---|
| `OD-ATT-0001` conditional scope columns `A`/`B`/`*` | **all in force** — every requirement, criterion and scenario ships |
| `OD-ATT-0012`-dependent aggregate fork (three shapes) | **append-only from creation**; an adjustment is another append-only row |
| The attendance-record unique index question | **no unique `(Tenant, Employee, Date)`** — a second row for the same employee-date is exactly what an adjustment is |
| `OD-ATT-0011`-dependent `BranchId` columns | **present on attendance records**; absent from calendars, leave types, balances and requests |
| `REQ-ATT-0023` self-service | **BLOCKED and deferred**; no `ViewOwn` permission exists, asserted |
| `OD-ATT-0015` | **no change to Payroll proration** — recorded, not acted on |

### Reopen survives the `OD-ATT-0012` ruling, and here is why it is safe

`decisions-open.md` drew the reopen arrow as existing only under ruling (b). The ruling was (a), yet the
build carries a **reopen** action — which needs its justification stated rather than assumed.

**Because records are append-only from creation, reopening a period permits *appending*, never *editing*.**
The two are not in tension: `PreventAppendOnlyMutation` refuses `Modified` and `Deleted` unconditionally, so
a reopened period cannot be used to rewrite history no matter who holds the permission. Reopen is an
administrative act that lets more facts arrive; it is not an eraser.

Payroll still refuses an open period at approval (`OD-ATT-0010`), so reopening a period a run has consumed
does not silently invalidate a posted journal — it blocks the *next* approval until the period closes again.

---

## Two divergences in the master documents, named rather than silently resolved

**1. `BR-ATT` rule text is not copied into `Business-Rules.md`.** GL added its rules there; **Payroll did
not** — `Business-Rules.md` contains six `BR-GL` mentions and **zero** `BR-PAY`. FP-013 follows the more
recent PAY precedent: the identifier prefix is registered in `Requirement-Numbering.md`, and the rule text
lives in this feature package's [`business-rules.md`](business-rules.md).

**The two precedents genuinely disagree, and that is the owner's to settle, not this build's.**

**2. `Business-Rules.md` still lists Attendance — and Payroll — under "Future Modules."** Payroll shipped in
FP-012 and the line was never removed. **Not corrected here**: editing a governing document beyond the
ratified `REQ-ATT` addition would be scope this feature was not given. Flagged so it is a known
inconsistency rather than an unnoticed one.
