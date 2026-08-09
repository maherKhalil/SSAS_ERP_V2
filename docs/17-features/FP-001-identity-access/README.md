---
document_id: FP-001
title: Platform Identity and Access Management
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
approved_date: 2026-07-31
---

# Feature Package 001 — Platform Identity and Access Management

> **Implementation status (informational).** The FP-001 backend core — Domain, Application (user, role, and permission commands and queries), and persistence — is implemented and merged. The administrative HTTP transport for these operations is not yet implemented; the Platform API currently exposes only the authentication and localization endpoints. This note records implementation state only and changes no FP-001 requirement, decision, or contract.

## Purpose

This package defines the approved business and functional scope for tenant-aware identity and access management in SSAS ERP V2.

## Approved model

- Each tenant owns its own users, tenant administrators, roles, and assignments.
- Tenant users and tenant administrators never access another tenant.
- A platform-level **App Owner / App Support** actor may manage multiple tenants and may create or manage users across tenants according to explicit platform-support permissions.
- The same person may belong to multiple tenants through separate tenant memberships.
- A token is scoped to exactly one tenant.
- After authentication, tenant selection is shown only when more than one tenant membership is available; when exactly one is available, that tenant is selected automatically.
- Email is unique per tenant.
- Protected system roles and tenant-defined custom roles are supported.
- Permissions are assigned to roles, not directly to users.
- A user may hold multiple roles.
- Permissions are application-defined and code-owned for V1.
- Users are never physically deleted; they are deactivated.
- A role cannot be retired while it is assigned to active users. Those users must first be deactivated or reassigned as approved. Retired roles cannot receive new assignments, and audit history is preserved.
- Authentication and token lifecycle are specified in a separate feature package.

## Documents

1. `requirements.md`
2. `business-rules.md`
3. `domain-model.md`
4. `authorization-model.md`
5. `api-contracts.md`
6. `data-model.md`
7. `acceptance-criteria.md`
8. `test-scenarios.md`
9. `decisions-approved.md`
10. `traceability-matrix.md`

## Architecture constraints

- Multi-tenant modular monolith.
- Clean Architecture, DDD, and CQRS.
- Platform owns identity and access management.
- Repositories are aggregate-specific and module-owned.
- No generic repository.
- Module-owned `DbContext`.
- JWT authorization uses validated subject, tenant, role, and permission claims.
- Trusted tenant context cannot be overridden by request input.
- Roles and permissions remain distinct.
