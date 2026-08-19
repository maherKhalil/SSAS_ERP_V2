---
id: ADR-012
title: Runtime Module Composition
category: Architecture Decision Record
version: 1.2
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

## The module-facing tenant contract set (revision 1.1)

`SSAS.Platform.*` is a module under the rules above, so a business module may not reference it. But every
business module needs Platform's **tenant execution plane**: an Employee, a journal or a stock movement must
save through the tenant unit of work, be authorized against branch scope, and — where it is transferable —
open the sanctioned branch-transfer channel (`ADR-023`, `ADR-024`, `ADR-025`).

`SSAS.BuildingBlocks.Tenancy` is the first concrete set of the *explicitly authorized module-facing
abstractions* this ADR already permits. Platform implements those contracts; business modules consume them;
neither references the other.

```
SSAS.BuildingBlocks.Tenancy          (contracts)
        ▲                    ▲
        │                    │
SSAS.Platform.*         SSAS.HR.* / SSAS.GL.*
   (implements)              (consumes)
```

A contract belongs there only when **a business module must call it** and **Platform must implement it**.
That test is deliberately narrow: every type added widens its blast radius permanently, so contracts only
Platform uses — `IBranchWriteAuthorizer`, `ICompanyWriteAuthorizer`, `ICompanyContextResolver` — stay in
Platform. The set is enumerated by an architecture test rather than left to grow by habit.

**Module entities in the tenant model.** Tenant business data lives in one context and one migration stream
(`ADR-017`), which Platform owns and may not extend with another module's types. A module therefore supplies
its own EF mapping through `ITenantModelContributor` (in `SSAS.BuildingBlocks.Infrastructure`, which already
owns EF Core), and the **Host registers the set explicitly**. This is registration, not discovery: the
prohibition on reflection-based module discovery is unchanged.

Because contributors shape the model, the contributor set participates in the EF model cache key. Without
that, a context built with no contributors and one built with a module's would share whichever model was
created first in the process — a silent, order-dependent defect. Contributors must therefore be
deterministic: the same set must always produce the same model, and a contributor must not vary its mapping
by tenant, request, or ambient state.


## Module permission definitions (revision 1.2)

Functional permissions are **code-owned**: a tenant role may only be granted a permission the catalog
defines, because `AssignPermissionToRoleCommandHandler` refuses any other name and `Role.AssignPermission`
requires a definition only the catalog can produce. That rule is unchanged and is the reason the gap below
was total rather than partial.

Platform owns the catalog and, under the rules above, may not reference a business module. A module
therefore had nowhere to put its own definitions: HR declared five `HR.Employees.*` constants that no
catalog knew, so no role could be granted one and every Employee endpoint refused every caller.

A module supplies its definitions through `IPermissionCatalogContributor` (in
`SSAS.BuildingBlocks.Tenancy`, alongside the other module-facing contracts), and the **Host registers the
set explicitly**. This is registration, not discovery: the prohibition on reflection-based module discovery
is unchanged. Platform composes the registered set with its own definitions into the one
`IPermissionCatalog` the container hands out.

Writing a module's permission name into Platform's catalog is **not** an acceptable alternative. It is the
same coupling with the project reference removed: Platform would own a decision the module owns, and the
next module would repeat the argument.

Three properties make the composition safe:

- **The composer applies Platform's own validation.** A contributed name goes through the same
  `PermissionName` grammar as a Platform-owned one. There is no second, laxer path.
- **Scope is stamped, not accepted.** The contribution contract carries no scope, so a business module
  cannot mint `PermissionScope.PlatformSupport` — cross-tenant operator authority — however it is written.
  Business-module permissions are tenant authority by definition.
- **A duplicate name fails the composition.** Two modules claiming one name, or a module shadowing a
  Platform permission, refuses startup rather than resolving by registration order. Last-write-wins would
  decide which owner's definition applies by the Host's composition order, which is to say by accident.

The catalog is composed at startup and immutable afterwards, so the set a request is authorized against is
the set composition validated.

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
| 1.1 | 2026-08-19 | Solution Architecture Team | Adds the module-facing tenant contract set (`SSAS.BuildingBlocks.Tenancy`) and `ITenantModelContributor`, the first concrete instances of the "explicitly authorized module-facing abstractions" this ADR already permitted. Surfaced by FP-006C3, where HR's Employee became the first module entity needing the tenant execution plane. Module-to-module reference rules and the no-reflection-discovery rule are unchanged. |
| 1.2 | 2026-08-19 | Solution Architecture Team | Adds the module permission-contribution seam (`IPermissionCatalogContributor` in `SSAS.BuildingBlocks.Tenancy`) and the composed `IPermissionCatalog`. Surfaced by the FP-006 release review, where HR's five code-owned Employee permissions were defined nowhere the role-assignment path could see, making every Employee endpoint unreachable in production. Module-to-module reference rules, the Host-as-composition-root rule and the no-reflection-discovery rule are unchanged. |
