# FP-013 — Authorization model (proposed)

> **RATIFIED 2026-08-25.** All sixteen `OD-ATT` rulings are closed; see
> [`decisions-ratified.md`](decisions-ratified.md). Conditional passages below are resolved inline where the
> ruling removes a fork; where they are not, the ratification file is authoritative.

Three parts: the permission names, the read scope, and **the self-service blocker** — which is not a
preference but a missing input, and is the most important thing in this document.

---

## The self-service problem, verified rather than assumed

`REQ-ATT-0023` proposes that an employee may read their own attendance and leave records. It is the most
obviously desirable requirement in the package, and **it is not implementable today.**

`PayrollPermissionNames` says why, and it says it as a deliberate refusal:

> **Self-service is NOT here, and its absence is deliberate.** `OD-PAY-0016` deferred it because it would
> depend on a mapping from the authenticated identity to an employee record, and this build does not assert
> such a mapping exists. Adding a `Payroll.Payslips.ViewOwn` on an unverified assumption is exactly the
> shape of the FP-011 near-miss.

*(T-086: the block above is quoted as it stood when this package was written and is left unaltered so the
quotation remains accurate. `PayrollPermissionNames` no longer reads that way.)*

**The mapping now exists** — `UserEmployeeLink` (`ADR-030`, T-082), Platform-resident, asserted against a
real database. `Employee` still carries no user identifier and does not need one: the link is a separate
Platform table, which is `ADR-030` Decision 2.

So every self-service requirement in this package — an employee viewing their own attendance, submitting
their own leave request, seeing their own balance — **depends on an input the product does not have.** That
is precisely `DEC-PAY-0002`'s shape, and the same discipline applies:

**A permission whose subject cannot be resolved must not be declared.** No `Attendance.Records.ViewOwn`, no
`Attendance.Leave.RequestOwn` — **and the subject IS now resolvable, so what holds the line is FP-015's
unbuilt permission and endpoint rather than a missing input.** The absence is asserted by **`AC-ATT-0032`**,
which the Attendance architecture guard cites — the criterion is the durable handle, not the guard's method
name (T-087).

**This has real consequences for `OD-ATT-0001` and `OD-ATT-0007`.** A leave module in which employees cannot
submit their own requests is a leave module operated entirely by administrators on employees' behalf. That
may be acceptable for a first delivery — it is how a great deal of HR software starts — but **the owner
should rule on it knowingly**, not discover it at acceptance.

**Creating the identity→employee mapping was therefore a candidate prerequisite for FP-013** — and it was
built as `ADR-030` / `UserEmployeeLink` (T-082), a Platform change exactly as predicted, not an Attendance
one. It is raised here because this package is the second consecutive
feature to hit the same wall, and the second hit is when a missing input stops being a coincidence.

---

## Permission names — proposed

The grammar is `<Plane>.<Resource>.<Action>`. **The two existing modules use different verb granularity**,
and the build should not split the difference by accident:

| Module | Verbs |
|---|---|
| HR | `Create`, `Update`, `Deactivate`, `View`, `Terminate`, `Transfer`, `Import`, `Export` — granular, per-act |
| Payroll | `View`, `Manage`, plus `Approve` and `Post` for the sensitive acts — coarse, with the sensitive acts split out |

Attendance is proposed to follow **Payroll's** shape: coarse `View`/`Manage`, with genuinely sensitive acts
promoted to their own name. Attendance is closer to Payroll in character — a periodic operational cycle with
a close and a downstream consumer — than to HR's master-data maintenance.

| Permission | Covers |
|---|---|
| `Attendance.Calendars.View` | read the working calendar and holidays |
| `Attendance.Calendars.Manage` | maintain them |
| `Attendance.Records.View` | read attendance records within authority |
| `Attendance.Records.Manage` | record and correct within an open period |
| `Attendance.Periods.View` | period existence and state |
| `Attendance.Periods.Close` | **the sensitive act** — see below |
| `Attendance.LeaveTypes.View` / `.Manage` | the catalog *(scope B and C)* |
| `Attendance.Leave.View` | leave requests within authority *(B and C)* |
| `Attendance.Leave.Manage` | submit and amend on an employee's behalf *(B and C)* |
| `Attendance.Leave.Approve` | **the sensitive act** *(B and C)* |
| `Attendance.Leave.ViewSensitive` | **leave type disclosure** — `OD-ATT-0013`(3) |

### Why `Periods.Close` is separated

`BR-PLT-0103` names Payroll Processing sensitive, and `OD-PAY-0009` placed the sensitivity at **approval**
rather than calculation, because calculation commits nothing while approval is the assertion that these
figures are real.

Closing an attendance period is the analogous act: **it is the moment the numbers Payroll will consume stop
moving.** Under `OD-ATT-0010`'s likely ruling it is also the gate payroll calculation passes through, which
makes it the same kind of act as `Payroll.Runs.Approve` and deserving of its own grant.

### Why `Leave.ViewSensitive` exists

Seeing *"absent 3 days"* and seeing *"sick leave, 3 days"* are different disclosures. The second is health
information about an identified person.

The grammar already supports sensitivity splits, and `ViewPayslips` is the working precedent: it was
deliberately **not** folded into `ViewRuns`, because a run's existence and totals are operational while the
lines beneath them are an individual's pay. Leave *occurrence* is operational — a scheduler needs it. Leave
*type* is not.

**`OD-ATT-0013`(3) must rule on this**, and a decision to merge them is legitimate — but it should be a
decision, not a default.

---

## The read scope

`AttendanceReadScope` follows `EmployeeReadScope` and `RosterScoped` exactly (`DEC-ATT-0008`):

- **Private constructor, internal factory.** It cannot be constructed by a caller that has not passed through
  the factory, so a query cannot be issued with a scope somebody assembled by hand.
- **Authority resolved live** from `ITenantCompanyAccessResolver` — and, if `OD-ATT-0011` rules branch-owned,
  additionally from `ITenantBranchAccessResolver`, which returns **active branches only**.
- **`UnauthorizedAccessException`, never an empty list.** An empty result is indistinguishable from "no
  records", which converts an authorization failure into what looks like a data answer.

The architecture guard file naming the sanctioned read shapes **grows by one**, with its reasoning inline.
It listed one shape, then two, then three; Attendance makes four. FP-012 added the roster shape and hit the
guard's "exactly one `Set<Employee>()`" assertion when the query moved — the guard caught its own author,
which is the point of it.

### The branch dimension in the read scope

If `OD-ATT-0011` rules branch-owned, the read scope carries three dimensions and `RosterScoped`'s warning
applies directly: **a branch filter means a payroll-feeding query can silently omit employees.** The
mitigation is that the summary contract is company-grained regardless — but that is precisely the asymmetry
`OD-ATT-0011` names and does not resolve.

---

## Registration

`IPermissionCatalogContributor` exposes a **`Permissions` property**, not a method — the sort of detail that
costs a compile cycle to rediscover, so it is written down.

`AttendancePermissionCatalogContributor` follows `PayrollPermissionCatalogContributor` in shape and is
registered in the module's DI extension.

## What is deliberately absent

**No `ViewOwn` of any kind** — the self-service blocker above.

**No permission for the summary contract.** It is consumed in-process by Payroll, whose own
`Payroll.Runs.Manage` already gates the calculation that reads it. A second permission on the contract would
mean a payroll operator needed an Attendance grant to run payroll, which turns a module boundary into an
administrative one. **`IEmployeeRoster` set this precedent and it is followed without argument.**

**No approval-delegation permission.** Delegation — "approve on behalf of while the manager is away" — is
real in leave management and is not modelled here, because `OD-ATT-0007` has not yet established who the
ordinary approver is. Named as an absence so it is not mistaken for an oversight.
