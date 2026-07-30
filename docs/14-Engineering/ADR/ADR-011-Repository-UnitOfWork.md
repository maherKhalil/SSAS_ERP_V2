---
id: ADR-011
title: Adopt Repository Pattern and Unit of Work
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - repository
  - unit-of-work
  - persistence
depends_on:
  - ADR-002
  - ADR-003
  - ADR-008
used_by:
  - All Modules
---

# ADR-011: Adopt Repository Pattern and Unit of Work

---

# Status

**Accepted**

---

# Context

Business logic must remain independent from Entity Framework Core.

Repositories abstract persistence.

Unit of Work coordinates transactions across repositories.

---

# Decision

SSAS ERP V2 shall use:

- Repository Pattern
- Unit of Work Pattern

Repositories encapsulate aggregate persistence.

Unit of Work coordinates commits.

---

# Repository Responsibilities

Repositories shall:

- Load aggregates.
- Save aggregates.
- Execute queries.
- Hide EF Core implementation.

Repositories shall NOT contain business logic.

---

# Repository Rules

One repository per aggregate root.

Examples:

EmployeeRepository

CompanyRepository

JournalRepository

InvoiceRepository

---

# Unit of Work

Responsibilities:

- Manage transactions.
- Commit changes.
- Rollback failures.
- Dispatch Domain Events after commit.

One Unit of Work exists per application request.

---

# Transaction Flow

```
Command

↓

Handler

↓

Repository

↓

UnitOfWork

↓

SaveChanges()

↓

Commit

↓

Publish Domain Events
```

---

# Query Strategy

Repositories support:

- Aggregate retrieval
- Persistence

Complex reporting belongs in dedicated query services.

---

# Dependency Inversion

Application depends on:

```
IEmployeeRepository

IUnitOfWork
```

Infrastructure implements:

```
EmployeeRepository

EfUnitOfWork
```

---

# Alternatives Considered

## Direct DbContext

Advantages

- Less code

Disadvantages

- Tight coupling
- Difficult testing

Rejected.

---

## Generic Repository

Advantages

- Less duplication

Disadvantages

- Weak aggregate boundaries
- Poor domain modeling

Rejected.

---

# Consequences

Positive

- Testability
- Maintainability
- Replaceable persistence
- Better domain isolation

Negative

- Additional abstractions
- More interfaces

---

# Implementation Guidelines

Developers shall:

- Keep repositories aggregate-focused.
- Avoid generic CRUD repositories.
- Keep transactions inside Unit of Work.
- Dispatch Domain Events after commit.
- Inject interfaces only.

---

# Compliance Rules

Every module shall:

- Define repository interfaces in Application.
- Implement repositories in Infrastructure.
- Use one Unit of Work per request.
- Never inject DbContext into controllers.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Repository bloat | Aggregate-oriented repositories |
| Large transactions | Small application use cases |
| Hidden queries | Dedicated query services |

---

# Depends On

- ADR-002
- ADR-003
- ADR-008

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-004 | CQRS handlers use repositories |
| ADR-005 | Repositories enforce tenant isolation |
| ADR-006 | Repositories execute within authenticated tenant context |
| ADR-009 | Unit of Work publishes Domain Events after commit |

---

# Related Documents

- Solution Architecture
- Development Standards
- Coding Standards

---

# Review Criteria

Review if:

- Persistence technology changes.
- Microservices require distributed transactions.
- Repository abstraction no longer provides value.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |