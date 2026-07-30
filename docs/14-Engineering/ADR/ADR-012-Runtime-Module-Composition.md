---
id: ADR-012
title: Runtime Module Composition
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-07-30
owner: Solution Architecture Team
tags:
  - modular-monolith
  - composition-root
  - dependency-injection
depends_on:
  - ADR-001
  - ADR-003
used_by:
  - Sprint-00
  - Host
  - Platform
  - HR
  - GL
---

# ADR-012: Runtime Module Composition

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 is a Modular Monolith with a dedicated Host project. Module APIs
correctly depend on Application and Contracts, while concrete persistence and
external adapters belong in each module's Infrastructure project.

The Host must compose concrete implementations at startup without placing
business logic in the Host or reversing module API dependencies.

---

# Decision

SSAS.Host.API is the composition root.

- The Host may reference the Platform, HR, and GL API and Infrastructure projects.
- This is an outer-layer composition exception only; the Host must not contain business logic.
- Module API projects must not reference Infrastructure projects.
- Module Application and Domain projects remain independent of Host and Infrastructure.
- Infrastructure registration extensions own DbContext, repository, Unit of Work, and external-adapter registration.
- API registration extensions own endpoint mapping.
- The Host coordinates known module registration, middleware, and configuration only.
- Cross-module business communication must use approved public contracts, integration events, or explicitly authorized module-facing abstractions. Direct references to another module's internal Domain, Application, API, or Infrastructure assemblies are forbidden.
- Reflection-based runtime module discovery is not used in V1.
- Architecture tests must enforce these rules.

---

# Rationale

Concrete Infrastructure implementations must be visible to the composition
root for dependency injection. Allowing that visibility only in the Host keeps
the dependency inversion boundary intact while preserving API, Application,
and Domain independence.

---

# Alternatives Considered

## Module API References Infrastructure

Rejected because it reverses the documented API-to-Application dependency
direction and makes presentation assemblies aware of concrete adapters.

## Reflection-Based Module Discovery

Rejected for V1 because it hides runtime dependencies, complicates deployment,
and provides no benefit over explicit registration for the known module set.

## Dedicated Module Bootstrapper Projects

Deferred because the approved solution structure does not define bootstrapper
projects. This option requires a future architecture decision if needed.

---

# Implementation Guidelines

- Module Infrastructure exposes registration extensions such as `AddPlatformInfrastructure`.
- Module API exposes endpoint mapping extensions such as `MapPlatformEndpoints`.
- The Host invokes each approved module registration explicitly during startup.
- Repository and Unit of Work interfaces remain in Application; implementations remain in Infrastructure.
- Domain events are dispatched only after a successful Unit of Work commit.

---

# Compliance Rules

- The Host may reference only approved module API and Infrastructure projects for module composition.
- Module APIs must not reference any Infrastructure project, including their own.
- Module Application and Domain projects must not reference Host or Infrastructure.
- Business modules must not directly reference one another.
- Module Infrastructure projects must not reference another module's Infrastructure project.
- The Host must not contain business logic.
- Architecture tests must validate project-reference rules and circular dependencies.

---

# Consequences

## Positive

- Concrete adapters can be registered without weakening module API boundaries.
- Module extraction remains practical because composition is isolated in the Host.
- Runtime dependencies remain explicit and testable.

## Negative

- The Host has intentional compile-time references to module Infrastructure projects.
- Adding a module requires an explicit Host composition change.

---

# Related Documents

- ADR-001 Modular Monolith
- ADR-003 Clean Architecture
- ADR-009 Domain Events
- ADR-010 Repository Pattern
- ADR-011 Unit of Work
- Solution Structure
- Architecture Principles

---

# Revision History

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0 | 2026-07-30 | Solution Architecture Team | Initial accepted decision |
