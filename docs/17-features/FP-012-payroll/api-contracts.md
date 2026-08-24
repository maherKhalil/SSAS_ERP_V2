# FP-012 — API Contracts (PROPOSED)

A proposed route surface following the repo's conventions. **Route count is not fixed until
`decisions-open.md` is ruled** — several routes exist only under particular options.

---

## Conventions, inherited and not re-argued

* **Named-action POST routes.** No `DELETE` verb anywhere in the product; no payload-dispatch routes.
* **Every route carries a permission policy.** A route without one is reachable by any authenticated
  caller, which is the single worst mistake this surface could make and is invisible in a diff that simply
  forgets a line. GL pins this with a test asserting no route has an empty policy.
* **Strict request reading.** `StrictRequestReader.ReadStrictJsonAsync` deserializes with
  `JsonSerializerOptions.Default`, which is **case-sensitive** — so **every request record property needs
  `[property: JsonPropertyName("…")]`**.

> **This is not a style note.** FP-011 shipped GL's request records without the attributes and **every
> write route returned `400 request.invalid`** — the routes, handlers, domain and error mapper were all
> correct, and the fault was an *absence*, which reading the code does not reveal. Two API tests caught it
> on their first run. Payroll must not rediscover it.

* **Module API layers must not reference Platform assemblies** (`ADR-012`); wire-equivalence of problem
  codes is the contract where errors must match across modules.

---

## Proposed routes

### Compensation

| Method | Route | Permission |
|---|---|---|
| POST | `/api/payroll/employees/{employeeId:guid}/compensation` | `Payroll.Compensation.Manage` |
| GET | `/api/payroll/employees/{employeeId:guid}/compensation` | `Payroll.Compensation.View` |
| GET | `/api/payroll/employees/{employeeId:guid}/compensation/current` | `Payroll.Compensation.View` |

POST creates a **new dated record**; there is no update route, because `BR-PAY-0002` makes a change a new
record rather than a mutation. *Under `OD-PAY-0003` option 1 this becomes a PUT and the history disappears
— the route shape is the data decision made visible.*

`/current` resolves the record in force **today**; the collection route returns the history. Both are
guarded by the same permission: the current value is not less sensitive than the history.

### Pay elements

| Method | Route | Permission |
|---|---|---|
| POST | `/api/payroll/elements` | `Payroll.Elements.Manage` |
| GET | `/api/payroll/elements` | `Payroll.Elements.View` |
| GET | `/api/payroll/elements/{payElementId:guid}` | `Payroll.Elements.View` |
| PUT | `/api/payroll/elements/{payElementId:guid}` | `Payroll.Elements.Manage` |
| POST | `/api/payroll/elements/{payElementId:guid}/deactivation` | `Payroll.Elements.Manage` |
| POST | `/api/payroll/elements/{payElementId:guid}/activation` | `Payroll.Elements.Manage` |

No `code` on the update request: the code is immutable from creation following `Account`'s precedent, so
the wire shape has no field for it and a caller who sends one gets a 400 from the strict reader rather than
a silently ignored property.

### Runs

| Method | Route | Permission |
|---|---|---|
| POST | `/api/payroll/runs` | `Payroll.Runs.Manage` |
| GET | `/api/payroll/runs` | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{payrollRunId:guid}` | `Payroll.Runs.View` |
| POST | `/api/payroll/runs/{payrollRunId:guid}/calculation` | `Payroll.Runs.Manage` |
| POST | `/api/payroll/runs/{payrollRunId:guid}/approval` | `Payroll.Runs.Approve` |
| POST | `/api/payroll/runs/{payrollRunId:guid}/posting` | `Payroll.Runs.Post` |

Each transition is a **named action**, not a status field on a PUT. A payload-dispatch route
(`PUT {status: "approved"}`) would let the most sensitive act in the module arrive through the same door as
an ordinary edit.

**Calculation, approval and posting take no request body.** Everything each needs is on the run it names,
and a body would let a caller change what is being approved at the moment of approval — the same reasoning
that gives GL's posting route no body.

### Payslips

| Method | Route | Permission |
|---|---|---|
| GET | `/api/payroll/runs/{payrollRunId:guid}/payslips/{employeeId:guid}` | `Payroll.Payslips.View` |
| GET | `/api/payroll/employees/{employeeId:guid}/payslips` | `Payroll.Payslips.View` |

A **projection over stored run lines** (`OD-PAY-0015` option 1), not a document — there is no document
store (`DEC-PAY-0013`).

**Fifteen routes**, provisional.

---

## Response shapes — two notes worth fixing early

**Currency is projected, never stored and never accepted.** `ADR-027` decision 2: the company's
`BaseCurrencyCode` appears on every monetary response because an amount without a currency is unreadable,
and a request that *supplies* one is refused by the strict reader as an unknown property — the reader doing
its ordinary job, not a special case.

**A payslip response carries its lines and its total, and the lines sum to the total.** Under
`OD-PAY-0008`'s recommended rounding this holds by construction. Under option 2 it does not, and the
response would need to explain a discrepancy — which is the clearest practical argument for option 1.

---

## Not proposed

**No DELETE anywhere.** The absence is intended to be asserted by a test, as GL does, so that adding the
verb would require deleting the test.

**No route exposes another module's data.** Payroll does not proxy HR employee reads or GL account reads;
a client that needs those calls those modules.

**No bulk import of compensation.** HR has one because `REQ-HR-0005` and the `OD-DOC-001` split created it.
No equivalent payroll requirement exists, and inventing one would repeat the mistake FP-011 declined to
make with GL.
