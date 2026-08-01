---
document_id: FP-003-DEC
title: Approved Tenant Lifecycle Decisions
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
approved_date: 2026-08-01
---

# Approved Decisions

The decisions below are binding for FP-003 implementation. They preserve the existing `DEC-TEN-*` identifiers from the review package.

## DEC-TEN-0001 — Authoritative identifier

Use the existing server-generated, nonempty Guid `TenantId` as the immutable `platform.Tenants` primary key. Do not add a BIGINT Tenant key, maintain a second Tenant identifier, reuse a TenantId, or accept it from a client during creation.

## DEC-TEN-0002 — Status vocabulary

Use exactly `Provisioning`, `Active`, `Suspended`, and `Archived`. Only an existing `Active` Tenant is authentication-eligible; a missing Tenant is ineligible.

## DEC-TEN-0003 — Transition graph

Permit only Create to Provisioning, Provisioning to Active, Provisioning to Archived, Active to Suspended, Active to Archived, Suspended to Active, and Suspended to Archived. Archived is terminal. No transition is triggered automatically by time, inactivity, billing, payment, or subscription state.

## DEC-TEN-0004 — Tenant code

TenantCode is required, immutable, limited to 64 characters, trimmed, and stored with its display casing preserved. Its normalized value is exactly `Trim().ToUpperInvariant()`, with no culture-specific or provider-specific transformation. NormalizedTenantCode is globally unique and its column and unique index use the approved BIN2 collation.

## DEC-TEN-0005 — Tenant name

TenantName is required, limited to 200 characters, trimmed, and stored with its display casing preserved. It is not globally unique and has no NormalizedTenantName solely for uniqueness. TenantName is mutable only through an approved Tenant update operation; such an operation is not part of the first implementation milestone.

## DEC-TEN-0006 — Legal name

LegalName is deferred to a later legal or customer-profile feature and is not part of FP-003.

## DEC-TEN-0007 — Physical deletion

Physical Tenant deletion is prohibited. Provide no delete command, repository method, permission, endpoint, cascade, or routine database operation. Archive is the terminal retained lifecycle operation and supersedes the former Draft `DELETE /api/platform/tenants/{id}` contract.

## DEC-TEN-0008 — Platform aggregate and query filtering

Tenant is a Platform-level aggregate in the existing Platform persistence boundary. It does not implement `ITenantOwnedEntity`, receives no tenant query filter, and receives no automatic TenantId assignment from `ICurrentTenant`. Reading Tenant does not disable isolation filters on tenant-owned data.

## DEC-TEN-0009 — Lifecycle reason metadata

Persist `StatusChangedUtc`, `StatusChangedBy`, and `StatusChangeReasonCode`. The bounded reason codes are exactly `Created`, `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, and `IssueResolved`. Creation records `Created`; every transition records a code, and Suspend and Archive require an explicit non-`Created` code. Safe events contain the code only and no free-form reason text, secrets, or billing detail.

## DEC-TEN-0010 — Already issued access tokens

A Tenant status change does not cryptographically invalidate an already issued short-lived JWT, which may remain valid until expiry. It cannot be refreshed when current Tenant status is ineligible. Tenant selection, session creation, refresh, and operations that validate current status use current eligibility. Before ordinary tenant APIs are production-enabled, a centralized current-status authorization policy must deny non-Active status. High-risk operations use a live current-status check. Middleware, JWT, cache, and invalidation implementation are outside the first milestone.

## DEC-TEN-0011 — Eligibility result shape

The result contains exactly `TenantId`, `Exists`, nullable `TenantStatus`, `IsAuthenticationEligible`, and `TenantAuthenticationIneligibilityReason`. Reason values are exactly `None`, `TenantNotFound`, `Provisioning`, `Suspended`, and `Archived`. A missing Tenant returns false Exists, null status, false eligibility, and TenantNotFound; Active returns true Exists, Active, true eligibility, and None. TenantName is omitted.

## DEC-TEN-0012 — Platform authorization permissions

Create, read, list, activate, suspend, reactivate, and archive are Platform-level operations. Ordinary tenant roles never authorize them, and Platform lifecycle authority grants no tenant business-data access. Exact permission identifiers and Platform-support authentication remain deferred; no Tenant HTTP endpoint is included in the first milestone. The eligibility query is an internal trusted Platform authentication or authorization contract, not an end-user permission decision.

## DEC-TEN-0013 — Legacy TenantId reconciliation

Reconciliation is staged and environment-specific. First inventory distinct legacy TenantIds and produce an operator-reviewed mapping of each TenantId to code, name, and status. Fail on missing or duplicate mappings. Never infer lifecycle state, create placeholder metadata, or silently mark a legacy Tenant Active. Commit no production data or environment-specific mapping to the repository.

## DEC-TEN-0014 — Tenant foreign keys

The first milestone creates the Tenant aggregate, `platform.Tenants`, and its own constraints but does not auto-backfill legacy TenantIds or retrofit all existing foreign keys. After approved reconciliation, a dedicated enforcement migration verifies complete coverage, fails on orphans, and adds restricted foreign keys while preserving composite same-tenant constraints. Existing candidates include TenantUsers, Roles, tenant-user role assignments, role-permission assignments where TenantId is present, and invitation-bound AccountActionTokens. Every new table introduced after FP-003—including session, tenant-authentication, and future module-root tables—has its Tenant foreign key from its first migration.

## DEC-TEN-0015 — Provisioning and first administrator

Creating a Tenant creates only the Tenant in Provisioning. Activation creates no company or first administrator. A later onboarding coordinator may compose those deferred capabilities explicitly. Active status alone does not authenticate a caller without every independent identity, membership, session, and authorization prerequisite.

## DEC-TEN-0016 — Subscription independence

Subscription, billing, and payment state are separate concepts and never implicitly change Tenant status. Any future coupling uses an explicit authorized lifecycle command.

## DEC-TEN-0017 — Immutable audit dependency

FP-003 emits safe audit-ready events through the existing post-commit dispatcher. Immutable audit storage is a separate production-release dependency and is not implemented in the first milestone.

## Reconciled conflict register

| # | Prior conflict | Approved resolution |
|---|---|---|
| 1 | Draft Tenant Management mixed lifecycle with company, subscription, branding, localization, and notifications | FP-003 is authoritative for lifecycle and authentication eligibility; the other capabilities remain deferred and non-authoritative |
| 2 | Draft physical-delete route and permission | Superseded by terminal Archive under DEC-TEN-0007 |
| 3 | Draft TenantName uniqueness | Superseded by non-unique TenantName under DEC-TEN-0005 |
| 4 | General BIGINT guidance versus established Guid TenantId | Existing Guid TenantId remains authoritative under DEC-TEN-0001 |
| 5 | Existing tenant-owned Platform tables lack a Tenant principal foreign key | Use the staged enforcement process in DEC-TEN-0014 |
| 6 | Legacy TenantIds lack trustworthy lifecycle metadata | Use operator-reviewed, environment-specific reconciliation under DEC-TEN-0013 |
| 7 | JWTs may remain cryptographically valid after suspension | Current-state authorization requirements are fixed by DEC-TEN-0010 |
| 8 | Platform-support permissions are not defined | Keep the first milestone internal and defer endpoints and permission identifiers under DEC-TEN-0012 |
| 9 | Broad provisioning expected a company and first administrator | Tenant creation remains Provisioning-only under DEC-TEN-0015 |
| 10 | Draft associated subscription with lifecycle | Lifecycle is independent under DEC-TEN-0016 |
| 11 | Immutable audit storage does not yet exist | Emit safe events and retain the production dependency under DEC-TEN-0017 |
| 12 | FP-001 and FP-002 required Tenant status without an implementation source | Approved FP-003 is the source consumed by those workflows |
| 13 | Draft documentation lacked an exact status vocabulary and graph | DEC-TEN-0002 and DEC-TEN-0003 define both exactly |
