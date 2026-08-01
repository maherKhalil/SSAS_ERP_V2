---
document_id: FP-003-AC
title: Tenant Lifecycle Acceptance Criteria
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Acceptance Criteria

### AC-TEN-0001 — Provisioning creation

Creating a valid Tenant generates a nonempty Guid TenantId, stores the normalized code and trimmed display name, and begins in `Provisioning`.

### AC-TEN-0002 — Tenant code uniqueness

Two codes with the same `Trim().ToUpperInvariant()` value cannot be created, and the displayed trimmed casing of the accepted code is preserved.

### AC-TEN-0003 — Tenant name is not unique

Two different TenantIds may use the same trimmed TenantName.

### AC-TEN-0004 — Safe reads

Get and bounded list queries return safe lifecycle projections and no tenant business data.

### AC-TEN-0005 — Activation

A current `Provisioning` Tenant can be activated once and becomes authentication-eligible after successful commit.

### AC-TEN-0006 — Invalid transitions

Every transition not listed in the approved lifecycle matrix is rejected without changing state or publishing a committed transition event.

### AC-TEN-0007 — Suspension

Suspending an `Active` Tenant makes current authentication eligibility false and blocks subsequent tenant selection, new-session, and refresh eligibility decisions.

### AC-TEN-0008 — Exact eligibility

Eligibility is true only for `Active`. A missing Tenant returns `Exists = false`, null status, false eligibility, and `TenantNotFound`; existing statuses return the matching exact reason or `None` for Active.

### AC-TEN-0009 — Reactivation

A `Suspended` Tenant can be reactivated; no other status can use the reactivation operation.

### AC-TEN-0010 — Archive is terminal

Provisioning, Active, and Suspended Tenants may be archived, after which no transition or authentication eligibility is possible.

### AC-TEN-0011 — No physical deletion

No Domain operation, command, repository method, API contract, or migration cascade physically deletes a Tenant.

### AC-TEN-0012 — Platform authorization boundary

An ordinary tenant role cannot administer Tenant lifecycle. Platform lifecycle authorization does not grant tenant business-data access.

### AC-TEN-0013 — No status override

Caller-supplied status or eligibility values cannot create, activate, suspend, reactivate, archive, or authenticate a Tenant outside the persisted lifecycle rules.

### AC-TEN-0014 — Concurrency

A stale rowversion is rejected and cannot overwrite a newer Tenant status or lifecycle metadata.

### AC-TEN-0015 — Safe events

Every successful lifecycle change raises the corresponding safe event after persistence; no event contains credentials, tokens, complete claims, billing details, or HTTP context.

### AC-TEN-0016 — Narrow eligibility contract

The authentication-eligibility contract accepts one TenantId and returns exactly TenantId, Exists, nullable TenantStatus, IsAuthenticationEligible, and TenantAuthenticationIneligibilityReason. It exposes no name, `IQueryable`, aggregate, generic repository, subscription decision, or authorization grant.

### AC-TEN-0017 — Migration reconciliation

`AddTenantLifecycle` creates the Tenant table without legacy auto-backfill or blanket foreign-key retrofit. Reconciliation uses an operator-reviewed environment-specific mapping and fails on missing or duplicate entries. A later dedicated enforcement migration verifies coverage, fails on orphans, and adds restricted foreign keys; no process invents placeholder metadata or Active status.

### AC-TEN-0018 — Persistence ownership and isolation

Tenant lifecycle uses the existing Platform context, schema, connection, migration history, and Unit of Work; Tenant itself has no tenant query filter, while existing tenant-owned entities retain their isolation filters.

### AC-TEN-0019 — Already issued access token

An already issued token does not override current non-Active status. Before ordinary tenant HTTP APIs are production-enabled, the approved centralized current-status enforcement described by `DEC-TEN-0010` is present and tested.

### AC-TEN-0020 — Focused milestone scope

The first implementation milestone introduces no subscription, company, branding, configuration, notification, authentication-session, refresh-token, JWT-issuance, tenant endpoint, Angular, or immutable-audit-store implementation.
