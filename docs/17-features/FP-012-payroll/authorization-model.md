# FP-012 — Authorization Model (PROPOSED)

**Pay data is the most sensitive read surface this product will have.** That sentence governs the whole
page, and it is the reason the read side gets more attention here than in any previous package.

---

## The precedent, applied with more force

`DEC-POS-0018` separated `HR.SalaryGrades.View` from ordinary HR reads when the data in question was
merely **structural** — a band attached to a job, disclosing pay policy but no person's pay.

FP-012 holds **individual compensation**. If a structural band warranted its own permission, an individual's
salary warrants at least as much, and the argument for folding it into an HR permission is correspondingly
weaker.

`DEC-DOC-0015` supplies the counter-discipline so this does not become permission inflation: it *declined*
an additive permission because export read exactly what search read — no new boundary, no new permission.
FP-011 then *granted* `GL.Reports.View` because a trial balance **aggregates**, revealing totals an
individual enquiry surfaces one at a time — same rule, different fact.

**Applied here:** a permission is added when it exposes something a caller could not otherwise see, and not
otherwise.

---

## Proposed permissions

Grammar `<Plane>.<Resource>.<Action>`, per the constant-file + catalog-contributor pattern.

| Permission | Grants | Why separate |
|---|---|---|
| `Payroll.Compensation.View` | Read an individual's compensation record | **The sensitive read.** Nothing in HR grants it (`BR-PAY-0010`) |
| `Payroll.Compensation.Manage` | Create a dated compensation record | Setting someone's pay is not the same act as reading it |
| `Payroll.Elements.View` | Read the tenant's pay element definitions | Structural, not personal — deliberately weaker than `Compensation.View` |
| `Payroll.Elements.Manage` | Define elements and their GL mapping | Changes what every future run computes and where it posts |
| `Payroll.Runs.View` | See runs, their status and totals | A run's existence and status is operational, not personal |
| `Payroll.Runs.Manage` | Create and calculate a run | Preparation |
| `Payroll.Runs.Approve` | **Approve a run** | `BR-PLT-0103` — the sensitive act (`OD-PAY-0009`) |
| `Payroll.Runs.Post` | Post an approved run to GL | Separate from approval, mirroring `GL.Drafts.Manage` / `GL.Journals.Post` |
| `Payroll.Payslips.View` | Read run lines for an individual | The other personal read surface (`OD-PAY-0015`) |

Nine, pending `OD-PAY-0016`.

**`Runs.Approve` and `Runs.Post` are separate.** They could plausibly be one. They are kept apart because
posting is the act that touches *another module's* ledger under `GL`'s own sensitivity regime, and because
GL already established that authorizing content and committing it are different grants. If the owner
merges them, the merge should be a decision rather than a default.

---

## The read scope

**Unforgeable, always** (`DEC-PAY-0006`). A scope is constructed by a resolver from the caller's grants and
is never accepted from the caller.

This matters more here than anywhere it has mattered before. Everywhere else in the product a forgeable
scope is an authorization defect; **for compensation it is a personal-data breach.** The `GlReadScope` /
`GlScopeResolver` shape — a guarded constructor a caller cannot populate — is the pattern to follow.

`FP-011` recorded a promotion trigger on exactly this type: `GlReadScope`'s company set duplicates HR's
`AuthorizedCompanyScope`, deliberately, with **"a third consumer"** named as the trigger to promote into
`SSAS.BuildingBlocks`, written where the type lives *because drift in a scope type is a security defect
rather than an inconvenience*.

**Payroll is that third consumer.** The trigger fires here. `ADR-027` decision 4 sanctions promotion but
does not mandate it and is explicit that promotion is a reviewed change to shared foundations — so this is
flagged for the architect, not performed by this package.

---

## Self-service, and an assumption not being made

`OD-PAY-0016` option 3 is a self-service scope: an employee reads **their own** payslip and nothing else.
It is genuinely useful and probably the first thing anyone will ask for.

**It depends on a mapping from the authenticated identity to an employee record, and this package does not
assert that such a mapping exists.** It should be verified in the repository before any requirement relies
on it. Writing "the employee views their own payslip" without checking is exactly the shape of assumption
that produced FP-011's near-miss — plausible, conventional, and not how this repo necessarily works.

---

## Company scope

Every payroll permission is evaluated **within a company** (`OD-PAY-0005`). A grant in one company conveys
nothing in another, following `ICompanyOwnedEntity` throughout the product.

The company context is established by the platform's `ICompanyContextEstablisher`; the endpoint filter that
binds it per module is small and module-specific — GL wrote its own fifteen-line
`GlCompanyContextEndpointFilter` rather than promoting one, and Payroll would do the same.

---

## What is not proposed

**No `Payroll.Admin` or wildcard permission.** Nothing in the product has one, and a wildcard over the most
sensitive data in the product would be the worst place to introduce the idea.

**No per-employee ACL.** Scope is by company (and possibly self), not by individual employee. A per-employee
grant model is a substantially larger authorization design and nothing asks for it.

**No approval-limit thresholds** — "runs over X need a second approver". Plausible, unauthored, and it would
need a currency-aware threshold per company; not proposed.
