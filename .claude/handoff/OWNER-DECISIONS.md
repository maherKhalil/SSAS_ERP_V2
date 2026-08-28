# Open decisions for the owner — assembled 2026-08-28 (T-130)

Nine items that engineering cannot settle on its own. Each carries **what it is**, **the measured facts**,
**what it blocks**, and **the options**. Where the call is genuinely the owner's there is no recommendation.

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
- **Defer knowingly** — it works today via engineering; the cost is engineering time per change.

---

## 3. Employment type — unchanged, still absent

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

## 9. `Company.BaseCurrencyCode` is stored non-Unicode — added 2026-08-28 (T-134)

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
