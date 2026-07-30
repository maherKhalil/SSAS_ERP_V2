---
id: ADR-010
title: Adopt the Repository Pattern for Aggregate Persistence
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - repository
  - persistence
  - clean-architecture
  - ddd
depends_on:
  - ADR-002
  - ADR-003
  - ADR-004
  - ADR-008
used_by:
  - Platform
  - HR
  - GL
  - Payroll
  - Inventory
  - Purchasing
  - Sales
---

# ADR-010: Adopt the Repository Pattern for Aggregate Persistence

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 follows Clean Architecture and Domain-Driven Design principles.

Business logic must remain independent of persistence technology.

Direct access to Entity Framework Core from the Application layer would tightly couple business logic to infrastructure and make testing more difficult.

The Repository Pattern provides a persistence abstraction while preserving aggregate boundaries.

---

# Problem Statement

The application requires a consistent mechanism for loading and persisting domain aggregates without exposing Entity Framework Core to business logic.

The solution must:

- Preserve Clean Architecture.
- Support dependency inversion.
- Improve testability.
- Protect aggregate boundaries.
- Allow future persistence changes.

---

# Decision

SSAS ERP V2 shall adopt the Repository Pattern.

Repositories are responsible only for aggregate persistence and retrieval.

Business logic shall never exist inside repositories.

Repositories shall expose domain-focused operations rather than generic CRUD methods.

---

# Decision Drivers

The decision is based on:

- Clean Architecture
- Domain-Driven Design
- Separation of concerns
- Testability
- Long-term maintainability
- AI-assisted code generation consistency

---

# Repository Responsibilities

Repositories shall:

- Load aggregates.
- Persist aggregates.
- Delete aggregates when required.
- Execute aggregate-specific queries.
- Hide Entity Framework Core implementation.

Repositories shall not:

- Implement business rules.
- Perform authorization.
- Perform validation.
- Manage transactions.
- Call external services.

---

# Aggregate Ownership

Each Aggregate Root owns exactly one repository.

Examples:

```
EmployeeAggregate
    ↓
IEmployeeRepository

CompanyAggregate
    ↓
ICompanyRepository

JournalAggregate
    ↓
IJournalRepository

InvoiceAggregate
    ↓
IInvoiceRepository
```

Repositories shall never span multiple aggregate roots.

---

# Repository Interface Location

Repository interfaces belong in the **Application** layer.

Example:

```
Application

    Employees

        Interfaces

            IEmployeeRepository.cs
```

---

# Repository Implementation

Repository implementations belong in the **Infrastructure** layer.

Example:

```
Infrastructure

    Persistence

        Repositories

            EmployeeRepository.cs
```

The Application layer shall never reference concrete repository implementations.

---

# Query Strategy

Repositories shall support only aggregate-focused queries.

Examples:

```
GetByIdAsync()

GetByEmployeeNumberAsync()

ExistsAsync()

AddAsync()

RemoveAsync()
```

Large reporting queries shall not be implemented inside repositories.

Instead, they shall use:

- Query Handlers
- Read Models
- Reporting Services

---

# Generic Repository

Generic repositories shall not be used.

Example (Not Allowed):

```
Repository<TEntity>
```

Reasons:

- Breaks aggregate boundaries.
- Encourages anemic domain models.
- Promotes CRUD-centric development.
- Reduces domain expressiveness.

Each repository shall model the language of its business domain.

---

# Dependency Inversion

The Application layer depends only on interfaces.

Example:

```
Application

↓

IEmployeeRepository

↓

Infrastructure

↓

EmployeeRepository

↓

Entity Framework Core
```

Infrastructure depends on Application.

Application never depends on Infrastructure.

---

# Entity Framework Core

Repositories shall use Entity Framework Core internally.

EF Core shall remain an implementation detail.

The Application layer shall not:

- Reference DbContext.
- Reference DbSet.
- Execute LINQ against DbContext.
- Use EF-specific APIs.

Reference: ADR-008.

---

# Multi-Tenancy

Repositories shall automatically enforce tenant isolation.

Requirements:

- Apply TenantId filtering.
- Never expose another tenant's data.
- Reject cross-tenant access.

Developers shall never manually bypass tenant filtering.

Reference: ADR-005.

---

# Performance

Repositories shall:

- Return aggregates only when necessary.
- Avoid loading unnecessary relationships.
- Use projections for read-only scenarios.
- Prefer asynchronous APIs.
- Support pagination where appropriate.

Performance optimizations shall be evidence-based.

---

# Transactions

Repositories shall not manage transactions.

Transaction management is the responsibility of the Unit of Work.

Reference: ADR-011.

---

# Testing

Repositories shall support:

- Unit testing through interface mocking.
- Integration testing with SQL Server.
- Automated tenant isolation tests.

---

# Alternatives Considered

## Repository Pattern (Selected)

Advantages

- Clean separation of concerns.
- Supports dependency inversion.
- Better testability.
- Protects aggregate boundaries.

Disadvantages

- Additional abstraction layer.
- More interfaces to maintain.

---

## Direct DbContext Usage

Advantages

- Less code.
- Simpler implementation.

Disadvantages

- Tight coupling.
- Difficult testing.
- Business logic becomes persistence-aware.

Rejected.

---

## Generic Repository

Advantages

- Less code duplication.

Disadvantages

- Poor domain modeling.
- Weak aggregate boundaries.
- Encourages generic CRUD.

Rejected.

---

# Consequences

## Positive

- Infrastructure independence.
- Better maintainability.
- Better testing.
- Strong aggregate boundaries.
- Consistent implementation.

## Negative

- More interfaces.
- Slightly more boilerplate.
- Requires architectural discipline.

---

# Implementation Guidelines

Developers and AI assistants shall:

- Create one repository per aggregate.
- Keep repositories focused on persistence.
- Use asynchronous methods.
- Return domain aggregates.
- Keep repository interfaces expressive.
- Avoid exposing IQueryable.
- Never inject DbContext into controllers or handlers.

---

# Compliance Rules

Every repository shall:

- Represent exactly one aggregate root.
- Be defined as an interface in the Application layer.
- Be implemented in the Infrastructure layer.
- Enforce tenant isolation.
- Remain free of business logic.
- Use EF Core internally.

Architecture reviews shall verify compliance.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Repository bloat | Keep repositories aggregate-focused |
| Generic CRUD design | Use domain-specific methods |
| Persistence leakage | Hide EF Core behind interfaces |
| Cross-tenant access | Automatic tenant filtering and tests |

---

# Depends On

- ADR-002 – SQL Server
- ADR-003 – Clean Architecture
- ADR-004 – CQRS
- ADR-008 – Entity Framework Core

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-001 | Repository implementations follow the Modular Monolith architecture |
| ADR-004 | CQRS handlers depend on repositories for persistence |
| ADR-005 | Repositories automatically enforce tenant isolation |
| ADR-006 | Repositories execute within the authenticated user and tenant context |
| ADR-008 | EF Core is the persistence technology used internally |
| ADR-009 | Domain Events are raised by aggregates loaded through repositories |
| ADR-011 | Unit of Work coordinates repository transactions and commits |

---

# Related Documents

- Solution Architecture Document
- Architecture Principles
- Development Standards
- Coding Standards
- Sprint-00 Foundation

---

# Review Criteria

This ADR shall be reviewed if:

- The persistence technology changes significantly.
- Aggregate boundaries are redefined.
- The architecture adopts event sourcing.
- A future migration to microservices requires a different repository strategy.

Until then, the Repository Pattern remains the mandatory persistence abstraction for SSAS ERP V2.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |