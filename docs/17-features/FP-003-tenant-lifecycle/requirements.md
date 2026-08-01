---
document_id: FP-003-REQ
title: Tenant Lifecycle Requirements
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Requirements

## Business requirements

### BR-TEN-0001 — Authoritative tenant lifecycle

Platform must own one authoritative lifecycle record for every tenant represented by `TenantId`.

### BR-TEN-0002 — Authentication eligibility

Tenant selection, new session creation, session refresh, and ordinary tenant business access must use trusted current tenant lifecycle state. Only an `Active` tenant is eligible.

### BR-TEN-0003 — Explicit lifecycle control

Tenant lifecycle changes must occur only through explicit, authorized Platform operations and approved transitions.

### BR-TEN-0004 — Historical preservation

Tenant records and their identity, membership, authentication, audit, and business references must be retained. Physical tenant deletion is prohibited.

### BR-TEN-0005 — Platform authorization plane

Tenant lifecycle administration is a Platform-level capability and is never granted by an ordinary tenant role.

### BR-TEN-0006 — Stable tenant identity

The existing Guid `TenantId` and immutable globally unique tenant code identify the tenant throughout its lifetime.

### BR-TEN-0007 — Tenant isolation continuity

Introducing the Tenant aggregate must preserve the existing shared-schema tenant-isolation rules for tenant-owned data.

### BR-TEN-0008 — Security traceability

Every tenant lifecycle transition must be attributable to trusted actor and UTC metadata and must emit safe audit-ready event data.

## Functional requirements

### FR-TEN-0101 — Create tenant

Create a Tenant with a server-generated Guid `TenantId`, immutable normalized tenant code, required display name, and initial `Provisioning` status.

### FR-TEN-0102 — Get tenant

Return one safe tenant lifecycle view by trusted `TenantId` without exposing tenant business data.

### FR-TEN-0103 — List tenants

Return a bounded, ordered Platform-level tenant lifecycle list using approved filters and pagination.

### FR-TEN-0104 — Activate tenant

Explicitly transition a tenant from `Provisioning` to `Active` using optimistic concurrency.

### FR-TEN-0105 — Suspend tenant

Explicitly transition a tenant from `Active` to `Suspended`, making it immediately ineligible for new ordinary authentication decisions.

### FR-TEN-0106 — Reactivate tenant

Explicitly transition a tenant from `Suspended` to `Active` using optimistic concurrency.

### FR-TEN-0107 — Archive tenant

Explicitly transition a `Provisioning`, `Active`, or `Suspended` tenant to terminal `Archived` state while retaining history.

### FR-TEN-0108 — Get authentication eligibility

Return the requested `TenantId`, existence, nullable current status, derived authentication eligibility, and an exact ineligibility reason. Eligibility is true only when the Tenant exists and status is `Active`.

## Security requirements

### SEC-TEN-0201 — Trusted state

No route, header, claim, query string, request body, or caller-supplied Boolean may override persisted tenant lifecycle state.

### SEC-TEN-0202 — Exact eligibility

Authentication eligibility must be derived from the exact current status using ordinal enum semantics; no name, subscription, billing, role, or support inference is allowed.

### SEC-TEN-0203 — Platform authorization

Lifecycle-changing operations require explicit Platform-level authorization. Tenant roles cannot authorize them.

### SEC-TEN-0204 — Tenant isolation

Reading Platform-level lifecycle state must not grant access to tenant-owned business data or create a generic cross-tenant bypass.

### SEC-TEN-0205 — Sensitive observability

Lifecycle commands, events, logs, telemetry, exceptions, and audit-ready values must contain no credentials, tokens, complete claims collections, billing secrets, or HTTP context.

### SEC-TEN-0206 — No physical deletion

No command, endpoint, repository method, cascade, or routine database operation may physically delete a Tenant.

### SEC-TEN-0207 — Concurrency protection

Stale lifecycle writes must fail without overwriting newer status or metadata.

### SEC-TEN-0208 — Suspended-token authorization

An already issued access token may remain cryptographically valid until expiry but must not refresh and must not override current tenant ineligibility for operations that validate current status. Before ordinary tenant APIs are production-enabled, they require a centralized current-status authorization policy; high-risk operations require a live current-status check.

## Non-functional requirements

### NFR-TEN-0301 — Asynchronous operations

Persistence and external-I/O operations are asynchronous and accept cancellation tokens.

### NFR-TEN-0302 — Clean Architecture

Tenant lifecycle Domain and Application code remain free of EF Core, SQL Server, ASP.NET Core, HTTP, and UI dependencies.

### NFR-TEN-0303 — Module isolation

Platform tenant lifecycle does not depend on HR, GL, or another module's implementation or database.

### NFR-TEN-0304 — SQL Server verification

Migration compatibility, Guid keys, uniqueness, check constraints, restricted deletes, and rowversion behavior are tested against SQL Server.

### NFR-TEN-0305 — Query boundaries

Application contracts expose neither generic repositories nor `IQueryable`. Tenant lifecycle reads return bounded safe projections.

### NFR-TEN-0306 — Quality gates

The full build, Domain/Application tests, SQL Server integration tests, and architecture tests pass with zero introduced warnings or errors.

### NFR-TEN-0307 — Audit-ready events

Lifecycle events are immutable, contain only safe domain values, and use the existing post-commit event-dispatch boundary. Immutable audit persistence remains a separate production-release dependency.
