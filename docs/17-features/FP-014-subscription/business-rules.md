# FP-014 — Business rules

Written from the ruling set of 2026-08-25. Reads on from
[`domain-model.md`](domain-model.md), [`lifecycle-model.md`](lifecycle-model.md) and
[`authorization-model.md`](authorization-model.md).

---

## THESE RULES ARE NOW IN THE MASTER REGISTER — PROMOTED AT RATIFICATION BY T-022

**`docs/00-Master-Product-Specification/Business-Rules.md` now carries `BR-SUB-0001`–`BR-SUB-0021`,
and `Requirement-Numbering.md` registers both `REQ-SUB-0001` and `BR-SUB-0001`** (T-022, 2026-08-25).

**Until then — while this package was being drafted — neither file carried `SUB` at all.** The master
register held four business-rule prefixes, `BR-PLT`, `BR-HR`, `BR-GL` and `BR-ATT`, and
`Requirement-Numbering.md` listed the same four. **The reasoning below is kept because it is still the
argument for why promotion belongs to ratification** (`DEC-L-027`: change the tense, not the content).

While this package was being drafted, every rule below was a **proposal for that file, not a citation
from it.** Adding them was a governing-document edit and an owner decision, exactly as `OD-SUB-0002`'s
`REQ-SUB` prefix was, and **this package did not perform it while drafting.** `OD-SUB-0002` ruled that
the `REQ-SUB` prefix is added *at ratification, not before*, following FP-013's precedent; the
`BR-SUB` space followed the same rule, and **ratification then performed both.**

### This is written here because it is precisely where the last two packages lost their rules

**`BR-PAY-0001`–`0013` and `BR-ATT-0001`–`0012` were both drafted inside their feature packages and
never promoted.** Both modules have shipped. The master `Business-Rules.md` still lists Payroll and
Attendance under *"Business Rules for the following modules will be added in future releases"* — for
modules that are in `main`, carrying migrations and test suites.

Nothing caught it, because a package that defines `BR-ATT` in its own `business-rules.md` looks
**complete** to a checker that reads inside the package. That gap is now closed: `trace-check.py`
check 7 reports both as `UNPROMOTED`, and reported `BR-SUB` the same way until T-022 promoted it.
**What check 7 says today — two rows, not three:**

```
UNPROMOTED (2) - a package owns a BR space the master register does not carry:
  - BR-PAY - 13 rule(s) (BR-PAY-0001…BR-PAY-0013) defined in FP-012-payroll/business-rules.md,
    and the master Business-Rules.md carries no BR-PAY rule at all
  - BR-ATT - 12 rule(s) (BR-ATT-0001…BR-ATT-0012) defined in FP-013-attendance/business-rules.md,
    and the master Business-Rules.md carries no BR-ATT rule at all
```

**A third row — `BR-SUB`, 21 rules — appeared when this file landed, and ratification closed it.**
What that row meant changed while this package was being written, and the distinction is worth stating
precisely because it is the difference between a working check and an accepted defect:

- **Before ratification it is the check working.** Promotion is an act of ratification, not of
  drafting — `OD-SUB-0002` ruled the `REQ-SUB` prefix is added at ratification and the `BR-SUB` space
  follows the same rule. A package that promoted its own rules while drafting them would be editing a
  governing document on its own authority.
- **After ratification the same row is a ratification defect.** `DEC-L-012` closed the wider question
  — 29 orphans and 22 untraced against 73 master identifiers — and closed it **forwards**: every
  package promotes its `BR-` rules and cites the constraints it satisfies **at ratification**, with
  `trace-check` check 7 as the mechanism. An `UNPROMOTED` row that survives ratification is a defect
  in the ratification, not a cost the product has agreed to carry.

**So this is promotion work that ratification must do, not a backlog item it may inherit.** The
existing `BR-PAY` and `BR-ATT` rows are on different terms: `DEC-L-012` pays those per module when
each module is next touched, rather than in a sweep, because a 51-identifier diff across documents
nobody is changing would land unread.

**What this file guarantees is that the rules exist, are numbered contiguously from `0001`, and are
findable.** Until ratification it did not claim they were in force at the master level. **They now
are** — T-022 promoted all twenty-one, and T-028 amended `BR-SUB-0020`'s scope in both copies at once
so the source and the promoted copy cannot drift.

---

## The rules

| ID | Rule | Basis |
|---|---|---|
| `BR-SUB-0001` | A tenant has **at most one subscription in force at any instant**. The record in force at instant `T` is the one with the greatest `EffectiveFromUtc` not later than `T` — derived by ordering, never stored | `OD-SUB-0008`; `REQ-SUB-0001` |
| `BR-SUB-0002` | A subscription record is **never modified and never deleted**. A plan change, a renewal and a billing-currency change are each a new record | `OD-SUB-0008`; the `EmployeePositionAssignment` convention |
| `BR-SUB-0003` | A new subscription record's `EffectiveFromUtc` is **strictly greater** than that tenant's current maximum. History is appended to, never inserted into | derived from `OD-SUB-0008` × `OD-SUB-0017`; see below |
| `BR-SUB-0004` | **No tenant-plane actor may create, amend or delete** a subscription, plan, grant or invoice, whatever permissions it holds | `OD-SUB-0013`; `ADR-005` § Platform Administration (`:248`); `ADR-015`; `REQ-SUB-0004` |
| `BR-SUB-0005` | An entitlement grant may only **raise**. Resolved entitlement is `plan ∪ grants` for modules and `max(plan, grants)` for every cap | `OD-SUB-0011`; `REQ-SUB-0010` |
| `BR-SUB-0006` | A metered quantity is judged against **the subscription record in force when the quantity was observed**, not against the record in force now | `OD-SUB-0008` × `OD-SUB-0017`; `REQ-SUB-0027` |
| `BR-SUB-0007` | A request to a route belonging to a module the tenant is not entitled to is **refused with `403`** before the handler runs | `BR-PLT-0008`; `OD-SUB-0006`; `REQ-SUB-0011` |
| `BR-SUB-0008` | **Platform-plane routes are never subject to module enablement** — authentication, tenant selection, refresh, logout, platform support and the subscription surface itself stay reachable | `ADR-017` § Platform database boundary (`:169`), `:376`–`:378` (**amended by `DEC-L-024`**, formerly `ADR-021` § 10 Outage behaviour (`:207`)); `REQ-SUB-0013` |
| `BR-SUB-0009` | A permission belonging to a module the tenant is not entitled to is **neither grantable nor effective**, so a stale role assignment cannot reach a disabled module | `OD-SUB-0012`; `REQ-SUB-0015` |
| `BR-SUB-0010` | Losing entitlement to a module **does not delete, alter or hide** the tenant's data in it. The data is unreachable, not destroyed, and returns intact on re-entitlement | `OD-SUB-0012`; `REQ-SUB-0016` |
| `BR-SUB-0011` | **Entitlement never appears in an access token** and is resolved server-side on every request | `FP-002` `authentication-model.md:16`; `DEC-SUB-0005`; `REQ-SUB-0008` |
| `BR-SUB-0012` | An entitlement change **takes effect without re-issuing a token and without restarting the host**. The cache is invalidated on change and never refreshed on a timer | `OD-SUB-0004`; `REQ-SUB-0009` |
| `BR-SUB-0013` | A tenant whose subscription term has **expired reaches no gated module, and still authenticates**. Expiry never refuses a login; a refusal on tenant status still does, and the two remain **distinct** | `OD-SUB-0009` as amended by `DEC-L-033`; `OD-SUB-0010`; `REQ-SUB-0018`, `REQ-SUB-0019` |
| `BR-SUB-0014` | Subscription state and `TenantStatus` are **orthogonal**. Expiry never writes `TenantStatus`, and no commercial reason is added to `TenantStatusChangeReason` | `OD-SUB-0010`; `REQ-SUB-0019` |
| `BR-SUB-0015` | A tenant with **no subscription record has no entitlement** and reaches no gated module. There is no default plan | `CON-0001`; `REQ-SUB-0007` |
| `BR-SUB-0016` | A plan is **retired, never deleted**, because historical subscription records reference it | the `PayElement` and `Account` precedent; `REQ-SUB-0028` |
| `BR-SUB-0017` | An **issued invoice is never edited**. A correction is a credit note, never an amendment | GL's posted-journal discipline; `REQ-SUB-0025` |
| `BR-SUB-0018` | An **invoice number is never reused**, including the number of a voided invoice | `REQ-SUB-0025` |
| `BR-SUB-0019` | A tenant may read **which modules it has**; it may not read price, invoice, payment state or any other commercial term | `REQ-SUB-0021`; the `FP-002` disclosure precedent |
| `BR-SUB-0020` | **No cardholder datum is stored, transmitted in any request or response, or logged** anywhere in SSAS | `OD-SUB-0016`; `ADR-029`. **Amended 2026-08-25** — the scope read "by this package" until `ADR-029`, written after this rule, ruled the boundary product-wide. The prohibition is unchanged |
| `BR-SUB-0021` | A seat cap is enforced **at admission and nowhere else**. Creating or activating a user beyond the tenant's resolved cap is refused **at that moment**, naming the cap, the current count and the plan. **Login is never refused for a seat cap.** An excess arriving by plan downgrade is **billed and reported**, never enforced against anyone already working | `DEC-L-009` closing the `OD-SUB-0017` residue; `REQ-SUB-0027` |

Twenty-one rules, `BR-SUB-0001` through `BR-SUB-0021`, contiguous.

---

## The three that are not obvious, and why they are rules rather than implementation notes

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

**`BR-SUB-0021` — the enforcement point, and why it is not `BR-SUB-0006` with more words.**
`BR-SUB-0006` says how a metered quantity is **judged**: against the subscription record in force
when it was observed. `BR-SUB-0021` says where a cap is **enforced**. They are different statements
about different moments, and neither implies the other — a product could judge historically and still
enforce at login, which is exactly the design `DEC-L-009` rejected.

**THERE IS NO LONGER AN ASYMMETRY HERE, AND `DEC-L-033` IS WHAT COLLAPSED IT.**

This paragraph used to explain why expiry blocked login while a seat cap did not, and set the two side
by side to show the events were not alike. **`DEC-L-033` (2026-08-26) amended `OD-SUB-0009`: expiry now
gates modules and never blocks login.** So **no commercial event blocks authentication at all**, and the
rule is uniform rather than asymmetric:

| | Expiry — `BR-SUB-0013` | Seat excess — `BR-SUB-0021` |
|---|---|---|
| Blocks authentication | **no** | **no** |
| Where it bites | every gated module, for the whole tenant | at the grant, when a user is created or activated |
| When it arrives | on a **dated** term everyone could see coming | incrementally, as users are added |
| Who can resolve it | the tenant's administrator, by renewing | the administrator who caused the excess |

**The original reasoning is preserved because it is why the rule is what it is.** The two events were
never alike, and `DEC-L-009` refused a login block for a seat cap on grounds that applied just as well
to expiry once the owner looked at it again: **a lapsed customer who cannot sign in cannot reach the
page that would let them subscribe.** The asymmetry was the argument that survived one of the two
cases; the amendment removed the other.

Blocking a login for a seat cap would enforce against an arbitrary user who did nothing, at the
moment they sat down to work, in an ERP of record — converting a commercial disagreement into an
operational outage for someone with no power to end it. **Refusing at admission tells the person who
caused the excess, immediately and specifically, and they can act on it.**

**No grace period exists for the seat cap, and none is needed.** A grace period softens a lapse; with
the cap enforced at admission nothing lapses.

**This paragraph used to end by calling expiry "the only commercial event that blocks a login". After
`DEC-L-033` no commercial event blocks a login at all** — expiry gates modules instead. What survives
is the distinction that mattered: expiry is **dated, foreseeable and whole-tenant**, and a seat excess
is incremental and arbitrary in who it would hit. That is still why one is enforced at the gate and
the other at the grant.

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
- **Nothing on grace, and the amendment makes it matter less.** Whether an expired subscription has a
  grace period before `BR-SUB-0013` bites is still unauthored. But `DEC-L-033` moved the bite from
  authentication to the gated modules, so a lapse no longer locks a tenant out of the surface it would
  renew from — which is most of what a grace period exists to soften. A grace period would still be a
  second ruling; it is now a smaller one.

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
