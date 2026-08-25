---
id: ADR-030
title: Identity-to-Employee Mapping
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-25
owner: Solution Architecture Team
tags:
  - identity
  - employee
  - self-service
  - multi-tenancy
  - platform
  - hr
  - architecture
depends_on:
  - ADR-012
  - ADR-015
  - ADR-017
  - ADR-020
used_by:
  - FP-012
  - FP-013
---

# ADR-030: Identity-to-Employee Mapping

# Status

**Accepted** — 2026-08-25.

**Both records this ADR stands in a defined relationship to are themselves `Accepted`** — `ADR-017`,
whose residency reasoning this decision follows, and `ADR-015`, whose two-plane separation it must not
disturb. A record that resolves a question left open by an accepted decision cannot itself be
provisional (`DEC-L-021`).

# Context

**Two delivered packages stopped at this exact wall, and the second one is why this is an ADR rather
than a package decision.**

`REQ-ATT-0023` (FP-013) reads *"BLOCKED — NOT IMPLEMENTABLE TODAY: it needs a mapping from the
authenticated identity to an employee record, and no such mapping exists."* `OD-PAY-0016` (FP-012)
deferred payroll self-service for the same missing input. Neither was a scoping preference; both were
the same absence, found independently.

Verified rather than assumed: **`Employee` carries no user or identity reference of any kind**, and
neither HR's domain nor its contracts expose one.

Every self-service capability the product will want — an employee seeing their own payslip, their own
attendance, their own leave balance, submitting their own leave request — needs one question answered:
*given the authenticated caller, which employee record is this?* The whole Self Service roadmap item
sits behind it.

**The obvious answer is a column, and it is wrong in three independent ways.** Because it is obvious,
a future reader will propose it, so the reasoning is recorded here rather than left to be
re-derived — that is what this ADR is for.

# Decision

## Decision 1 — The link is a mapping in the Platform database, keyed by tenant

It holds the tenant user and the employee it names, and it lives with tenant identity and access
membership rather than with HR data.

**`ADR-017` already made this call for the neighbouring case**, and made it deliberately:

> "**Tenant identity/access membership remains Platform database data even though it is
> tenant-scoped.** This is a deliberate refinement of the naive rule 'everything
> `ITenantOwnedEntity` moves to the tenant database'."

*Which employee is this user* is an identity fact about a user, not an HR fact about an employee. It
belongs with membership, and membership is already here.

## Decision 2 — `Employee` gains no column, and neither does the tenant user

**Three independent reasons. Any one of them would be sufficient; a future proposal must answer all
three.**

**1. Residency.** Decision 1. An HR column would put an identity fact in the tenant ERP database,
against the reasoning `ADR-017` applied to membership itself.

**2. The relationship is optional on both sides, so it is a relationship and not an attribute.** Most
employees have no user — an ERP of record holds people who never sign in. And a user need not be an
employee: an external accountant, a support-created administrator, a contractor with system access
and no employment record. **Nullable columns on two aggregates can disagree with each other**, and
nothing in either aggregate's invariants would catch it; a mapping cannot disagree with itself.

**3. Cutover moves one side and not the other — and the reason is subtler than it looks.**
`TenantCutoverCopyPlan.Build` selects by `ITenantOwnedEntity` **within the model it is given**, and it
is given the tenant model. `Employee` is in that model and moves at a Shared→Dedicated cutover.

**The tenant user carries `ITenantOwnedEntity` too — and still does not move**, because it is in the
Platform model, not the tenant one. Cutover membership follows *model residency*, not the marker
interface alone.

So a column on `Employee` would travel to a new database while the identity it names stayed put, and
a column on the tenant user would point into a database that had just been replaced beneath it. **The
two sides are on different sides of a migration boundary**, and a mapping that is itself Platform-side
is unaffected by the move.

## Decision 3 — Optional on both sides, at most one live link each way

A user has at most one live link to an employee; an employee has at most one live link to a user.
Neither is required to have one.

**The cardinality is a rule about what may be true at once, not a schema instruction.** How it is
enforced is the implementing package's to decide.

## Decision 4 — No foreign key exists in either direction, and none can

The two rows live in different databases. This is a stated consequence, not an oversight, and it is
**not novel in this product**:

- `ADR-017` already accepts `CreatedBy`/`ModifiedBy` as plain scalar identifiers with no foreign key,
  observing that the split "introduces no integrity regression — only a resolution problem".
- `DEC-SUB-0009` settled the same rule for FP-014's commercial plane: intra-database keys where the
  rows share a database, values where they do not.

**Referential integrity across this link is the application's to maintain**, exactly as it already is
for every audit-actor reference in the product.

## Decision 5 — Absence is an ordinary answer, never an error

Resolution is a lookup, and **"this caller is not an employee" is a normal result** that every caller
must handle — not an exception, not a 500, not a null that flows onward untested.

This falls out of decision 2's optionality, and it is stated separately because it is where the
implementation will go wrong if it is not. A support administrator opening a self-service page is not
a fault condition; it is Tuesday.

# Consequences

## Positive

- **Two blocked requirements gain a path.** `REQ-ATT-0023` and the capability `OD-PAY-0016` deferred
  become implementable, without either package having to invent the mapping locally.
- **Neither aggregate is disturbed.** `Employee` and the tenant user keep their current shape, so no
  module inherits a schema change from a capability it does not use.
- **Cutover is unaffected.** The mapping is Platform-side; a Shared→Dedicated migration neither moves
  it nor breaks it.
- **The two planes stay separate.** `ADR-015`'s platform/tenant split is untouched: this is a fact
  about a tenant user, and a platform-support principal is not a tenant user.

## Negative

- **A join across databases is not available.** Any read wanting employee detail for a signed-in user
  resolves the mapping first and then reads HR through its contract — two steps where a column would
  have been one, and `ADR-012` forbids reaching into HR's tables directly regardless.
- **Integrity is the application's.** No database constraint prevents a mapping row naming an employee
  that no longer exists. Decision 4 accepts this on the audit-actor precedent, but it is a real cost
  and it lands on whoever writes the deletion paths.
- **One more lookup on a hot path.** Self-service reads happen per request; the mapping is one more
  resolution before the work starts.

## Risks

- **The column will be proposed again.** It is the obvious design and it is one line. The mitigation
  is that decision 2 gives three reasons rather than one, so a proposal that answers only residency
  has not answered the objection.
- **Absence handled as an error.** Decision 5 exists because the failure is silent and plausible: a
  developer tests with an employee-linked user, every path works, and the support administrator finds
  the crash in production.
- **The mapping becoming an authorization surface.** It answers *which employee is this user*, and it
  would be easy to let it start answering *what may this user see*. That is the permission catalog's
  question, and `ADR-015`'s plane separation is what keeps it there.

# Alternatives Considered

**A nullable user reference on `Employee`.** Rejected on all three grounds of decision 2: it puts an
identity fact in HR's database, it makes an optional-on-both-sides relationship an attribute of one
side, and it travels at cutover while the identity it names does not.

**A nullable employee reference on the tenant user.** Rejected for the mirror reason: it would point
into a tenant database that a Shared→Dedicated cutover has just replaced, and it makes the same
optionality mistake from the other direction.

**A mapping in the tenant ERP database.** Rejected: it contradicts decision 1's residency reasoning,
and it would be unreadable during a customer-managed database outage — precisely the failure
`ADR-021` requires the platform-only surfaces to survive.

**Deriving the link from matching attributes** — email, employee number, name. Rejected outright. It
is not a mapping but a guess, it fails silently when the attributes diverge, and it makes a person's
access to their own payroll data depend on a string comparison.

# What this ADR does not decide

Stated explicitly, because each belongs to the package that builds this and silence would read as
coverage:

- **Whether the link carries history** — whether a superseded link is retained and queryable, or
  simply replaced. The product has both patterns in use.
- **How a link is created and revoked**, by whom, and under which permission.
- **What happens when an employee is archived or a tenant user is deactivated** — whether the link
  lapses, is severed, or is left standing as a historical fact.
- **Whether self-service permissions are distinct from delegated ones** — whether *view my own
  payslip* is a different permission from *view an employee's payslip*, which every consuming module
  will need answered.
- **The schema.** No table, column or type is named here. `DEC-L-023` rules the shape; the package
  rules the schema, the way `ADR-028` was left to name its own.

# Deferred obligations

**The package that implements this** must close the five questions above as owner decisions, and must
carry decision 5 into an acceptance criterion — an unlinked caller reaching a self-service surface is
a case a test asserts, not a case a reviewer hopes was considered.

**`FP-012` and `FP-013` are not amended by this ADR.** `REQ-ATT-0023` and `OD-PAY-0016` stand exactly
as written until the package that satisfies them exists. An ADR that unblocked requirements by
editing their packages would leave two documents claiming a capability the product does not have.

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-25 | Solution Architecture Team | Records `DEC-L-023`: the identity-to-employee link is a Platform-database mapping keyed by tenant, with neither `Employee` nor the tenant user gaining a column. Five decisions, covering residency, the three independent reasons against a column, cardinality, the absence of any cross-database foreign key, and absence-as-an-ordinary-answer. Written to remove the wall `REQ-ATT-0023` and `OD-PAY-0016` independently reached; names the schema as the implementing package's to choose. |
