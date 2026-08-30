# Open decisions for the owner — assembled 2026-08-28 (T-130)

**15 items** that engineering cannot settle on its own — **eleven ERP (1-11) and four HIS (12-15)**. Each
carries **what it is**, **the measured facts**, **what it blocks**, and **the options**. Where the call is
genuinely the owner's there is no recommendation.

⚠ **This count is derived from the headings, not incremented.** The line it replaces said *"Nine items"*
while the file held eleven — **the header is a second summary of the body, and it went stale the moment an
entry was appended without it.**

**12-15 were added 2026-08-30 because they existed only in `scripts/his-catalogue/MIGRATION-PLAN.md`** — a
window reading this file alone would have concluded there were eleven decisions when there are fifteen.
**Their analysis stays in the plan and is pointed at rather than copied**; what lives here is the question
and what it costs.

**Every number here was re-verified against the tree today.** Three items had moved since they were
recorded, and one had moved enough to change what the decision is about. Those are marked ⚠ **CHANGED**.

---

## 1. Overtime tiers — ⚠ CHANGED, and the decision is a different one than recorded

**What it was recorded as.** *"`PayElement.OvertimeTier` — four layers, a fifth never written."*

**⚠ That is stale. The tier is wired end to end and it does price.**

```
AttendanceRecord.OvertimeTier          captured, validated, persisted
AttendanceSummaryResult                summed per tier into OvertimeQuantityByTier
PayrollRunCommandHandlers              passed to the calculator
PayrollCalculator.OvertimeQuantity     element.OvertimeTier -> quantities[tier] -> priced at the element rate
```

**What is missing is not a layer. It is a shared VOCABULARY**, and the gap is a money path:

```
AttendanceRecord   trims, does not case-fold
PayElement         trims, does not case-fold      (the same rule, written separately)
the match          TryGetValue, StringComparer.Ordinal — CASE-SENSITIVE
```

**Neither side case-folds, and the match is case-sensitive.** A record tagged `"Night"` against an element
tagged `"NIGHT"` **does not match — and the lookup returns `0m`.** The employee is paid **no overtime for
that tier, silently**: no error, no warning, and a payslip that looks complete.

**Every test on both sides used the literal `"NIGHT"`**, so the mismatch was covered nowhere.

**✅ FIXED (T-131), and it needed no decision from you.** Both sides now normalise through one shared rule —
trim then upper-case, the same treatment leave-type codes and calendar names already had. A test now covers
the mismatched-case cases, and reverting the fix fails it.

**What it blocks.** Nothing today — one tier spelled consistently works. It is a latent money defect that
surfaces the first time two people type the same tier differently.

**⚠ THIS IS TWO PROBLEMS AND ONLY ONE OF THEM IS YOURS.**

**The accidental half is fixed and needed no decision.** One rule — how a tier is normalised — was written
twice, and **the half neither copy implemented was case.** Nobody chose that. Both sides now share one rule,
so **a tier typed in any case matches.**

**The half that IS yours: what should happen when a tier genuinely has no matching pay element.** A record
can still carry a tier no element prices — because someone invented a tier, or the element was retired.
**That still pays zero, silently, and no amount of normalising changes it.**

**The options.**
- **Refuse the run** — payroll fails and names the unmatched tier. Nobody is underpaid, but a single bad
  record blocks the whole run until someone fixes it. *This is what the product already does for a
  comparable contradiction (`AttendanceContradictsEmployment`), which is precedent, not a decision.*
- **Pay zero and report** — the run completes and lists what it could not price. Nothing is blocked; someone
  has to read the report for the underpayment to be caught.
- **A tier catalog** — companies define their tiers once, and records and elements can only reference an
  existing one. Removes the possibility rather than handling it, at the cost of a setup step.
- **Keep paying zero silently** — with the hazard recorded and accepted.

---

## 2. Platform's administration transport — ⚠ CHANGED (it is roughly twice the task it was)

**What it is.** Three whole administrative surfaces exist as domain logic and application handlers with no
HTTP routes: **tenants, roles, and users beyond de/reactivation.**

**The measured facts.**

```
Platform permissions catalogued                            28
  of those, required by no route                           16
Platform.Application handlers                              65
  named nowhere in SSAS.Platform.API                       29   (a FLOOR — see below)
TenantStorageErrors codes declared                        117
  returned somewhere in src/                              115
  mapped to an HTTP status by any mapper                     0
```

**The 29 is a floor**, not an exact count: the measurement counts a handler as "routed" if its name appears
anywhere in the API assembly, including in a comment. The true number is 29 or higher.

**⚠ What changed.** This was carried for a fortnight as *"16 permissions and ~23 handlers"*. The handler
count is **29, not ~23**, and the mapping half was not in the record at all. **No file in
`SSAS.Platform.API` names a single `TenantStorage.` error code.** On the day this transport is built, **115
business refusals arrive with no HTTP status** and fall through to `500` — no exception, no log entry, and
handlers that read correctly. That failure has been found twice this fortnight (T-118, T-125) at one
instance each.

**What it blocks.** Any self-service administration. Today a tenant, role, or user change requires
engineering.

**The options.**
- **Build it** — the routes plus 115 status decisions. The second half is the larger piece.
- **Build a slice** — e.g. tenants only, and accept that roles and users stay manual.
  **⚠ Corrected 2026-08-29 (T-155, T-158): a tenants slice needs about SEVEN error mappings, not 115.**
  The 115 belong to storage administration — backup, restore, cutover — which tenant lifecycle never
  touches. **And the tenant slice is separately blocked by a recorded deferral; see the note in item 1.**
- **Defer knowingly** — it works today via engineering; the cost is engineering time per change.

---

## 3. Employment type — ⚠ BASIS CORRECTED 2026-08-29 (T-153, T-158). Built; the question survives.

**⚠ What changed.** This item said employment type does not exist. **It does** — shipped on the owner's
ruling: full time is monthly, part time is daily or hourly, contract takes no compensation record.
`Employee` carries the field and the assumption guards now number four, not three.

**The decision is unchanged and its stated basis was wrong.** The type lives on the **command path**, read
once when compensation is recorded, and **never reaches a calculation**. So a part-timer is expressible in
HR and **payroll still cannot tell one from a full-timer**.

**The question, precisely:** should the calculation itself use employment type — proration, accrual, anything
that should differ for a part-timer — or is expressing it at the compensation boundary enough?

**What it is.** There is **no employment-type concept anywhere in HR.** `Employee` has no field for it. Every
payroll calculation assumes one shape of employment.

**The measured facts.** `EmploymentTypeAssumptionTests` carries **three guards** whose only job is to fail
when someone adds an input that would silently assume full-time employment — the guards exist precisely
because the concept does not.

**What it blocks.** The first non-full-time hire. A part-timer, a fixed-term contractor, or an intern would
be paid on full-time assumptions with nothing detecting it.

**The options.**
- **Add it before the first such hire** — it reaches Employee, the calculator, and attendance proration.
- **Decide the business will not have non-full-time staff**, and the guards become permanent.
- **Accept the risk knowingly** with a manual check at hiring.

---

## 4. Self-service grants — ⚠ MECHANISM CORRECTED 2026-08-29 (T-158). There is no per-user grant.

**⚠ What changed.** This item described *"per-user permission grants, one person at a time"*. **No such
mechanism exists in this product.** `TenantUser` holds role assignments and nothing else; there is no
`TenantUserPermission` or `UserPermissionGrant` entity anywhere in `src/`. **Permissions reach a user only
through a role** — so the option this item offered as an improvement, *"a bulk grant by role"*, is already
the only mechanism there is.

**What is actually absent** is assigning a role to many users at once, or a role granted by default at hire.
**The three self-service permissions are confirmed as recorded, and none appears in any seeded role.**

**What it is.** Employee self-service is gated by per-user permission grants, with **no bulk mechanism**.
Granting it to a workforce means granting it one person at a time.

**⚠ The measured facts — the recorded count was six.** There are **three**:

```
Attendance.Records.ViewOwn
Attendance.Leave.ViewOwn
Payroll.Payslips.ViewOwn
```

**What it blocks.** Rolling self-service out to more than a handful of staff.

**The options.**
- **A bulk grant** — by role, department, or "all active employees".
- **Grant by default at hire**, so the question never arises per-person.
- **Keep it manual** if self-service stays limited to a few people.

---

## 5. `POST /api/attendance/records/bulk` — ⚠ REWEIGHTED 2026-08-29 (T-157). It was never specified.

**What it is.** A bulk attendance-record route that exists as **one row in a route table and nothing else**.

**⚠ What changed.** This item previously read *"specified in FP-013's api-contracts and never built"*. That
is literally accurate and it invites you to picture a specification. **There is none.** Measured:

```
"bulk" across all thirteen FP-013 documents        3 occurrences, all in api-contracts.md,
                                                   all the same route-table row plus its note
requirements.md / acceptance-criteria.md /
test-scenarios.md / traceability-matrix.md         zero — no REQ, no AC, no TS names it
anything doing it under another name               none; RecordAttendanceCommand is strictly singular
```

**Compare item 1's tenant transport: ten criteria, fifteen scenarios, an ADR and a decision id.**

**And the block it sits in lost five of its six other arguments.** That api-contracts section was written as a
**proposal before the module shipped**, and nothing compared the two until the route inventory was built. The
comparison found six divergences — two paths carrying an id the live routes do not take, three routes built
and never documented — and **all five were corrected to the code.** `/records/bulk` is the sole survivor, and
it survived **not because it was validated but because it was the one claim with no code to contradict it.**

**It is not deferred.** No guard, no criterion, no scope statement defers it; the document explicitly
declined to decide, saying *"whether it is still wanted is the owner's call."*

**What it blocks.** Importing attendance from a device or a spreadsheet. Today each record is one request.
**Whether that is wanted is the only real question here, and nothing found supports or contradicts it.**

**The options.** Build it; delete the row and record that attendance arrives one record at a time; or leave
it as a known gap — now correctly weighted as a proposal nobody has ruled on rather than as unbuilt work.

---

## 6. The hours-per-day factor — ✅ NO DECISION NEEDED NOW (T-194). Absent and unclaimed.

**What it is.** There is **no hours-per-day constant anywhere in `src/` or `docs/`.** It was raised, held,
and no consumer has appeared under the owner's own model of the business.

**What it blocks.** Nothing observed. It would matter if an hourly employee's entitlement had to be
expressed in days, or a daily employee's in hours.

**The options.** Leave it absent until something needs it; or define it now if the business already thinks
in both units.

---

## 7. Calendar resolution — ✅ NO DECISION NEEDED NOW (T-194). Duplicated and unpinned; blocks nothing while companies have one calendar each.

**⚠ What changed.** The inference is not only undecided — it is **written twice and enforced nowhere.**
`IsDefault` descending then `NormalizedName` appears at `AttendanceRepositories.cs:46` **and again** at
`AttendanceReadService.cs:347`. **They agree today. No test pins the order.** And the requirement that makes
it matter — that two calls must not return different calendars and therefore different day counts — **is
stated in only one of the two.** The read path silently depends on the write path's comment.

**What it is.** When a company has more than one working calendar, the code picks one by
**`IsDefault` first, then by name** — chosen for determinism, so the answer is never arbitrary.

**That is an inference, not a ruling.** Nobody has said what *should* happen when a company has several
calendars, and **the same inference governs both leave and pay.**

**What it blocks.** Nothing today, while companies have one calendar each.

**The options.** Ratify the current rule; assign calendars per employee or per department; or forbid more
than one calendar per company and delete the ambiguity.

---

## 8. Period versus pay date in run inclusion — ✅ NO DECISION NEEDED NOW (T-194). Deliberate; recorded so the results do not look wrong.

**What it is.** Two different dates decide two different questions, and the split is real:

```
which employees and records are in the run   the PERIOD's bounds
which compensation is in force               the period's PayDateUtc
```

**`PayDateUtc` is separate from `EndUtc` because the date determining the fiscal period for posting is not
the date the period ends.** The code says so and the domain enforces `PayDateBeforePeriod`.

**What it blocks.** Nothing. It is recorded here because it produces results that look wrong to someone who
assumes one date governs both — a raise dated between period end and pay date applies, while attendance
from the same days does not.

**The options.** Confirm it; or change one side, which is a payroll-semantics decision with back-dating
consequences.

---

## 9. `Company.BaseCurrencyCode` is stored non-Unicode — ✅ NO DECISION NEEDED NOW (T-194). Added 2026-08-28 (T-134).

**What it is.** Currency codes are persisted as **`char(3)` under an ordinal collation**, not Unicode. Every
other column holding ERP data is Unicode.

**The measured facts.** The product has **five** non-Unicode string columns. Four are localization plumbing —
culture codes (`varchar(2)`), a text-format token, a change-type token. **`Company.BaseCurrencyCode` is the
only one in the ERP's own data.**

**Why it is defensible today.** ISO 4217 defines currency codes as three uppercase ASCII letters, so `char(3)`
holds every legal value. **The engineer who flagged it wrote the caveat themselves:** *"it is always ASCII" is
an argument that ages badly, and this one is nearer the ERP's own data than a culture code is.*

**What it blocks.** Nothing today. It matters if the product ever stores something in that column that is not
an ISO 4217 code — a local or historical currency designation, or a symbol.

**What it costs to change.** **A tenant migration**, which is why it was recorded for a decision rather than
altered by the change that found it.

**The options.**
- **Leave it** — ISO 4217 is a real standard and the column holds a code, not a name.
- **Widen it to `nvarchar(3)`** — a tenant migration now, while the data is small.
- **Decide when a non-ISO currency is actually needed** — cheapest today, most expensive if it arrives with
  live data behind it.

---

## 10. Overlapping leave requests under concurrency — ✅ RESOLVED, NOTHING TO DECIDE (T-194)

**⚠ This shipped and the list did not say so.** `ILeaveSubmissionLock` is registered in production
(`SSAS.Attendance.Infrastructure/ServiceCollectionExtensions.cs`), `SubmitLeaveRequestCommandHandler` takes
it, and `AttendanceOverlapChainSqlServerTests` proves it against real SQL Server with three tests: the lock
refuses without an open transaction, a second submission for the same employee on a SECOND CONNECTION
cannot take it, and a submission for a different employee is not blocked. A database unique index refuses a
second identical active request besides.

**Struck rather than deleted, because the entry below records why it was weighed differently from the
fiscal-year and attendance-period guards** — a leave request is self-service and submitted whenever an
employee likes, so the rarity argument that justified accepting the other two never applied here. That
reasoning is what produced the lock and is worth keeping.

---

<details><summary>The original entry, for the record</summary>

### 10. Overlapping leave requests are possible under concurrency — added 2026-08-29 (T-146, T-148)

**What it is.** Two leave requests for the same employee covering the same days can both be accepted, if
they are submitted close enough together. Overlapping approved leave becomes **double-counted unpaid
absence**, and unpaid absence is a line on a payslip.

**The measured facts.**

```
SubmitLeaveRequest        reads the overlap check, decides, saves — nothing held in between
transaction               none, and correctly so: every Attendance handler mutates one repository
isolation level           not set anywhere in src/ — SQL Server default, READ COMMITTED
idempotency on the route  none; the product has no general request-idempotency mechanism
RowVersion on the request does not help — it guards an UPDATE, and these are two INSERTs
database constraint       impossible: no index can express "these ranges must not overlap"
```

**A double-clicked submit button is sufficient.** It needs no adversary and no unusual timing.

**Why this is not the same as the other two range-overlap guards.** Fiscal years and attendance periods
have the identical weakness, and `CalendarCommandHandlers.cs:73` records a deliberate decision to accept it:
*"the exposure is small (defining a fiscal year is rare and deliberate) and the alternative is a lock held
across a human-scale operation."* **That reasoning is sound and it is about frequency.** A fiscal year is
defined once a year by an accountant; an attendance period monthly by an operator. **A leave request is
self-service, submitted by an employee whenever they like.** The exposure was weighed for a different
operation.

**The options, and what each does not do.**

- **Add a transaction** — **does not fix it.** At READ COMMITTED a transaction takes no range locks, and the
  competing rows do not exist yet, so both submissions still pass. Recorded because it is the fix a
  reasonable engineer reaches for and would then believe was done.
- **Serializable isolation or an explicit range lock** — closes it, and is exactly the "lock held across a
  human-scale operation" that was weighed and declined for fiscal years. The question is whether that
  judgement survives a self-service operation.
- **An application-level lock** — closes it, same cost, different mechanism.
- **A unique constraint on (employee, start date, end date)** — cheap, and catches the **double-click case
  only**: identical repeated submissions. It does nothing for a genuine partial overlap. The likeliest case,
  not the general one.
- **Accept it, as fiscal years did** — with the frequency difference stated, so the acceptance is about
  leave rather than inherited from a decision about something else.

**What is already true.** The guard itself is tested against a real database (T-146), so it works when
requests arrive one at a time. **Tested is not enforced:** the test proves the check runs, not that
concurrency cannot defeat it.

</details>

## 11. The commercial plane is half-built, and nothing recorded that — added 2026-08-29 (T-158)

**What it is.** FP-014's subscription and billing feature. **The domain and the read path exist. The entire
write half does not, and invoicing does not exist at all.**

**The measured facts.**

```
BUILT    domain, 9 entities   ModuleDefinition, PlanLimit, PlanModuleGrant, PlanPrice,
                              SubscriptionPlan, TenantEntitlement, TenantEntitlementGrant,
                              TenantSubscription, TrialSubscription
         persistence          migration, configurations, repository
         the READ path        entitlement cache, reader, snapshot, and an API projection

ABSENT   write handlers       create / update / retire a plan, assign a subscription,
                              grant or revoke an entitlement — zero
         permissions          all six documented ones absent from the catalog:
                              Plans.View/.Administer, Subscriptions.View/.Administer,
                              EntitlementGrants.Administer, Invoices.View
         routes               all 25 documented routes absent
         invoicing            NO invoice type in the product. No file even named for one.
```

**⚠ Why this is not the same as item 1.** Item 1's surfaces are **handlers built, transport missing** — the
doors are the only thing absent. **This is domain built, and handlers, permissions, routes and an entire
invoice concept missing.** *"Just needs transport"* and *"needs handlers, permissions, routes and an
invoice aggregate"* are different decisions, and the first would badly understate this.

**And nothing records the gap.** The tenant transport's absence is deferred by a criterion and enforced by
a live test. **FP-014's api-contracts describes 25 routes with no reconciliation note, no
specified-but-unbuilt marking, and no instrument has ever compared it to the code.**

**What it blocks.** Selling the product. There is no way to define a plan, price it, subscribe a tenant,
grant an entitlement or issue an invoice other than by engineering.

**The options.** Put it on the roadmap as a feature rather than a transport slice; mark the document so a
reader stops believing 25 routes exist; or decide the commercial plane is out of scope before release and
record that. **Doing nothing leaves a document describing a product that is half there.**

---

## 12. `GeneralStores` — is it a shared service or an ERP module? — added 2026-08-30 (T-216, T-229)

**What it is.** In the HIS schema, inventory is referenced from Marketing, InPatient, Nursing, Maintenance,
CSSD, Emergency and Laboratory. Whether it is shared or ERP-owned decides **59 of the 159 crossing foreign
keys (37%)**, and it also decides whether **7 of the 13 `dbo` rule-encoding views** are ours or theirs.

**Recommendation: shared.** Being referenced by seven clinical modules is correct design, not an accident,
and it must survive the split. **This needs ratifying, not deriving** — your own framing (*one product, HIS
with ERP included*) already implies it.

**Analysis:** `scripts/his-catalogue/MIGRATION-PLAN.md`. Not repeated here.

## 13. `ApplicationSetup` — is it shared master data? — added 2026-08-30 (T-216)

**What it is.** Cities, governorates, countries, banks — referenced from both sides and owned by neither.
Decides **8 crossing edges**.

**Recommendation: shared.** Same footing as 12: ratification rather than a question.

## 14. Who owns the hospital's organisational structure — HR, or the clinical modules? — added 2026-08-30

**What it is.** ⚠ **This is the real architectural question, it decides 63 edges (40% of the seam), and it
is genuinely open.** `Nursing.Employee` is one person master with two role extensions (`Doctors`,
`NurseMaster`); `HR.Department` and `HR.SubDepartment` are pointed at from InfectionControl, CSSD, Billing
and InPatient. **It is referenced in both directions, so it cannot simply follow ERP.**

**No recommendation — this one is argued, not derived.** It is answerable without reading a schema, which
is why it is worth your time rather than more of ours.

**⚠ It is one decision only if three parts move together.** They are coupled by the edges, not identical:
the **person master** (38 edges), the **ward and department tree** (14), and **employment reference data**
(11). Splitting them is legitimate — the person master could sit with HR while the ward tree stays clinical
— **and the price of each split is that its edge count stays crossing.** So: one decision at 63 edges, or
up to three with a stated cost for separating them.

## 15. Does the HIS migration proceed at all? — added 2026-08-30

**What it is.** ⚠ **Nothing on this list, and nothing in the plan, has ever asked this.** Decisions 12–14
all presuppose it. The plan is complete and self-checking, the ERP is unaffected either way, and **no code
has been written for HIS by instruction** — so the cost of answering "no" is the planning already done and
nothing further.

**It is recorded because it was never asked, not because it is in doubt.** A prior question that only ever
lives inside the answers to later ones is how a project acquires a direction nobody chose.

## What is NOT on this list

**Engineering-owned items are excluded** — guard coverage, test shape, the register's floor, inventory
migration. Those are being handled in the loop and do not need the owner.

**One item was checked and INVERTED rather than removed.** It was recorded as *"Company's `{companyId}`
route lacks a type constraint"*. Measuring it showed the opposite: **a 400 for a malformed identifier is the
product's convention, asserted by six tests across three modules and stated as a principle in FP-007** — and
Company follows it. **Attendance's `:guid` routes answer 404 for the same condition, which contradicts that
convention and is asserted by no test at all.**

**So the open item is Attendance's, not Company's, and it is engineering's to settle** — recorded here only
so the earlier framing does not outlive the measurement. See `T-130.md`.

**⚠ TEST CADENCE IS EXCLUDED, AND IT WAS ESCALATED ANYWAY.** On 2026-08-30 `NEXT-SESSION.md` listed *"when
to run the integration suite"* under what waits on the owner, phrased as *"it is their compute and their
time"*. **That is inside the "test shape" exclusion above** — the loop put a question to the owner that
this very section assigns to engineering, and the two documents contradicted each other for a day.

**Struck, and recorded rather than quietly removed.** The measured position is 24.2 minutes against 43.9
(855 passing, board row 1095): not per-task, comfortably a pre-merge or nightly gate where 44 was not.
**The loop decides it.**

**The general control this section is for: an escalation is a claim that engineering cannot answer
something, which makes it an absence claim** — and it spends the owner's attention, the one cost in this
project that appears on no ledger. **Check a new escalation against this list's exclusions before it is
written, not after.**
