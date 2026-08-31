# Open decisions for the owner — assembled 2026-08-28 (T-130)

**21 items** that engineering cannot settle on its own — **eleven ERP (1-11), four HIS (12-15), four measured on 2026-08-30 (16-19), and one on 2026-08-31 (20)**. ⚠ **Entry 17 is WITHDRAWN in place and struck: the dispatcher it said did not exist had existed for a month.** Each
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

### ⚠ MEASURED 2026-08-30 (T-161) — THE SPLIT IS NOW A NUMBER, AND ONE PART OF IT IS NOT ENGINEERING WORK

**All 54 of FP-014's acceptance criteria were mapped against the tree and the test suite: 20 pinned by a
named test, 11 implemented but unpinned, 19 not implemented, 4 blocked on an undefined subject.**

⚠ **THE LINE FALLS ALMOST EXACTLY BETWEEN WHAT A TENANT MAY USE AND WHAT A TENANT IS CHARGED.** The
entitlement half is built and genuinely well tested — append-only immutability with both bypass routes
covered, term invariants, expiry, cache expiry at the boundary instant, the seed run twice. **The billing
half does not exist:** no declaration of `Invoice`, `PaymentAttempt`, `Overage`, `Proration` or `SeatUsage`
anywhere in `src/`. **This sharpens the entry above rather than replacing it** — "half-built" is now
measured, and the half that exists is the half with the guarantees.

⚠ **AND FOUR CRITERIA ARE NOT WORK AT ALL — THEY ARE THIS DECISION, WAITING.** `AC-SUB-0040`, `0049`,
`0050` and `0051` all rest on the **undefined seat**: `DEC-L-009` says *"seats"* and never defines one, and
`AC-SUB-0049` names `TenantUser` **because that is the only reading available, not because it was ruled.**
Flagged in T-008, again in T-013, still open — as is `REQ-SUB-0027`'s two enforcement semantics.
**Filing these under "not implemented" would present a decision nobody has made as engineering not yet
done, which is precisely the sentence that would mislead this decision.** They are counted separately for
that reason.

**One more thing the owner should know before reading any status table:** `AC-SUB-0008` — *no tenant-plane
subscription permission exists* — **is satisfied because this package defines no permissions on either
plane.** All 28 platform permission names were enumerated; none is a subscription permission. **A green
row there is universal absence, not an implemented distinction.**

**Nothing above changes what this decision asks.** It changes what a reader would otherwise assume the
remaining work is: **the seat is not build work, and the entitlement half is not at risk.**

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

## 16. Attendance-driven hourly overtime cannot be paid — added 2026-08-30 (T-270, measured)

**What it is.** ⚠ **Measured end to end, not read.** A pay element's overtime tier can only be set by
`PayElement.SetOvertimeTier`, **which has no production caller**. Attendance accepts and validates a tier on
recorded overtime; **Payroll has no way to price one.** Probe with an element built exactly as the API can
build it and 6 hours of `NIGHT` overtime: **overtime lines 0, overtime amount 0; basic pay correct. The run
succeeds — no error, no warning, a payslip that looks complete.**

**Locked twice, independently, so closing either half alone fixes nothing:** overtime recorded *without* a
tier never reaches payroll (`AttendanceSummaryService:227` filters on `OvertimeTier is not null`); overtime
*with* a tier reaches it and finds no element that can match.

**Boundary, stated precisely:** *Attendance-driven **hourly** overtime*. **Base salary, fixed-amount
elements, absence deductions and one-off payments are unaffected**, and a `FixedAmount` element used as an
overtime allowance still pays.

**Why it survived.** Four test files call `SetOvertimeTier` **directly**, each supplying the missing half,
**so every test of the capability passes while the capability is unreachable from any request.** And the
mechanism was already documented — `EmployeeErrorWireContractTests` recorded it — **but nobody followed it
to the payslip.**

**The decision.** Finishing it means adding a tier to the pay-element commands: **a product decision, not an
engineering one.** ⚠ **The other half is yours alone: whether anyone has recorded overtime expecting
payment.** The code answer does not depend on it.

## 17. ⚠ WITHDRAWN THE SAME DAY — THE DISPATCHER EXISTS. THIS ENTRY WAS FALSE

**This entry was added 2026-08-30 (T-271) and withdrawn 2026-08-30 (T-165). It asked the owner to build a
dispatcher or record a deferral. There was nothing to decide: the dispatcher was built on 2026-07-31 and
the flow is complete.** Three `Accepted` ADRs were annotated on this premise; **all three annotations are
withdrawn in place.**

`AggregateRoot<TId> : Entity<TId>, IHasDomainEvents` → 65 raise sites → tracked by the `DbContext` →
`ITenantUnitOfWork` / `IPlatformUnitOfWork`, injected in **122 places** → `EfUnitOfWork.SaveChangesAsync`
→ `DispatchDomainEventsAsync`, reading `ChangeTracker.Entries().OfType<IHasDomainEvents>()` →
`IDomainEventDispatcher.DispatchAsync` (registered `AddScoped`) → each `IDomainEventConsumer` →
`ClearDomainEvents()`.

⚠ **THE MECHANISM OF THE ERROR, WHICH IS THE PART WORTH KEEPING.** The instrument looked for production
readers of **`DequeueDomainEvents`** and correctly found none. **The dispatch path does not use that
method** — it reads the `DomainEvents` property and calls `ClearDomainEvents()`. **A complete and correct
enumeration of the WRONG MEMBER was published as the absence of the whole mechanism.** ⚠ **And it was
stated three ways — *"nothing consumes them"*, *"there is no dispatcher"*, *"checked three ways"* — which
made one measurement read as three corroborating ones.** *A complete enumeration of the wrong set reads
exactly like a complete enumeration*, written on this board on the same day, applied to the architect's
own work six hours later.

**What survives, and it is small:** exactly **one** `IDomainEventConsumer` is registered
(`LocalizationCacheDomainEventConsumer`). **Handler coverage is a fair question and is not an owner
decision.** ⚠ **The specific harm this entry claimed to prevent — a handler written in good faith that
never runs — was never possible: a registered consumer is delivered to.**

**Original entry retained below, struck, because the correction is worth more than the claim.**

### ~~17. Three Accepted ADRs specify a domain-event dispatcher that does not exist — added 2026-08-30 (T-271)~~

**What it is.** `RaiseDomainEvent` is called **65 times** across the product. **Nothing consumes them** —
`DequeueDomainEvents`, `ClearDomainEvents` and the `DomainEvents` property have no production reader, and
there is no dispatcher of any name. **Every domain event raised is appended to a list on its aggregate and
discarded.**

**This is not an unrecorded plan — it is the opposite.** **ADR-009 is `Status: Accepted`** and gives the
publishing flow; **ADR-008** states *"Domain Events are dispatched after successful persistence"*;
**ADR-004** lists *"Commands publish Domain Events"* as a consequence.

⚠ **And the convention for handling this correctly already exists in the repository and is followed
elsewhere:** `FP-010-ANALYSIS` carries `status: Deferred — gated on ADR-028` in its own front matter.
**Documented, deferred, and the record says so.** **So this is one document departing from a convention you
already keep, not a convention to adopt.**

**The risk.** Nothing is visibly broken, **because nothing depends on the events — which is why it went
unnoticed.** ⚠ **The exposure is the next person to write a handler in good faith against an Accepted ADR,
subscribing to an event that will never be delivered: it would compile, pass review, and never run.**
**All three ADRs were annotated 2026-08-30 so that harm cannot land while this waits.**

**The decision: build the dispatcher, or record the deferral formally.** Either closes it.

## 18. `Branch.FirstBranchRequired` is specified and not implemented — added 2026-08-30 (T-272)

**What it is.** `Branch-Management.md` gives the code a **role table, a state table and an error-table
row** — *"The tenant has no active branch; an administrator must create the first branch."* **Nothing
produces it.**

**Same shape as 17 and the smallest of the three.** **The decision is the same: build it, or record it as
deferred the way `FP-010` does.** ⚠ **One of 63 documented codes with no producer, and the only one of the
seven that is neither deliberate nor already recorded as deferred.**

## 19. The distributed rate-limit obligation is a declaration, not a verification — added 2026-08-30 (T-283)

**What it is.** ⚠ **This is a DEPLOYMENT obligation, and it is the only item on this list that lives outside
the code.**

The support-authentication surface — the one anonymous door into the privileged cross-tenant plane — is
**well defended in the application**: login is limited twice over (30/minute per IP **and** 5 per 15 minutes
per identity+IP), refresh and logout are limited, keys are HMAC'd, **and the limiter's default switch arm is
1/minute, so an endpoint added without a case is throttled rather than unlimited.** Account lockout is 5
attempts for 15 minutes, **held on the account rather than the IP, so address rotation buys nothing against
one account.**

**⚠ But the limiter's window store is an in-process dictionary and does not span replicas.** The design knows
this: **production start-up throws unless the HMAC secret is at least 32 characters AND
`UpstreamDistributedRateLimitingEnforced` is set.**

**⚠ That flag is a DECLARATION, NOT A VERIFICATION. The application cannot check that an upstream limiter
actually exists.** So on multiple instances **the per-IP limits divide by the replica count while the
account lockout does not** — the backstop holds, the front door widens.

**The decision.** **Confirm that an upstream distributed limiter is actually deployed in front of this
surface, or run it single-instance.** **Nothing in the repository can answer this and nothing in it will
ever fail if the answer is no.**

**Related and NOT a defect:** revoking a support principal's administer permission leaves their issued token
valid **for at most fifteen minutes** — `JwtOptionsValidator` refuses any configured value above that, so
**misconfiguration cannot widen the window.** **Disable is the immediate action; revoke is bounded.** Worth
knowing during an incident, and not a finding.

**Not examined, so that this entry is not read as a clean bill:** whether the upstream limiter is deployed
(not knowable from the repository), and **MFA on this surface — not looked for either way, and whether it
should carry a second factor is a product decision rather than a measurement.**

## 20. ⚠ 36% of what this repository asserts never runs before a merge — added 2026-08-31 (T-176, measured)

**What it is.** `DEC-L-007` — your rule — says a gated task with a **green gate merges immediately**. The
gate at `GATE_SCOPE=TASK` **excludes `Integration.Tests` by design.** Nobody had measured what that
excludes.

**The measured facts.**

```
Integration.Tests      3,724 of 10,457 assertions  = 36% of everything the repository asserts
                       68 files, 772 facts         TASK runs NONE of it
Release configuration  a different analyzer set    TASK runs Debug only
                       the gate's own header records the first Release run exposing CA1826
                       that Debug had never shown
```

⚠ **The structural reason is sharper than the count: NO TASK SUITE EVER MATERIALISES A REAL SCHEMA.** 144
`EnsureCreated`/`Migrate` calls in Integration against **eight** across all seven TASK suites — and the TASK
suites naming `UseSqlServer` point at `"Server=model-only;Database=none"`. **A model is built; a connection is never
opened.** So **everything the mapping layer MEANS at the database level is asserted in exactly one suite,
and it is the one the merge gate skips.**

**What TASK does still catch, so this is not overstated:** it builds the whole solution, **so a change that
fails to compile anywhere — Integration included — reddens it. A compile break cannot merge.** The exposure
is runtime behaviour and Release-only analysis.

⚠⚠ **UPDATED AGAIN, AND THE ACUTE HALF IS CLOSED: THE FIRST GREEN PHASE GATE RAN ON 2026-08-31 —
`[GATE GREEN — all eight suites, Debug and Release]`, INTEGRATION 848/848 IN BOTH CONFIGURATIONS.** The
one named test is fixed and confirmed **in the configuration that failed it**, and the total moved from
846 to 848 **because two capture controls were added — nothing was removed, weakened or skipped to reach
green.**

⚠ **AND `test-baseline.txt` GAINED ITS ROWS: 7 → 16.** **For the first time since 2026-08-27 this
repository has a recorded expectation for the Integration suite and the Release configuration.**

⚠⚠ **WHICH EXPOSED THE CONSEQUENCE NOBODY HAD DRAWN: condition 4 skips any suite with no baseline row, so
for four days IT COMPARED SEVEN OF SIXTEEN SUITE/CONFIGURATION PAIRS** — and reported that only as a
count. **It compares all sixteen from the next run on**, and the gate's header now says so and tells a
reader to read the *suite total(s) checked* number rather than the word `ok`.

**WHAT THIS DOES AND DOES NOT CHANGE FOR YOUR DECISION.** ⚠ **The structural exposure is unchanged: 36% of
the repository's assertions still do not run before a merge, and no TASK suite still ever materialises a
schema.** **What has changed is that the suite is now KNOWN GREEN and has a recorded baseline, so the next
divergence is detectable rather than invisible.** **The options below are unchanged; they are now a choice
about keeping a known-good suite watched, rather than about discovering what an unwatched one contains.**

⚠⚠ **IT IS NO LONGER HYPOTHETICAL. THE INTEGRATION SUITE HAS BEEN RED SINCE BEFORE THIS WORK BEGAN, AND
EVERY MERGE WENT GREEN OVER IT.** Found 2026-08-31 by the first `GATE_SCOPE=PHASE` run to complete.

`PlatformAuthenticationPersistenceTests.Concurrent_http_refresh_and_logout_use_validated_transport_and_sql_serialization`
**fails 1 of 846 — a logout answering `403 Forbidden` where `204 No Content` is expected. It fails in both
the Debug and the Release legs, and it is DETERMINISTIC rather than a race.**

**Bisected rather than guessed**, running that one test at each point in a separate worktree: it fails at
the pre-175 commit, at the pre-164 commit, **and at `112cb31` — the commit this whole stretch of work
started from.** ⚠ **So it is nobody's change from this loop, and it predates all of it.**

**And the repository already said why nobody knew.** `test-baseline.txt`, in its own words: *Integration,
and every Release row — NOT YET WRITTEN. Both are produced only by a green `GATE_SCOPE=PHASE` run, and none
has completed since this file was introduced on 2026-08-27.* ⚠ **The FACT was recorded. The IMPLICATION —
that nobody therefore knows whether Integration is green — was never drawn.**

**That is this decision, without a hypothetical: a suite holding 36% of the repository's assertions has
been failing for days, and the merge rule never looked.**

⚠ **DIAGNOSED 2026-08-31, AND THE ANSWER MATTERS TO HOW YOU READ THIS ENTRY: THE PRODUCT IS CORRECT. THE
TEST IS WRONG — AND IT EXPIRED, LITERALLY, AT 2026-08-30 12:00:00 UTC, ABOUT FIFTEEN HOURS BEFORE THE RUN
THAT FOUND IT.**

The fixture freezes its clock at **2026-07-31 12:00:00 UTC** and dates the test's CSRF value from a seeded
expiry of that instant plus the 30-day session idle lifetime. **The service that validates it uses a
time-limited protector checked against the REAL clock.** Measured in the failing run:
`csrfExpiry=2026-08-30T12:00:00Z`, `realNow=2026-08-31T03:31:09Z`, `expired=True`. ⚠ **The token was
genuinely expired and refusing it was exactly right.**

**So this is a TIME BOMB, not a regression** — which is why it bisected to every commit tried, including
the one this work started from. **Nothing in this stretch of work caused it and nothing in it could have
prevented it.**

⚠ **DO NOT READ THE RED SUITE AS A BROKEN PRODUCT. Read it as this:** a suite holding 36% of the
repository's assertions **went unwatched for four days**, and what it was hiding happened to be a test
defect. **The exposure is unchanged; the luck is that this time it cost nothing.** **The next thing that
suite hides may not be a test.**

**The diagnosis was reached by instrumenting rather than arguing:** the wire response names
`authentication.request_rejected` for **both** the refresh and the logout — so not a race — and an echo
endpoint under the same path prefix proved the transport gate accepted the request (`IsHttps`, origin,
both cookies, the CSRF header all present), **leaving the CSRF check as the only remaining site.**

---

**The earlier example, kept because the trajectory matters.** This entry first claimed that deleting
`EmployeeConfiguration`'s `.HasFilter("[NormalizedNationalId] IS NOT NULL")` would ship a data defect. **That was
false** — EF Core's SQL Server provider supplies that filter **by convention** for any unique index over a
nullable column, measured by removing the declaration and reading the built model. **The reachable form is
`.HasFilter(null)`, which explicitly overrides the convention**, and with that substitution every other
claim held — but `HasFilter(null)` is a **deliberate act** where deleting a line is an accident, **so the
example had already stopped carrying this decision before the real one arrived.** ⚠ **Evidence went
hypothetical → weakened → actual, and all three states are on the record, because a decision that shows
only its strongest moment is not one you can weigh.**

**Classes only Integration catches**, named from its own test names rather than from categories: scoped
uniqueness *including absence many times*; `rowversion` optimistic concurrency; migration refusal against
live data; database-level cascade from a **raw** delete bypassing EF; routing/cutover atomicity under
concurrent change; schema health surviving connectivity churn.

**What engineering is doing without you, so this decision is smaller than it looks.** ⚠ **The specific hole
is being closed rather than the rule being changed:** item 177 builds a structural guard — a unique index
over a nullable column must carry a NULL filter — which runs under TASK and reddens on exactly the deletion
above. **It also enumerates the 45 unique indexes that carry no filter today, because if any is over a
nullable column that is a live defect and not a hypothetical one.** Item 178 measures the Release half,
which was stated from the gate's header and not measured.

**What it blocks.** Nothing today. **It is a standing exposure, and it is the kind that is invisible until
it is expensive.**

**The options.**

- **Leave `DEC-L-007` as it is.** Defensible once 177 lands: the worst known class becomes TASK-visible,
  and a compile break already cannot merge. **The residual is whatever nobody has thought to guard.**
- **Require `GATE_SCOPE=PHASE` for changes touching persistence configuration or migrations** — a narrow
  rule over the area where TASK is structurally blind, at the cost of a ~24-minute gate on those changes.
- **Run Integration on every merge.** Closes it completely; makes every merge cost the full run.

**A related fact, WEAKER THAN THIS ENTRY FIRST CLAIMED.** `Performance.Tests` and `UI.Tests` contain zero
source files — a `.csproj` each. **This entry first said their names assert coverage that does not exist.**
⚠ **They are in fact recorded and deliberate: `test-baseline.txt` names both as EMPTY SCAFFOLDS and says
*their absence is correct and stays correct until somebody writes a test in one.*** **So it is a known
placeholder, not an unnoticed gap, and it needs nothing from you.** `B17` is retained only to establish
what they were for before anything is removed.

**Measurement caveats, stated by the window that made it:** assertion counts are `Assert.*` **call sites**
rather than executed assertions, so a `[Theory]` multiplies at run time — **the comparison between suites
is fair but no figure is exact** — and the Release half is taken from the gate's own header rather than
from a Release-only analysis run.

---

## 21. ⚠ The coder cannot restart itself, and no wording fixes the last gap — added 2026-08-31 (T-186, measured from inside the loop)

**What it is.** You have flagged *coder idle* **more than a dozen times this session.** Every diagnosis
until now was the architect's, made from outside the coder's process, **and two of three were wrong.**
Item 186 asked the coder to instrument its own loop instead. **The answer is structural and it is not
either window's fault.**

**The measured facts, in the coder's own terms.**

- ⚠ **`QUEUE.md` IS A MAILBOX WITH NO DOORBELL.** It is durable, authoritative and immune to the message
  transport — **and nothing reads it on its own.** **Only a delivered message starts a turn.** A ruling
  that reaches the file but not the wire leaves the file saying *work outstanding* while the coder is
  stopped: **from the architect's side the queue is full; from the coder's, nothing happened.**
- **It had read that file TWICE in the entire session**, both times because a message said to.
- **A turn ends when it emits its final summary. Reporting to the architect is a tool call and does not end
  it.** ⚠ **Nothing sits between *item complete* and *turn over* — there is no step where the queue is
  consulted, because there is no step there at all.**
- **Messages arriving mid-turn are seen and acted on. Mid-turn delivery is NOT the failure.**
- ⚠ **It cannot self-wake. No timer, no poll. Only inbound input starts a turn.**

⚠ **AND THE FILE'S OWN INSTRUCTION COULD NOT EXECUTE.** It said *read this before going idle* — **going
idle is not an action anybody takes; the turn simply ends.** **An instruction attached to a non-event has
nothing to fire on.** The rule three lines below it — *grep the results trail before building any
instrument* — **fired repeatedly, because it hangs on an action somebody performs.**

**What has been done without you.** The coder made reading the queue **the last step of completing an
item** — an event that exists — and the file's header is rewritten to match, with the inert instruction
withdrawn rather than repeated louder.

⚠⚠ **UPDATED HOURS LATER, AND THE UPDATE IS GOOD NEWS THIS ENTRY DID NOT PREDICT: THE RE-ANCHORING TOOK
HOLD ON ITS FIRST CYCLE.** The coder read `QUEUE.md` as the closing step of its next turn, **found two
unstarted items, and began a 69-minute run with no message telling it to.** **First time in the session it
picked up work from the file rather than from the wire.** ⚠ **The architect's *doorbell* message arrived
while the item it dispatched was already running — and the architect had, for the third time, called an
idle that was not one.**

**So the strong claim above is withdrawn.** *Stopped with items queued* is no longer the normal case; the
file now starts work.

⚠ **WHAT REMAINS, AND IT IS GENUINELY SMALLER: THE LAST COMPLETION STILL ENDS SOMEWHERE.** When the queue
is empty at that last look, nothing restarts the coder. **A window that cannot self-wake needs an EXTERNAL
trigger for that final gap, and no wording supplies one.** **The options below still stand — they are just
buying a smaller thing than this entry first said.**

**What it blocks.** Nothing technical. **It costs you an interruption every time the loop reaches its own
end, and it has cost you a dozen already.**

**The options.**

- **Leave it.** You nudge when you notice. **Now with the honest expectation that this is irreducible from
  inside, not a discipline problem either window can drill away.**
- **Give the coder a scheduled wake** — anything that starts a turn on a timer would let it re-read the
  queue unprompted. **Neither window can arrange this; it is a change to how the coder is run.**
- **Have the architect send on a cadence rather than only on completion** — cheap, and it makes the
  architect the doorbell. **It still fails when the architect's own last turn ends.**

- ⚠ **BUILT 2026-08-31, AND IT IS THE THIRD OPTION: the architect now holds a recurring timer that wakes
  IT roughly every quarter hour.** On waking it reads origin, the working tree and this repository's queue
  file; **if the coder is stopped with open rows it sends a doorbell restating each row's scope in full,
  and if a run is in flight it does nothing.** **The architect is the doorbell, on a cadence rather than
  only on completion.**

⚠⚠ **AND IT IS NOT PERMANENT, WHICH THE OWNER SHOULD HEAR PLAINLY RATHER THAN DISCOVER: the timer lives
only in the architect's session. It is not written to disk, it dies when that session exits, and it
auto-expires after seven days.** **It closes the gap while the loop is running and closes nothing
afterwards.**

**A genuinely durable fix is outside both windows.** It needs one of: **a scheduled wake attached to the
coder itself**, or **a supervisor outside both sessions**, or **the owner's own nudge**. ⚠ **Neither
window can arrange any of the three, and neither has proposed a change to `CLAUDE.md`, settings or
permissions to get there.** **The choice is the owner's; what has been built is the best available
approximation and it is labelled as one.**

⚠ **Nothing here is a request to change `CLAUDE.md`, settings or permissions.** The coder proposed no such
change and the architect would not carry one; **the remedy above is yours to choose or decline.**

---

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

**⚠ AND THAT INVERSION HAS ITSELF BEEN INVERTED — 2026-08-30 (T-236, T-237). READ THIS BEFORE ACTING ON THE
PARAGRAPH ABOVE.** The claim *"a 400 for a malformed identifier is the product's convention, asserted by six
tests across three modules"* **does not survive enumeration.** Those six tests cover **four different input
surfaces** — malformed rowversions, a malformed company header, a malformed query-string filter, a malformed
policy name and malformed JSON. **Exactly one is about a ROUTE PATH, and it is Company's.**

**Counted across `src/`: 71 route-path identifiers are constrained and answer 404; 25 are unconstrained and
answer 400.** Attendance was **not** the deviant — Company and Localization are. The paragraph above named
the wrong module as the exception because it inferred a route-path convention from tests about headers,
bodies and query strings. **"Malformed input is a 400" is real and well-evidenced everywhere except the one
surface it was cited for.**

**Ruled by engineering, no owner action: 400 everywhere, and the constraints come off.** The ruling does
**not** rest on which behaviour is in the majority — it rests on the fact that **404 makes a malformed
identifier indistinguishable from an absent record**, so a caller cannot tell "your GUID is not a GUID"
from "that record is gone". A 400 with a problem document can say which. Staged behind a per-module route
ambiguity check, since removing a constraint widens what a route matches.

**Recorded rather than edited away, because this entry has now been wrong in two directions** — and a
correction that erases its predecessor teaches nobody why the first reading was persuasive.

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
