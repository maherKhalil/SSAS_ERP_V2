# FP-014 — Proposed business rules

Written from the ruling set of 2026-08-25. Reads on from
[`domain-model.md`](domain-model.md), [`lifecycle-model.md`](lifecycle-model.md) and
[`authorization-model.md`](authorization-model.md).

---

## ⚠ THESE RULES ARE NOT IN THE MASTER REGISTER, AND PROMOTING THEM IS A SEPARATE ACT

**`docs/00-Master-Product-Specification/Business-Rules.md` contains no `BR-SUB` rule.** It carries
four business-rule prefixes — `BR-PLT`, `BR-HR`, `BR-GL`, `BR-ATT` — and
`Requirement-Numbering.md` lists the same four. `SUB` is in neither.

So every rule below is a **proposal for that file, not a citation from it.** Adding them is a
governing-document edit and an owner decision, exactly as `OD-SUB-0002`'s `REQ-SUB` prefix is, and
**this package does not perform it.** `OD-SUB-0002` ruled that the `REQ-SUB` prefix is added *at
ratification, not now*, following FP-013's precedent; the `BR-SUB` space follows the same rule.

### This is written here because it is precisely where the last two packages lost their rules

**`BR-PAY-0001`–`0013` and `BR-ATT-0001`–`0012` were both drafted inside their feature packages and
never promoted.** Both modules have shipped. The master `Business-Rules.md` still lists Payroll and
Attendance under *"Business Rules for the following modules will be added in future releases"* — for
modules that are in `main`, carrying migrations and test suites.

Nothing caught it, because a package that defines `BR-ATT` in its own `business-rules.md` looks
**complete** to a checker that reads inside the package. That gap is now closed: `trace-check.py`
check 7 reports both as `UNPROMOTED`, and would report `BR-SUB` the same way the moment this file
lands:

```
UNPROMOTED (2) - a package owns a BR space the master register does not carry:
  - BR-PAY - 13 rule(s) (BR-PAY-0001…BR-PAY-0013) defined in FP-012-payroll/business-rules.md,
    and the master Business-Rules.md carries no BR-PAY rule at all
  - BR-ATT - 12 rule(s) (BR-ATT-0001…BR-ATT-0012) defined in FP-013-attendance/business-rules.md,
    and the master Business-Rules.md carries no BR-ATT rule at all
```

**Expect a third row after this file merges. That is the check working, not a defect in this
package.** The owner has an open ruling on the whole class — 29 orphans and 22 untraced against 73
master identifiers — and promoting `BR-SUB` ahead of that ruling would settle by action a question
that is being decided deliberately.

**What this file guarantees is that the rules exist, are numbered contiguously from `0001`, and are
findable.** What it does not do is claim they are in force at the master level. They are not.

---

## Proposed rules

| ID | Rule | Basis |
|---|---|---|
| `BR-SUB-0001` | A tenant has **at most one subscription in force at any instant**. The record in force at instant `T` is the one with the greatest `EffectiveFromUtc` not later than `T` — derived by ordering, never stored | `OD-SUB-0008`; `REQ-SUB-0001` |
| `BR-SUB-0002` | A subscription record is **never modified and never deleted**. A plan change, a renewal and a billing-currency change are each a new record | `OD-SUB-0008`; the `EmployeePositionAssignment` convention |
| `BR-SUB-0003` | A new subscription record's `EffectiveFromUtc` is **strictly greater** than that tenant's current maximum. History is appended to, never inserted into | derived from `OD-SUB-0008` × `OD-SUB-0017`; see below |
| `BR-SUB-0004` | **No tenant-plane actor may create, amend or delete** a subscription, plan, grant or invoice, whatever permissions it holds | `OD-SUB-0013`; `ADR-005:248`; `ADR-015`; `REQ-SUB-0004` |
| `BR-SUB-0005` | An entitlement grant may only **raise**. Resolved entitlement is `plan ∪ grants` for modules and `max(plan, grants)` for every cap | `OD-SUB-0011`; `REQ-SUB-0010` |
| `BR-SUB-0006` | A metered quantity is judged against **the subscription record in force when the quantity was observed**, not against the record in force now | `OD-SUB-0008` × `OD-SUB-0017`; `REQ-SUB-0027` |
| `BR-SUB-0007` | A request to a route belonging to a module the tenant is not entitled to is **refused with `403`** before the handler runs | `BR-PLT-0008`; `OD-SUB-0006`; `REQ-SUB-0011` |
| `BR-SUB-0008` | **Platform-plane routes are never subject to module enablement** — authentication, tenant selection, refresh, logout, platform support and the subscription surface itself stay reachable | `ADR-021:207`; `REQ-SUB-0013` |
| `BR-SUB-0009` | A permission belonging to a module the tenant is not entitled to is **neither grantable nor effective**, so a stale role assignment cannot reach a disabled module | `OD-SUB-0012`; `REQ-SUB-0015` |
| `BR-SUB-0010` | Losing entitlement to a module **does not delete, alter or hide** the tenant's data in it. The data is unreachable, not destroyed, and returns intact on re-entitlement | `OD-SUB-0012`; `REQ-SUB-0016` |
| `BR-SUB-0011` | **Entitlement never appears in an access token** and is resolved server-side on every request | `FP-002` `authentication-model.md:16`; `DEC-SUB-0005`; `REQ-SUB-0008` |
| `BR-SUB-0012` | An entitlement change **takes effect without re-issuing a token and without restarting the host**. The cache is invalidated on change and never refreshed on a timer | `OD-SUB-0004`; `REQ-SUB-0009` |
| `BR-SUB-0013` | A tenant whose subscription term has **expired cannot log in**, and that refusal is **distinct** from a refusal on tenant status | `OD-SUB-0009`; `OD-SUB-0010`; `REQ-SUB-0018`, `REQ-SUB-0019` |
| `BR-SUB-0014` | Subscription state and `TenantStatus` are **orthogonal**. Expiry never writes `TenantStatus`, and no commercial reason is added to `TenantStatusChangeReason` | `OD-SUB-0010`; `REQ-SUB-0019` |
| `BR-SUB-0015` | A tenant with **no subscription record has no entitlement** and reaches no gated module. There is no default plan | `CON-0001`; `REQ-SUB-0007` |
| `BR-SUB-0016` | A plan is **retired, never deleted**, because historical subscription records reference it | the `PayElement` and `Account` precedent; `REQ-SUB-0028` |
| `BR-SUB-0017` | An **issued invoice is never edited**. A correction is a credit note, never an amendment | GL's posted-journal discipline; `REQ-SUB-0025` |
| `BR-SUB-0018` | An **invoice number is never reused**, including the number of a voided invoice | `REQ-SUB-0025` |
| `BR-SUB-0019` | A tenant may read **which modules it has**; it may not read price, invoice, payment state or any other commercial term | `REQ-SUB-0021`; the `FP-002` disclosure precedent |
| `BR-SUB-0020` | **No cardholder datum is stored, transmitted in any request or response, or logged** by this package | `OD-SUB-0016`; boundary held for `T-010` |

Twenty rules, `BR-SUB-0001`–`BR-SUB-0020`, contiguous.

---

## The two that are not obvious, and why they are rules rather than implementation notes

**`BR-SUB-0003` — the monotonic append.** It looks like a database detail and it is a business rule,
because of what it protects. `BR-SUB-0006` judges a metered quantity against the record in force when
it was observed. If a record could be appended *behind* the present, an operator could change what
was in force in March by writing a row today, and every overage already judged and invoiced would
silently refer to a different plan. **The rule is what makes `BR-SUB-0006` mean anything**, and it
falls out of `OD-SUB-0008` and `OD-SUB-0017` meeting — neither ruling states it alone.

**`BR-SUB-0005` — additive is a shape, not a check.** Stated as a rule because the resolution
function's `max(plan, grants)` makes it structurally impossible to violate, and a reader who sees no
`CHECK` constraint and no validation branch could reasonably conclude it is unenforced. It is
enforced twice: refused at write so the mistake is loud, and unable to take effect at read even if a
row existed.

---

## Rules that would be needed and cannot be written yet

Named as absences rather than guessed, in the manner FP-013 used for accrual and device capture.

- **Nothing on tax.** Multi-currency invoicing from a vendor to tenants crosses jurisdictions and no
  document in this repository names one. `DEC-PAY-0016` is the standing precedent for refusing to
  encode a jurisdiction the product has not named, and it is followed. An invoice total is the sum of
  its lines.
- **Nothing on dunning, refunds or credit notes.** The ruling set names all three as consequences of
  `OD-SUB-0016` with no requirement covering them; `REQ-SUB-0025` and `REQ-SUB-0026` are one line
  each. A rule written now would be a requirement invented in a business-rules file.
- **Nothing on what happens to a seat over cap.** `OD-SUB-0017` ruled caps are enforced alongside
  module enablement, but whether exceeding a seat cap **blocks the next login** or **bills an
  overage** is not ruled, and the two are very different products. `BR-SUB-0006` states how the
  quantity is judged; it deliberately does not state the consequence. **This is the one gap in the
  ruling set that a build could stumble into**, because both readings are consistent with
  "seats plus limits".
- **Nothing on grace.** Whether an expired subscription has a grace period before `BR-SUB-0013` bites
  is unauthored. The ruling is that expiry blocks login; a grace period would be a second ruling.

---

## The interaction that needs stating plainly

**`BR-SUB-0012` and `BR-SUB-0013` pull in opposite directions, and a naive cache satisfies neither.**

`BR-SUB-0012` requires an entitlement change to take effect immediately, which `OD-SUB-0004` answered
with invalidation-on-change rather than a TTL. **But expiry — `BR-SUB-0013` — writes nothing.** No
row changes when a term lapses; the clock passes `TermEndUtc` and the answer is simply different from
then on.

So there is **no invalidation event to hang expiry on**, and a cached value holding only
"enabled: true" is wrong from the instant of expiry and stays wrong until something unrelated evicts
it. The cached entry must carry the term and be evaluated against the clock on read, or be keyed so
it cannot outlive `TermEndUtc`.

**And invalidation on a plan edit fans out.** A plan is shared, so amending its modules or limits
changes the entitlement of **every tenant whose in-force record names it**. An invalidation keyed
only on `TenantId` leaves all of them stale — the one case where `BR-SUB-0012` fails without anyone
touching the tenant it fails for.
