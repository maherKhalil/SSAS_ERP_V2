---
id: ADR-003
title: Adopt Clean Architecture
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - architecture
  - clean-architecture
  - dependency-inversion
  - modularity
depends_on:
  - ADR-001
used_by:
  - Sprint-00
  - All Modules
---

# ADR-003: Adopt Clean Architecture

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 is a long-term enterprise SaaS ERP platform expected to evolve over many years and support numerous business modules including:

- Platform
- Human Resources
- General Ledger
- Payroll
- Inventory
- Purchasing
- Sales
- Reporting
- Future Modules

The architecture must ensure that business rules remain independent from frameworks, databases, and user interface technologies.

The project also relies heavily on AI-assisted development. Therefore, architectural boundaries must be explicit, enforceable, and easy to validate.

---

# Problem Statement

Without a clear architectural pattern, enterprise applications often experience:

- Tight coupling
- Business logic scattered across layers
- Difficult testing
- Poor maintainability
- Expensive refactoring
- Framework lock-in
- Increasing technical debt

The project requires a structure that isolates business logic from infrastructure concerns.

---

# Decision

SSAS ERP V2 shall adopt **Clean Architecture** as the primary architectural pattern.

The architecture shall enforce strict dependency rules where source code dependencies always point inward toward the Domain layer.

No external framework shall be required by the Domain.

---

# Architectural Layers

The application is divided into the following logical layers.

```
Presentation (API / Web)

↓

Application

↓

Domain

↑

Infrastructure
```

Only inward dependencies are allowed.

---

# Layer Responsibilities

## Presentation Layer

Responsibilities:

- HTTP endpoints
- Controllers
- Authentication
- Request validation
- API versioning
- Swagger configuration

Must NOT contain:

- Business logic
- Database logic

---

## Application Layer

Responsibilities:

- Use Cases
- CQRS Handlers
- DTOs
- Validation
- Interfaces
- Authorization Policies
- Business Workflows

Must NOT contain:

- SQL
- Entity Framework
- HTTP logic

---

## Domain Layer

Responsibilities:

- Entities
- Value Objects
- Domain Services
- Business Rules
- Aggregate Roots
- Domain Events
- Specifications

The Domain layer is the heart of the system.

It must remain independent of all frameworks.

---

## Infrastructure Layer

Responsibilities:

- Entity Framework Core
- SQL Server
- External APIs
- Email
- File Storage
- Authentication Providers
- Logging
- Caching

Infrastructure implements interfaces defined by the Application layer.

---

# Dependency Rules

Allowed

```
Presentation
    ↓
Application
    ↓
Domain

Infrastructure
    ↑
Application
```

Not Allowed

```
Domain → Infrastructure

Domain → API

Application → API

Domain → Entity Framework

Application → SQL Server
```

The Domain layer must never reference Infrastructure.

---

# Dependency Inversion Principle

Interfaces shall be declared in the Application layer.

Infrastructure shall implement those interfaces.

Example

```
Application

IUserRepository

↓

Infrastructure

SqlUserRepository
```

The Domain never knows how data is stored.

---

# Business Logic

Business rules belong only in:

- Domain
- Application

Business rules shall never be implemented in:

- Controllers
- Middleware
- Entity Framework configurations
- SQL Stored Procedures (unless explicitly approved)

---

# Persistence Strategy

Persistence is an implementation detail.

The application shall depend on repository interfaces rather than Entity Framework directly.

Entity Framework Core remains replaceable.

---

# Testing Strategy

Clean Architecture enables:

- Unit Testing of Domain
- Unit Testing of Application
- Mock Infrastructure
- Integration Testing
- End-to-End Testing

Business rules can be tested without SQL Server.

---

# AI Development Considerations

AI-generated code must respect architectural boundaries.

Codex shall:

- Never place business logic inside controllers.
- Never inject DbContext directly into controllers.
- Never bypass the Application layer.
- Never reference Infrastructure from Domain.

Violations must be corrected before merge.

---

# Alternatives Considered

## Traditional Layered Architecture

Advantages

- Simple
- Familiar

Disadvantages

- Tight coupling
- Difficult testing
- Business logic leaks into UI
- Harder maintenance

Rejected.

---

## Onion Architecture

Advantages

- Similar dependency rules
- Strong separation

Disadvantages

- Less intuitive for some developers

Not selected because Clean Architecture provides clearer implementation guidance for the team.

---

## Hexagonal Architecture

Advantages

- Excellent isolation
- Strong ports/adapters model

Disadvantages

- More complexity than currently required

Could be adopted in future if architectural needs change.

---

# Consequences

Positive

- High maintainability
- Independent business logic
- Easier testing
- Easier AI implementation
- Better module isolation
- Lower technical debt
- Easier migration to microservices

Negative

- More projects
- More interfaces
- Additional abstraction
- Slightly steeper learning curve

The benefits outweigh the additional complexity.

---

# Module Organization

Each module shall follow the same structure.

```
Module

├── Domain
├── Application
├── Infrastructure
└── API
```

Example

```
HR

HR.Domain

HR.Application

HR.Infrastructure

HR.API
```

This structure applies to every module in the solution.

---

# Compliance Rules

Every pull request shall verify:

- No forbidden dependencies.
- No business logic in Presentation.
- No infrastructure references from Domain.
- Dependency direction is maintained.
- Unit tests continue to pass.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Excessive abstractions | Introduce abstractions only when justified |
| Layer violations | Architecture reviews and automated validation |
| Increased project count | Consistent solution structure and documentation |
| AI-generated architectural drift | Enforce architecture through documentation and code review |

---

# Implementation Guidelines

Developers and AI assistants shall:

- Keep Domain framework-independent.
- Use constructor injection.
- Depend on abstractions.
- Avoid service locators.
- Keep controllers thin.
- Keep use cases focused.
- Use Domain Events for cross-module communication.
- Follow CQRS for Application workflows.

---

# Related Documents

- ADR-001 – Modular Monolith
- ADR-002 – SQL Server
- ADR-004 – CQRS
- Solution Architecture
- Clean Architecture
- Development Standards
- Sprint-00 Foundation

---

# Review Criteria

This ADR shall be reviewed if:

- The project adopts a fundamentally different architectural style.
- Business logic is intentionally moved outside the Domain/Application layers.
- A migration to distributed services requires revised dependency rules.

Until such changes are formally approved, Clean Architecture remains the mandatory architectural standard for SSAS ERP V2.

# Depends On

- ADR-001 – Modular Monolith

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-002 | Infrastructure uses SQL Server |
| ADR-004 | CQRS exists inside the Application layer |
| ADR-005 | Tenant logic spans Domain, Application, and Infrastructure |
| ADR-006 | Authentication is implemented in the Presentation and Infrastructure layers |
| ADR-008 | EF Core belongs in Infrastructure |
| ADR-009 | Domain Events originate in the Domain layer |
| ADR-010 | Repository interfaces are defined in the Application layer |