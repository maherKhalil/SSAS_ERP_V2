---
id: ADR-008
title: Adopt Entity Framework Core as the Standard ORM
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - entity-framework-core
  - ef-core
  - orm
  - persistence
  - database
depends_on:
  - ADR-002
  - ADR-003
used_by:
  - Platform
  - HR
  - GL
  - Payroll
  - Inventory
  - Purchasing
  - Sales
---

# ADR-008: Adopt Entity Framework Core as the Standard ORM

---

# Status

**Accepted**

---

## ⚠ Domain-event dispatch is specified here and NOT IMPLEMENTED (measured 2026-08-30)

This document describes domain events being dispatched. **There is no dispatcher in the product** — 65 raise sites, zero consumers. **See the implementation-status note at the head of ADR-009 before writing anything that depends on it.**

# Context

SSAS ERP V2 requires a robust, maintainable, and high-performance Object-Relational Mapper (ORM) that integrates seamlessly with the selected technology stack:

- ASP.NET Core
- SQL Server
- Clean Architecture
- CQRS
- Modular Monolith

The persistence strategy must support:

- Complex business domains
- Multi-tenancy
- Transactions
- Migrations
- Optimistic concurrency
- Performance optimization
- Automated testing

---

# Problem Statement

The application requires a consistent persistence technology that:

- Minimizes boilerplate code
- Supports domain-driven development
- Integrates with .NET
- Enables database migrations
- Supports LINQ queries
- Allows optimized SQL when necessary
- Works well with AI-assisted development

---

# Decision

SSAS ERP V2 shall use **Entity Framework Core** as the standard Object-Relational Mapper (ORM).

Entity Framework Core is responsible for:

- Object persistence
- Change tracking
- Migrations
- Transactions
- Relationship management
- Query generation

EF Core shall be the default data access technology throughout the solution.

---

# Decision Drivers

The decision is based on:

- Native .NET support
- Excellent SQL Server integration
- Mature ecosystem
- Strong tooling
- LINQ support
- Code-first migrations
- Testability
- Long-term Microsoft support

---

# DbContext Strategy

Each business module shall own its persistence model.

Examples:

```
PlatformDbContext

HRDbContext

GLDbContext

InventoryDbContext

PayrollDbContext
```

Modules shall not directly access another module's DbContext.

---

# DbContext Lifetime

Each DbContext shall:

- Be registered with Scoped lifetime.
- Represent a single unit of work per request.
- Never be shared across requests.
- Never be injected into controllers directly.
- Be accessed through repositories or application services.

Background services shall create their own scope before resolving a DbContext.

# Entity Configuration

Entity configuration shall be separated from entity classes.

Example:

```
Employee

EmployeeConfiguration

Department

DepartmentConfiguration
```

All mappings shall use the Fluent API.

Data Annotations should be limited to simple validation metadata where appropriate.

---

# Database Migrations

Schema changes shall be managed through EF Core Migrations.

Requirements:

- One migration per logical change
- Migrations committed to source control
- No manual schema updates in production
- Migration names must be descriptive

Example:

```
AddEmployeeIndexes

CreatePayrollTables

AddTenantConfiguration
```

---

# Multi-Tenancy

EF Core shall enforce tenant isolation through centralized mechanisms.

Recommended implementation:

- Global Query Filters
- Repository abstraction
- Tenant-aware DbContext

Every tenant-owned entity shall include `TenantId`.

EF Core shall automatically apply tenant filtering where possible.

---

# Query Strategy

Read operations should:

- Use projections
- Return DTOs
- Disable tracking when updates are not required

Example:

```
AsNoTracking()
```

Write operations shall use tracked entities.

---

# Performance Guidelines

Developers shall:

- Avoid unnecessary eager loading
- Use Include() only when required
- Use pagination for large result sets
- Select only required columns
- Monitor generated SQL
- Optimize indexes before rewriting queries

Performance optimizations must be based on measured evidence.

---

# Transactions

Business transactions shall be coordinated through the application's Unit of Work.

EF Core transactions shall be used where appropriate.

Distributed transactions should be avoided unless explicitly approved.

---

# Concurrency

The application shall support optimistic concurrency.

Entities requiring concurrency control should include a concurrency token.

Typical approaches include:

- RowVersion
- Timestamp

Concurrency conflicts shall be handled gracefully.

---

# Soft Deletes

Entities requiring logical deletion should implement soft delete.

Recommended fields:

```
IsDeleted

DeletedAt

DeletedBy
```

Soft-deleted records should be excluded through centralized query filters.

---

# Audit Fields

Tenant-owned entities should include standard audit fields:

```
CreatedAt

CreatedBy

ModifiedAt

ModifiedBy
```

Additional fields may include:

```
DeletedAt

DeletedBy

RowVersion
```

Audit values shall be populated automatically where possible.

---

# Raw SQL

Raw SQL is permitted only when justified.

Typical scenarios:

- Complex reporting
- Performance-critical queries
- Bulk operations
- Database-specific features

Raw SQL shall:

- Be parameterized
- Be reviewed
- Be documented

Business logic shall not reside in SQL.

---

# Stored Procedures

Stored procedures may be used for:

- High-volume batch processing
- Operational maintenance
- Performance-critical workloads

Routine CRUD operations should use EF Core.

---

# Lazy Loading

Lazy Loading shall not be enabled by default.

Reasons:

- Hidden database calls
- Performance unpredictability
- Difficult debugging

Explicit loading is preferred.

---

# Testing

EF Core repositories shall support:

- Unit testing through abstractions
- Integration testing against SQL Server
- Migration validation
- Query testing

Tests should not rely solely on the InMemory provider for behavior verification.

---

# Alternatives Considered

## Entity Framework Core (Selected)

Advantages

- Excellent .NET integration
- Strong tooling
- LINQ support
- Migrations
- High productivity
- Large community

Disadvantages

- ORM overhead in some scenarios
- Requires understanding of generated SQL

---

## Dapper

Advantages

- Very fast
- Lightweight
- Full SQL control

Disadvantages

- Manual mapping
- No change tracking
- No migrations
- More boilerplate

Rejected as the primary ORM.

---

## ADO.NET

Advantages

- Maximum control
- Minimal abstraction

Disadvantages

- High development effort
- Increased maintenance
- Significant boilerplate

Rejected.

---

# Consequences

## Positive

- Rapid development
- Consistent persistence layer
- Strong integration with ASP.NET Core
- Simplified migrations
- AI-friendly development
- Easier maintenance

## Negative

- Developers must understand EF Core performance characteristics
- Generated SQL should be reviewed for critical operations
- ORM abstractions may not suit every scenario

---

# Implementation Guidelines

Developers and AI assistants shall:

- Use Fluent API configurations.
- Keep entities persistence-ignorant where possible.
- Use AsNoTracking() for read-only queries.
- Apply global query filters for tenant isolation.
- Use migrations for schema evolution.
- Avoid Lazy Loading.
- Prefer projections over loading entire entities.
- Review generated SQL for complex queries.

---

# Compliance Rules

Every module shall:

- Use EF Core as the primary ORM.
- Define its own DbContext.
- Use Fluent API configurations.
- Manage schema changes through migrations.
- Implement audit fields consistently.
- Apply tenant isolation.
- Follow repository abstractions.

Architecture reviews shall verify compliance.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Inefficient queries | Monitor generated SQL and optimize indexes |
| N+1 query issues | Explicit loading, projections, query reviews |
| Migration conflicts | Controlled migration process and code reviews |
| Tenant data leakage | Global query filters and automated tests |

---

# Depends On

- ADR-002 – SQL Server
- ADR-003 – Clean Architecture

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-001 | EF Core operates within the Modular Monolith architecture |
| ADR-002 | EF Core targets SQL Server |
| ADR-003 | EF Core resides in the Infrastructure layer |
| ADR-004 | CQRS handlers use EF Core through repositories |
| ADR-005 | Global query filters enforce tenant isolation |
| ADR-006 | Persistence operates within the authenticated user and tenant context |
| ADR-009 | Domain Events are dispatched after successful persistence |
| ADR-010 | Repository and Unit of Work abstractions encapsulate EF Core |

---

# Related Documents

- Solution Architecture
- Database Standards (future)
- Development Standards
- Coding Standards
- Sprint-00 Foundation

---

# Review Criteria

This ADR shall be reviewed if:

- The project adopts a different ORM.
- Database provider requirements change significantly.
- Performance analysis justifies a hybrid persistence strategy.
- Future architectural decisions require a different persistence model.

Until then, Entity Framework Core remains the mandatory ORM for SSAS ERP V2.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | YYYY-MM-DD | Solution Architecture Team | Initial version |