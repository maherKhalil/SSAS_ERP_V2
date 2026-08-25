---
id: ADR-029
title: Tokenized Payment Capture and the Cardholder Data Boundary
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-25
owner: Solution Architecture Team
tags:
  - payments
  - subscription
  - commercial-plane
  - cardholder-data
  - tokenization
  - modular-monolith
  - architecture
depends_on:
  - ADR-001
  - ADR-012
  - ADR-017
  - ADR-027
used_by:
  - FP-014
---

# ADR-029: Tokenized Payment Capture and the Cardholder Data Boundary

# Status

**Accepted**

Accepted rather than `Proposed`, and the difference is deliberate. Most ADRs here stand at `Proposed`
because their feature has not shipped — `ADR-024` through `ADR-027` all do. This one records a ruling
already made (`DEC-L-018`, 2026-08-25) whose entire purpose is to constrain code that does not exist
yet. A boundary that arrives after the first component crosses it has not been drawn; it has been
described.

# Context

`OD-SUB-0016` ruled that the product **issues invoices and captures payment itself**, rather than
delegating billing to an external system. That ruling is not in question here.

What was in question is what "captures payment itself" means for this codebase, because the phrase
admits two readings that could not be further apart in their consequences.

**Under the literal reading — the card number reaches SSAS** — every component sharing the process
comes into assessment scope, because scope follows the data rather than the intent. `ADR-001` commits
this product to a modular monolith: one process, one deployable, modules separated by contract rather
than by boundary. So the literal reading forces one of two outcomes, and both are bad:

- **Carve a cardholder-data environment out of the monolith**, which is a direct contradiction of
  `ADR-001` and would need it qualified for one module — the first exception to the product's
  foundational structural decision, taken for its least architectural feature.
- **Or accept the whole monolith into scope**, which imposes the constraints of a payment system on
  HR, General Ledger, Payroll and Attendance — modules that will never touch a card — for the rest of
  the product's life.

FP-014 identified this tension while modelling the commercial plane and deliberately refused to
resolve it: `domain-model.md`, `data-model.md` and `api-contracts.md` each stop where capture
mechanics begin and name this ADR as the owner. **A feature package settling a question of this size
by drawing a table would have been the defect, not the delay.**

# Decision

## Decision 1 — Capture is tokenized. No cardholder data enters SSAS, ever.

The primary account number, the card verification value, the cardholder name and the expiry date are
captured **by the payment provider**, through hosted fields or a redirect, and are exchanged for an
opaque token.

**They do not traverse any SSAS component.** Not a controller, not a DTO, not a domain type, not a
column, not a log, not a queue, not a crash dump. The product receives a token that is meaningless
outside the provider's systems and stores that.

The word *ever* is load-bearing. This is not a default to be revisited per feature; it is the boundary
the rest of this ADR depends on.

## Decision 2 — `ADR-001` is not qualified and needs no exception

**The modular monolith stands unamended.**

The tension described in Context exists only under the reading decision 1 forecloses. With no
cardholder data inside the process, there is no cardholder-data environment to carve out and no
argument for treating one module differently from the others. The product remains one deployable with
modules separated by contract.

This is the substantive reason to rule the shape rather than leave it to the build: **the alternative
was not "a slightly different payment design", it was the first structural exception to `ADR-001`.**

## Decision 3 — The product owns everything about a payment except the instrument

Tokenization is not delegation of the commercial plane. SSAS owns:

- the **invoice** — its numbering, issue, immutability after issue, and void;
- the **payment flow** — when a payment is attempted, against which invoice, and what it is for;
- the **outcome and state** — attempted, succeeded, failed, and the invoice's settlement state;
- **reconciliation** — matching provider outcomes to invoices and surfacing what does not match.

What it does not own is the **instrument**. The provider holds the card; SSAS holds a token, an
outcome and a reference.

`OD-SUB-0016` is therefore satisfied in substance: the product bills its tenants, and does not hand
that to a third-party billing system.

## Decision 4 — No SSAS type may be capable of holding cardholder data

The consequence a future package must respect, and the one that is mechanically checkable:

> **No entity, value object, DTO, request body, response body, log statement or diagnostic in this
> repository may declare a field capable of holding a primary account number, a card verification
> value, a cardholder name or an expiry date.**

The rule is about **capability, not content**. A nullable string field named `CardNumber` that is
never populated still violates it, because the boundary is enforced by the absence of a place to put
the data, not by the discipline of not putting it there. A field that exists will eventually be
filled.

**FP-014 is the first application of this rule, not its source.** Its `data-model.md` states that
`SubscriptionPaymentAttempt`'s column list is complete and holds only an opaque `ProviderReference`;
its `api-contracts.md` extends the same statement to request bodies, response bodies and logging.
Those statements were written before this ADR and are now instances of it.

## Decision 5 — The provider is not chosen here

This ADR rules the **shape** — tokenized, provider-hosted capture — and not the vendor. Which provider,
on what commercial terms, is a business decision that has not been made and is not architecture's to
make. Any provider satisfying decision 1 satisfies this ADR.

# Consequences

## Positive

- **`ADR-001` survives intact**, and the product avoids its first structural exception being taken for
  its least architectural feature.
- **The boundary is checkable rather than procedural.** Decision 4 is a statement about type surface,
  which reflection can assert — the same shape as the guards that now protect append-only enforcement
  and the platform authorization plane.
- **The boundary exists before the code does.** Every future payment component is written against it
  rather than retrofitted to it, which is the only sequencing in which a rule of this kind is cheap.
- **HR, GL, Payroll and Attendance are unaffected, permanently.** No module that never touches a card
  inherits a constraint from one that does.

## Negative

- **The product cannot offer a fully in-page card form of its own design.** Hosted fields and redirects
  constrain the checkout experience, and that is a real cost accepted knowingly.
- **A provider outage is a payment outage**, and the product cannot fall back to capturing the card
  itself — decision 1 forecloses the fallback as well as the primary path. That is the intended
  consequence of *ever*.
- **Provider migration is more than a configuration change**, because tokens are provider-specific and
  do not port. Nothing in this ADR mitigates that.

## Risks

- **The rule erodes at the edges rather than at the centre.** Nobody will propose a `Pan` column. The
  realistic failure is a free-text `Notes`, `Description` or `Metadata` field on a payment type, into
  which a support process pastes a card number. Decision 4's capability test is the mitigation and it
  is weakest exactly here, because such a field is capable by accident.
- **Diagnostics are the second edge.** A provider callback logged verbatim during an incident is
  stored cardholder data if the provider ever includes more than a token in it. The
  no-logging clause covers it; the risk is that it is remembered under pressure.
- **"Tokenized" is not self-defining.** A provider whose "token" is a reversible encoding of the PAN
  would satisfy the word and violate the decision. The test is whether the value is meaningless
  outside the provider, not whether it is called a token.

# Alternatives Considered

**Capture the card in SSAS and carve out a cardholder-data environment.** Rejected: it contradicts
`ADR-001` directly, and would make the product's first structural exception a payment form.

**Capture the card in SSAS and accept the whole monolith into scope.** Rejected: it charges four
modules that will never see a card for the privilege of one that will, permanently.

**Delegate billing entirely to an external system.** Rejected because `OD-SUB-0016` already ruled
against it — the product issues its own invoices. This ADR is what makes that ruling compatible with
`ADR-001` rather than a reason to revisit it.

**Say nothing and let the first payment package decide.** Rejected on sequencing. A boundary drawn
after a component has crossed it is not a boundary, and FP-014 correctly declined to draw it from
inside a feature package.

# What this ADR does not decide

Stated explicitly, because each is unauthored today and silence would read as coverage:

- **The provider**, and the commercial terms of using one — decision 5.
- **Settlement, refunds, credit notes, dunning and chargebacks.** `REQ-SUB-0025` and `REQ-SUB-0026`
  are one line each; none of these has a requirement behind it.
- **Tax jurisdiction.** Multi-currency invoicing from vendor to tenant crosses jurisdictions and no
  document in this repository names one. `DEC-PAY-0016` is the standing precedent for refusing to
  encode a jurisdiction the product has not named.
- **Where the payment surface sits in the module structure.** It is commercial-plane, Platform-database
  data under `ADR-017`; which module owns it is FP-014's ratification to settle.

# Deferred obligations

**The first package to build the payment surface** must carry decision 4 into an executable guard —
an architecture test asserting that no transport contract type in the repository declares a field
capable of holding cardholder data. FP-014 already specifies one (`AC-SUB-0048`, `TS-SUB-0047`);
this ADR is what that criterion cites.

**Any future ADR proposing to relax decision 1** must state which components enter assessment scope as
a result, and must reopen `ADR-001` explicitly rather than by implication.

---

# Revision History

| Version | Date | Author | Change |
|---|---|---|---|
| 1.0 | 2026-08-25 | Solution Architecture Team | Records `DEC-L-018`: payment capture is tokenized and no cardholder data enters SSAS, so `ADR-001` stands unqualified and needs no exception. Five decisions, covering the boundary, the monolith, what the product still owns, the capability rule future packages must respect, and the deliberate absence of a provider choice. Written to resolve the tension `FP-014` identified and declined to settle from inside a feature package. |
