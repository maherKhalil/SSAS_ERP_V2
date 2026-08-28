# Open decisions for the owner — assembled 2026-08-28 (T-130)

Eight items that engineering cannot settle on its own. Each carries **what it is**, **the measured facts**,
**what it blocks**, and **the options**. Where the call is genuinely the owner's there is no recommendation.

**Every number here was re-verified against the tree today.** Three items had moved since they were
recorded, and one had moved enough to change what the decision is about. Those are marked ⚠ **CHANGED**.

---

## 1. Platform's administration transport — ⚠ CHANGED (it is roughly twice the task it was)

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
- **Defer knowingly** — it works today via engineering; the cost is engineering time per change.

---

## 2. Employment type — unchanged, still absent

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

## 3. Overtime tiers — ⚠ CHANGED, and the decision is a different one than recorded

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
Attendance groups tiers with   StringComparer.Ordinal        case-sensitive
AttendanceRecord stores the tier VERBATIM                    no trim
PayElement.SetOvertimeTier    stores overtimeTier.Trim()     trims
the match                     quantities.TryGetValue(tier)   ordinal, exact
```

**Both sides are free strings, and they normalise differently.** A record tagged `"Night"` against an
element tagged `"NIGHT"`, or a record with a leading space against an element that trimmed one, **does not
match — and `OvertimeQuantity` returns `0m`.** The employee is paid **no overtime for that tier, silently**:
no error, no warning, and a payslip that looks complete.

**Every test on both sides uses the literal `"NIGHT"`.** The mismatch case is covered nowhere.

**What it blocks.** Nothing today — one tier spelled consistently works. It is a latent money defect that
surfaces the first time two people type the same tier differently.

**The options.**
- **A tier catalog** — companies define tiers once; records and elements reference them. Removes the class.
- **Normalise on both sides** — trim and upper-case at both write points. Cheap, and does not stop a typo
  that is not a case difference.
- **Refuse at run time** — the calculator fails the run when a record's tier matches no element, instead of
  paying zero. Turns a silent underpayment into a visible refusal.
- **Leave it** — with the hazard recorded.

---

## 4. Self-service grants — ⚠ CHANGED (three permissions, not six)

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

## 5. `POST /api/attendance/records/bulk` — unchanged, specified and never built

**What it is.** A bulk attendance-record route, **specified in `FP-013`'s api-contracts and never built.**
It is documented there as specified-but-absent rather than deleted, deliberately.

**What it blocks.** Importing attendance from a device or spreadsheet. Today each record is one request.

**The options.** Build it; delete the specification and decide records arrive one at a time; or leave it
recorded as a known gap.

---

## 6. The hours-per-day factor — unchanged, absent and unclaimed

**What it is.** There is **no hours-per-day constant anywhere in `src/` or `docs/`.** It was raised, held,
and no consumer has appeared under the owner's own model of the business.

**What it blocks.** Nothing observed. It would matter if an hourly employee's entitlement had to be
expressed in days, or a daily employee's in hours.

**The options.** Leave it absent until something needs it; or define it now if the business already thinks
in both units.

---

## 7. Calendar resolution — unchanged, inferred rather than decided

**What it is.** When a company has more than one working calendar, the code picks one by
**`IsDefault` first, then by name** — chosen for determinism, so the answer is never arbitrary.

**That is an inference, not a ruling.** Nobody has said what *should* happen when a company has several
calendars, and **the same inference governs both leave and pay.**

**What it blocks.** Nothing today, while companies have one calendar each.

**The options.** Ratify the current rule; assign calendars per employee or per department; or forbid more
than one calendar per company and delete the ambiguity.

---

## 8. Period versus pay date in run inclusion — unchanged, deliberate and undocumented for the owner

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

## What is NOT on this list

**Engineering-owned items are excluded** — guard coverage, test shape, the register's floor, inventory
migration. Those are being handled in the loop and do not need the owner.

**One item was checked and removed:** the `{companyId}` route constraint. It looked like an inconsistency
worth ruling on, and measuring it showed the product's convention already answers it. See `T-130.md`.
