---
id: ADR-009
title: Adopt Domain Events for Inter-Module Communication
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - domain-events
  - ddd
  - modular-monolith
  - integration
depends_on:
  - ADR-001
  - ADR-003
  - ADR-004
used_by:
  - All Modules
---

# ADR-009: Adopt Domain Events for Inter-Module Communication

---

# Status

**Accepted**

---

## ⚠ IMPLEMENTATION STATUS 2026-08-30 — ACCEPTED, AND NOT IMPLEMENTED

**Read this before writing a handler against this ADR. The publishing flow below describes a mechanism the
product does not currently have.**

**Measured 2026-08-30 across `src/`:**

- **`RaiseDomainEvent` is called 65 times** — departments, employees, job grades and others. Events are
  raised exactly as this ADR specifies.
- **Nothing consumes them.** `DequeueDomainEvents`, `ClearDomainEvents` and the `DomainEvents` property
  have **no production reader**. Checked three ways; none appears outside `AggregateRoot.cs` and its
  interface.
- **There is no dispatcher.** No `IDomainEventDispatcher`, and no type of any name that reads those members.

**So every domain event raised in this product is appended to a list on its aggregate and discarded when
that aggregate goes out of scope.** Nothing is visibly broken, **because nothing depends on the events —
which is exactly why this went unnoticed.**

**⚠ The risk this note exists to prevent: a handler written against this ADR, in good faith, subscribing to
an event that will never be delivered.** That handler would compile, pass review, and never run.

**This does not retract the decision.** The ADR may still be the intended architecture and the dispatcher a
later phase — **but that deferral was never recorded, and three Accepted ADRs describe the mechanism as
though it exists**: this one's publishing flow, ADR-008's *"Domain Events are dispatched after successful
persistence"*, and ADR-004's *"Publish domain events only from successful commands"*.

**Whether to build the dispatcher or to record the deferral formally is the owner's call.** Until one of
those happens, treat the publishing flow below as **specification, not description**.

# Context

SSAS ERP V2 is implemented as a Modular Monolith where business modules must remain independent while still collaborating.

Examples include:

- Employee Created
- Employee Terminated
- Company Activated
- Journal Posted
- Invoice Approved
- Purchase Order Received
- Payroll Completed

Direct module-to-module dependencies increase coupling and reduce maintainability.

---

# Problem Statement

Business operations frequently require actions in multiple modules.

Example:

Employee Created

↓

HR

↓

Create Identity Account

↓

Assign Default Role

↓

Generate Audit Record

↓

Send Welcome Notification

Without a standardized communication mechanism, modules become tightly coupled.

---

# Decision

Modules shall communicate through **Domain Events**.

A Domain Event represents something significant that has already occurred within the business domain.

Events describe facts, not commands.

Examples:

- EmployeeCreated
- EmployeeUpdated
- CompanyCreated
- LeaveApproved
- PayrollCompleted
- JournalPosted

---

# Event Principles

Domain Events shall:

- Be immutable.
- Represent past-tense business facts.
- Be raised by the Domain.
- Be handled in the Application layer.
- Never contain business logic.
- Never depend on Infrastructure.

---

# Publishing Flow

```
Command

↓

Domain Entity

↓

Raise Domain Event

↓

Save Changes

↓

Commit Transaction

↓

Publish Event

↓

Execute Event Handlers
```

Events shall only be published after a successful transaction.

---

# Event Handlers

Handlers may:

- Update read models.
- Trigger notifications.
- Invoke other application workflows.
- Produce audit records.
- Schedule background work.

Handlers shall remain idempotent whenever practical.

---

# Synchronous vs Asynchronous

Version 1:

- In-process synchronous dispatch after commit.

Future versions may introduce:

- Message brokers
- Service Bus
- Kafka
- RabbitMQ
- Azure Service Bus

No business code should require changes if the dispatch mechanism evolves.

---

# Event Naming

Events shall use past-tense names.

Examples:

EmployeeCreated

JournalPosted

InvoiceApproved

PayrollGenerated

---

# Event Payload

Events should contain only the information required by consumers.

Avoid exposing entire aggregates unless necessary.

---

# Multi-Tenancy

Every tenant-owned event shall include:

- TenantId
- CorrelationId
- EventId
- OccurredAt

This preserves tenant isolation throughout the event pipeline.

---

# Reliability

Events shall only be dispatched after successful persistence.

If persistence fails, no Domain Events shall be published.

---

# Alternatives Considered

## Direct Service Calls

Advantages

- Simple

Disadvantages

- Tight coupling
- Difficult maintenance

Rejected.

---

## Message Broker

Advantages

- Excellent scalability

Disadvantages

- Operational complexity

Deferred until microservices.

---

# Consequences

Positive

- Loose coupling
- Better extensibility
- Easier migration to microservices
- Better AI-generated code consistency

Negative

- More event classes
- Additional infrastructure abstraction

---

# Implementation Guidelines

Developers shall:

- Raise events only from aggregates.
- Publish after successful commit.
- Keep events immutable.
- Keep handlers focused.
- Avoid circular event chains.

---

# Compliance Rules

Every Domain Event shall:

- Be immutable.
- Be named in past tense.
- Include CorrelationId.
- Include TenantId when applicable.
- Execute after persistence.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Event loops | Review event dependencies |
| Duplicate processing | Idempotent handlers |
| Event ordering | Publish after commit |

---

# Depends On

- ADR-001
- ADR-003
- ADR-004

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-005 | Events preserve tenant boundaries |
| ADR-006 | Events execute under authenticated user context |
| ADR-008 | Events are persisted using EF Core transactions |
| ADR-010 | Unit of Work coordinates event dispatch |

---

# Related Documents

- Solution Architecture
- Development Standards
- Sprint-00 Foundation

---

# Review Criteria

Review if:

- Event sourcing is adopted.
- Microservices become the deployment model.
- External messaging becomes mandatory.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |