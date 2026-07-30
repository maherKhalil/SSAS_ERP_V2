---
id: ADR-005
title: Adopt Shared Database Multi-Tenancy with Logical Tenant Isolation
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - multi-tenancy
  - saas
  - tenant
  - security
  - architecture
depends_on:
  - ADR-001
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

# ADR-005: Adopt Shared Database Multi-Tenancy with Logical Tenant Isolation

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 is designed as a Software-as-a-Service (SaaS) ERP platform.

A single application instance will serve multiple independent customers (tenants).

Each tenant must have complete logical isolation of:

- Companies
- Users
- Employees
- Financial Data
- Payroll
- Inventory
- Documents
- Reports
- Configuration
- Business Transactions

The system must maximize resource efficiency while guaranteeing tenant data isolation and security.

---

# Problem Statement

The application requires a multi-tenant architecture that provides:

- Strong tenant isolation
- Cost-effective infrastructure
- Simplified deployment
- Operational efficiency
- Horizontal scalability
- Support for thousands of tenants

The chosen strategy must balance scalability, maintainability, and implementation complexity.

---

# Decision

SSAS ERP V2 shall adopt a **Shared Database, Shared Schema** multi-tenancy model with **logical tenant isolation**.

Each tenant's data will reside in the same database but will be isolated through a mandatory `TenantId` field and application-level enforcement.

No tenant may access another tenant's data unless explicitly authorized through platform administration features.

---

# Multi-Tenancy Model

The selected architecture is:

```
Application

↓

SQL Server Database

↓

Shared Tables

↓

TenantId
```

Every tenant-owned record is associated with a unique TenantId.

---

# Tenant Hierarchy

The platform supports the following hierarchy:

```
Platform

└── Tenant

      └── Company

            └── Business Data
```

Definitions:

- **Platform** – The SaaS provider.
- **Tenant** – A subscribing customer.
- **Company** – A legal entity owned by a tenant.
- **Business Data** – Operational records.

A tenant may own one or more companies.

---

# Tenant Identifier

Each tenant shall have a globally unique identifier.

```
TenantId
```

Characteristics:

- Immutable
- Unique
- Required
- Assigned at tenant creation
- Never reused

TenantId shall be included in every tenant-owned entity.

---

# Mandatory Tenant Ownership

The following entities must include `TenantId`:

- Company
- User
- Employee
- Department
- Position
- Payroll
- Journal Entry
- Customer
- Vendor
- Warehouse
- Product
- Purchase Order
- Sales Order
- Invoice
- Payment
- Audit Log
- Attachment Metadata

Platform-level entities may omit `TenantId` where appropriate.

---

# Tenant Resolution

The current tenant shall be resolved before processing any business request.

Supported resolution mechanisms include:

- Authenticated JWT claims (primary)
- API Gateway headers (future)
- Subdomain routing (future)
- Custom domain mapping (future)

The authenticated tenant becomes part of the execution context.

---

# Query Isolation

All queries against tenant-owned data must automatically filter by `TenantId`.

Developers and AI-generated code shall not manually implement tenant filtering in every query.

The platform shall provide centralized tenant filtering.

Examples include:

- Global query filters
- Repository abstraction
- Middleware
- Application services

The implementation mechanism may evolve without changing this policy.

---

# Write Isolation

All newly created tenant-owned records shall automatically receive the current `TenantId`.

Applications shall never allow client applications to specify arbitrary tenant identifiers.

Tenant ownership is assigned by the server.

---

# Cross-Tenant Access

Cross-tenant access is prohibited by default.

Only approved platform administration features may access multiple tenants.

Such operations must:

- Be explicitly documented.
- Require elevated authorization.
- Produce audit records.
- Be reviewed during implementation.

---

# Platform Administration

Platform administrators may perform:

- Tenant provisioning
- Tenant suspension
- Tenant activation
- Subscription management
- License management
- Usage reporting
- Platform monitoring

Platform administration is outside normal tenant boundaries.

---

# Security Considerations

Tenant isolation is a security boundary.

The system shall:

- Prevent data leakage.
- Prevent cross-tenant queries.
- Validate tenant ownership.
- Audit privileged operations.
- Reject unauthorized access attempts.

Security reviews shall verify tenant isolation throughout the application.

---

# Database Design

Every tenant-owned table shall:

- Include `TenantId`.
- Index `TenantId` appropriately.
- Support efficient filtering.
- Preserve referential integrity.

Composite indexes should include `TenantId` where it improves performance.

---

# Caching

Cached data shall be tenant-aware.

Cache keys should include the tenant identifier.

Example:

```
Tenant:{TenantId}:Employees
```

This prevents cache contamination between tenants.

---

# Background Processing

Background jobs shall execute within a tenant context.

Jobs processing multiple tenants must isolate each tenant independently.

Failures in one tenant must not affect others.

---

# Reporting

Reports generated for tenants shall only include data belonging to the requesting tenant.

Platform-wide reports require elevated administrative privileges.

---

# Backup and Recovery

Backups protect the shared database.

Tenant recovery procedures shall be documented if tenant-level restoration becomes a business requirement.

---

# Scalability

The selected model supports:

- Thousands of tenants
- Horizontal application scaling
- Efficient infrastructure utilization
- Simplified deployment
- Lower operational costs

If future growth requires physical tenant isolation, a new ADR shall define the migration strategy.

---

# Alternatives Considered

## Shared Database, Shared Schema (Selected)

Advantages

- Lowest infrastructure cost.
- Simplest deployment.
- Efficient resource utilization.
- Easier maintenance.
- Excellent scalability for small and medium tenants.

Disadvantages

- Strong application-level isolation is mandatory.
- Extra care required during development.

---

## Shared Database, Separate Schemas

Advantages

- Better logical separation.
- Easier tenant-specific maintenance.

Disadvantages

- Increased migration complexity.
- More operational overhead.
- Schema management becomes difficult at scale.

Rejected.

---

## Separate Database per Tenant

Advantages

- Maximum isolation.
- Easier tenant migration.
- Independent backup strategies.

Disadvantages

- Higher infrastructure costs.
- More complex deployments.
- Increased operational burden.
- Difficult to manage thousands of tenants.

Deferred for future consideration if enterprise requirements demand it.

---

# Consequences

Positive

- Efficient infrastructure utilization.
- Lower hosting costs.
- Simplified deployment.
- Strong scalability.
- Excellent support for SaaS.

Negative

- Tenant isolation must be rigorously enforced.
- Query filtering becomes mandatory.
- Architecture reviews must verify compliance.

---

# Implementation Guidelines

Developers and AI assistants shall:

- Never bypass tenant isolation.
- Never expose another tenant's data.
- Never accept TenantId from client requests.
- Always execute within the resolved tenant context.
- Ensure all repositories respect tenant boundaries.
- Keep tenant resolution centralized.

---

# Compliance Rules

Every tenant-owned feature must satisfy:

- TenantId present.
- Automatic tenant filtering.
- Automatic tenant assignment.
- Cross-tenant access prohibited unless documented.
- Audit logging for administrative operations.

Architecture reviews shall verify compliance.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Data leakage | Centralized tenant filtering and security testing |
| Missing TenantId | Domain model standards and code reviews |
| Cross-tenant queries | Automated integration tests and repository validation |
| Performance degradation | Proper indexing, query optimization, and monitoring |

---

# Related Documents

- ADR-001 – Modular Monolith
- ADR-002 – SQL Server
- ADR-003 – Clean Architecture
- ADR-004 – CQRS
- Solution Architecture
- Security Standards
- Sprint-00 Foundation

---

# Review Criteria

This ADR shall be reviewed if:

- The platform adopts physical tenant isolation.
- Separate databases per tenant become a business requirement.
- Regulatory requirements mandate different isolation strategies.
- Scalability or operational considerations require a different tenancy model.

Until such changes are formally approved, **Shared Database with Logical Tenant Isolation** remains the mandatory multi-tenancy 

# Depends On

- ADR-001
- ADR-002
- ADR-003

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-004 | CQRS handlers automatically apply tenant isolation |
| ADR-006 | TenantId is resolved from authenticated JWT claims |
| ADR-008 | EF Core applies global tenant query filters |
| ADR-009 | Domain Events execute within tenant boundaries |
| ADR-010 | Repository implementations enforce tenant isolation |architecture for SSAS ERP V2.