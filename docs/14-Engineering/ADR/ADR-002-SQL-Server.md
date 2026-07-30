---
id: ADR-002
title: Adopt Microsoft SQL Server as the Primary Database Platform
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - database
  - sql-server
  - ef-core
  - persistence
depends_on:
  - ADR-001
used_by:
  - Sprint-00
  - Platform
  - HR
  - GL
  - Inventory
  - Payroll
  - Sales
---

# ADR-002: Adopt Microsoft SQL Server as the Primary Database Platform

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 requires an enterprise-grade relational database capable of supporting:

- Multi-tenant SaaS architecture
- High transaction volumes
- ACID-compliant financial transactions
- Complex reporting
- Large datasets
- Advanced indexing
- Strong security
- High availability
- Disaster recovery
- Long-term maintainability

The database platform must integrate seamlessly with the selected technology stack:

- ASP.NET Core
- Entity Framework Core
- Clean Architecture
- Modular Monolith

The development team also has significant operational experience with SQL Server, reducing implementation and maintenance risk.

---

# Problem Statement

The project requires a single primary relational database platform that provides:

- Reliability
- Performance
- Security
- Scalability
- Enterprise support
- Mature tooling
- Cloud deployment options

Several database platforms were evaluated.

---

# Decision

SSAS ERP V2 shall use **Microsoft SQL Server** as its primary relational database.

Entity Framework Core shall be the default Object-Relational Mapper (ORM).

All production data shall be stored in SQL Server unless a documented architectural decision introduces additional data stores.

---

# Decision Drivers

The decision is based on the following criteria:

- Enterprise maturity
- Existing team expertise
- Excellent .NET ecosystem integration
- Advanced query optimizer
- Strong transaction management
- High availability options
- Mature backup and recovery capabilities
- Excellent tooling
- Long-term Microsoft support

---

# Alternatives Considered

## Option 1 – Microsoft SQL Server (Selected)

### Advantages

- Excellent integration with .NET and EF Core.
- Mature transaction engine.
- Strong indexing capabilities.
- Advanced execution plan optimization.
- Built-in security features.
- Proven scalability.
- SQL Server Management Studio (SSMS).
- Rich monitoring and diagnostics.
- Supports Always On Availability Groups.
- Strong ecosystem and documentation.

### Disadvantages

- Commercial licensing for some deployments.
- Windows-centric tooling (although Linux support exists).

---

## Option 2 – PostgreSQL

### Advantages

- Open source.
- Excellent standards compliance.
- Strong performance.
- Cross-platform.

### Disadvantages

- Team has less operational experience.
- Existing tooling and organizational standards favor SQL Server.
- Migration effort for existing SQL Server expertise.

---

## Option 3 – MySQL

### Advantages

- Open source.
- Large community.
- Easy hosting.

### Disadvantages

- Less suitable for complex ERP workloads.
- Weaker ecosystem alignment with existing team experience.
- Fewer enterprise features compared to SQL Server.

---

## Option 4 – Oracle Database

### Advantages

- Enterprise-grade scalability.
- Advanced enterprise capabilities.

### Disadvantages

- High licensing costs.
- Increased operational complexity.
- Not aligned with project budget or technology strategy.

---

# Decision Rationale

SQL Server was selected because it provides the best balance between:

- Performance
- Stability
- Maintainability
- Development productivity
- Operational familiarity
- Long-term support

Its integration with Entity Framework Core reduces development complexity while preserving the option to use stored procedures for performance-critical operations.

---

# Consequences

## Positive

- Excellent integration with ASP.NET Core.
- Mature development tools.
- Strong backup and recovery.
- High-performance query optimizer.
- Reliable transaction support.
- Rich monitoring ecosystem.
- Simplified AI-assisted code generation.
- Strong support for large ERP workloads.

---

## Negative

- Licensing costs for some deployment models.
- Future migration to another database would require testing and validation.
- SQL Server-specific optimizations may reduce portability.

---

# Database Design Principles

The following principles shall apply:

- Normalize transactional data where appropriate.
- Use surrogate primary keys unless documented otherwise.
- Apply foreign key constraints.
- Create indexes based on measured performance requirements.
- Avoid premature optimization.
- Use database migrations through Entity Framework Core.
- Preserve referential integrity.

---

# Performance Strategy

The application shall prioritize:

- Proper indexing
- Query optimization
- Efficient pagination
- Bulk operations where appropriate
- Optimistic concurrency
- Execution plan monitoring

Performance improvements shall be driven by measurement rather than assumptions.

---

# Security Considerations

The database shall support:

- Encrypted connections (TLS)
- Least-privilege access
- Parameterized queries
- SQL injection prevention
- Auditing where required
- Secure credential management
- Backup encryption where applicable

Sensitive information shall never be stored or transmitted in plain text.

---

# Multi-Tenancy Considerations

Tenant isolation is enforced at the application layer and reflected in the database schema.

Requirements include:

- Tenant-owned tables include `TenantId`.
- Queries automatically filter by tenant.
- Cross-tenant access is prohibited unless explicitly authorized.
- Administrative operations spanning tenants must be documented and secured.

---

# High Availability

Production deployments should support:

- Automated backups
- Point-in-time recovery
- Disaster recovery procedures
- High availability options such as Always On Availability Groups where appropriate

The exact topology depends on the deployment environment.

---

# Future Considerations

Future enhancements may include:

- Read replicas for reporting
- Partitioning of very large tables
- Data archival strategies
- Multi-region deployments
- Support for additional database providers through EF Core abstractions if business requirements justify the investment

Such changes require a new ADR.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Vendor lock-in | Maintain EF Core abstractions and avoid unnecessary SQL Server-specific features |
| Performance degradation | Continuous monitoring, indexing, and query tuning |
| Database growth | Archiving, partitioning, and capacity planning |
| Infrastructure failure | High availability and tested backup/recovery procedures |

---

# Implementation Guidelines

Codex and developers shall:

- Use Entity Framework Core by default.
- Follow the approved database naming conventions.
- Use migrations for schema changes.
- Avoid direct SQL unless justified.
- Parameterize all database access.
- Keep business logic out of the database except where documented for performance or operational reasons.

---

# Compliance

This ADR applies to all modules:

- Platform
- HR
- General Ledger
- Payroll
- Inventory
- Purchasing
- Sales
- Reporting

No module may introduce a different primary relational database without an approved ADR.

---

# Related Documents

- ADR-001 – Modular Monolith
- Solution Architecture
- Clean Architecture
- Development Standards
- Sprint-00 Foundation
- Database Standards (future)

---

# Review Criteria

This decision should be reviewed if:

- The organization adopts a different strategic database platform.
- Cloud-native requirements significantly change.
- Measured performance demonstrates that SQL Server no longer meets project objectives.
- Business requirements require multi-database support.

Until then, SQL Server remains the approved primary database platform.

# Depends On

- ADR-001 – Modular Monolith

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-003 | Infrastructure implementation follows Clean Architecture |
| ADR-004 | CQRS handlers persist data in SQL Server |
| ADR-005 | Tenant isolation is implemented in SQL Server schema |
| ADR-008 | Entity Framework Core targets SQL Server |
| ADR-010 | Repository implementation uses SQL Server |