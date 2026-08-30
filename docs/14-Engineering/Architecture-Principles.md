---
id: ENG-ARCH-001
title: Architecture Principles
category: Engineering Standards
version: 1.0
status: Approved
owner: Solution Architecture Team
---

# Architecture Principles

## Purpose

This document defines the mandatory architectural principles governing the design and implementation of SSAS ERP V2.

Unlike Architecture Decision Records (ADRs), which explain *why* architectural decisions were made, this document specifies *how* all developers and AI coding agents must implement the system consistently.

These principles are mandatory for every module, service, and feature.

---

# Guiding Principles

The architecture shall prioritize:

- Maintainability
- Scalability
- Modularity
- Security
- Testability
- Performance
- Simplicity
- Consistency
- AI-assisted development

---

# Principle 1 – Modular Monolith

The application shall be implemented as a Modular Monolith.

Modules communicate through well-defined contracts and shall not access each other's internal implementation.

Reference: ADR-001

---

# Principle 2 – Clean Architecture

Dependencies always point inward.

```
Presentation
    ↓
Application
    ↓
Domain
    ↑
Infrastructure
```

The Domain layer must not depend on any external framework.

Reference: ADR-003

---

# Principle 3 – CQRS

Commands modify state.

Queries return data.

A request shall never perform both responsibilities simultaneously.

Reference: ADR-004

---

# Principle 4 – Multi-Tenant by Default

Every business operation executes within a tenant context.

Every tenant-owned entity shall include `TenantId`.

Cross-tenant access is prohibited unless explicitly approved.

Reference: ADR-005

---

# Principle 5 – Security First

Authentication uses JWT Bearer Tokens.

Authorization is claims- and permission-based.

Never trust client-provided identity or authorization data.

Reference: ADR-006

---

# Principle 6 – Angular Frontend

All web user interfaces shall use Angular.

Business logic belongs in backend services, not Angular components.

Reference: ADR-007

---

# Principle 7 – Entity Framework Core

EF Core is the standard ORM.

Use Fluent API configuration.

Use migrations for schema evolution.

Avoid Lazy Loading.

Reference: ADR-008

---

# Principle 8 – Domain Events

Modules communicate through Domain Events.

Events represent completed business facts.

Publish events only after successful transaction commits.

Reference: ADR-009

---

# Principle 9 – Repository Pattern

Repositories encapsulate persistence for aggregate roots.

Business logic shall never exist inside repositories.

Reference: ADR-010

---

# Principle 10 – Unit of Work

Every request executes within a single Unit of Work.

Transactions are committed once.

Domain Events are published after a successful commit.

Reference: ADR-011

---

# Principle 11 – Branch-Scoped Execution Context

Tenant ownership answers *whose data is this*. Branch ownership answers *which operating location inside that tenant produced it*. They are independent dimensions.

Every tenant entity shall be **explicitly classified** as tenant-global or branch-owned. There is no default, and unclassified is a defect: an entity that should have been branch-scoped and was not is readable by every branch in the tenant, and nothing about it looks wrong.

Branch-owned entities shall implement `IBranchOwnedEntity` in addition to `ITenantOwnedEntity`, and carry both `TenantId` and `BranchId`.

`BranchId` shall be assigned by the server from the authenticated execution context. It shall never be accepted from a request DTO, header, form field, or token claim, and shall never change after the record is created.

Branch authorization shall be re-evaluated against live state on every branch-owned write and shall fail closed.

Branch-scoped queries shall carry an explicit `BranchId` predicate over the current branch or an authorized branch set. Omitting the predicate is a defect, not an optimization.

Reference: ADR-023

---

# General Rules

Developers and AI coding agents shall:

- Keep controllers thin.
- Keep business logic in Application and Domain layers.
- Never bypass tenant isolation.
- Never bypass branch scoping on branch-owned data.
- Never accept `BranchId` from client-supplied request data.
- Never inject DbContext into controllers.
- Never expose entities directly through APIs.
- Always use DTOs.
- Use asynchronous APIs where supported.
- Prefer constructor injection.
- Keep classes focused and cohesive.

---

# Naming Conventions

- One class per file.
- One public type per file.
- PascalCase for types.
- camelCase for local variables.
- Feature-first folder structure.
- One repository per aggregate.
- One handler per command or query.

---

# Dependency Rules

Allowed dependencies:

- Presentation → Application
- Application → Domain
- Infrastructure → Application
- Infrastructure → Domain

Forbidden dependencies:

- Domain → Infrastructure
- Domain → Presentation
- Application → Presentation

---

## Composition Root Exception

SSAS.Host.API may reference approved Platform, HR, and GL API and Infrastructure projects solely for dependency injection, configuration, middleware coordination, and endpoint mapping. This exception does not permit business logic in the Host. Module API projects must not reference Infrastructure. Module Application and Domain projects must not reference Host or Infrastructure. Cross-module business communication must use approved public contracts, integration events, or explicitly authorized module-facing abstractions. Direct references to another module's internal Domain, Application, API, or Infrastructure assemblies are forbidden.

---

# AI Coding Guidelines

AI coding agents shall:

- Follow all ADRs.
- Never introduce architectural shortcuts.
- Preserve module boundaries.
- Reuse shared abstractions.
- Generate tests for new functionality.
- Document public APIs.
- Respect coding standards.
- Avoid duplicate implementations.

---

# Code Review Checklist

Every pull request should verify:

- Architecture compliance.
- Tenant isolation.
- Branch classification of every new tenant entity.
- Authorization checks.
- Unit tests.
- Naming conventions.
- Logging.
- Error handling.
- Performance considerations.
- Documentation updates.

---

# Exceptions

Any deviation from these principles requires approval from the Solution Architect and, where appropriate, a new ADR documenting the change.

---

# Principle 12 – Malformed Input Is a 400, Including in the Route Path

**A syntactically invalid value returns `400 Bad Request` with a problem document, wherever it arrives** —
route path, query string, header, body or rowversion.

**Route paths were the exception until 2026-08-30.** 71 route parameters carried type constraints such as
`{id:guid}`. A constraint is evaluated during route *matching*, so a malformed value matched no route and
the framework answered **404 before any module code ran**. The constraints have been removed; the value now
reaches parameter binding, fails to bind, and produces a 400 naming the offending parameter.

**The reason is not consistency for its own sake. A 404 makes a malformed identifier indistinguishable from
an absent record** — a caller cannot separate *"your GUID is not a GUID"* from *"that record is gone"*. A
400 with a problem document can say which. This mattered on 71 routes, permanently, and cost nothing to fix
while the product has no external consumers.

**Enforced, not merely stated:** `RouteConstraintArchitectureTests` reddens when a constrained route
parameter is reintroduced, and product-wide the count is **0**. **An exception requires an allowlist entry
carrying a stated reason** — a route constraint is legitimate where it disambiguates two sibling parameter
routes, and the check that establishes this is a measurement, not an assumption.

# Principle 13 – When the Exception Type Is Already the Reason

**A discarded exception normally needs a stated reason. It does not need one when the type at a parse
boundary IS the reason.**

`FormatException`, `JsonException`, `BadHttpRequestException` and `DecoderFallbackException`, caught at a
parsing or deserialisation boundary and answered with `false` or a 400, are self-describing. **A comment
there restates the catch**, and comments that restate their code teach readers to skip comments — including
in the places where a real reason is the only thing standing between a maintainer and a silent failure.

**This exemption is narrow and does not extend by analogy.** It covers a parse boundary where the type
names the failure and the response is a refusal. It does **not** cover:

- **a broad `Exception` catch**, where distinct causes collapse into one observable — the same defect
  Principle 12 removes from the routing layer;
- **a persistence exception**, where the discarded inner error distinguishes a unique violation from a
  deadlock, and only one of those is retryable;
- **a teardown or cleanup catch**, where the reason is *"this must not fail the test"* and the cost of
  omitting it is a fixture that starts failing for a new reason and says nothing;
- **code that looks careless and is not** — where the generic message is deliberate, the reason is
  required, because the reader's default inference is wrong.

**Measured 2026-08-30:** 207 catch clauses, 94 discarding the exception, 54 carrying a reason. **19 of
those 54 state their reason above the enclosing `try` rather than above the `catch`** — a legitimate and
common shape here, and one an instrument scanning only the lines above a `catch` will report as unreasoned.

# Related Documents

- All accepted ADRs (001-012)
- Solution Architecture Document
- Development Standards
- Coding Standards
- Functional Specifications
- Sprint Documentation

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |
