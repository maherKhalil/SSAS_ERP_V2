---
document_id: FP-003-LIFECYCLE
title: Tenant Lifecycle Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Lifecycle Model

## Status semantics

### Provisioning

The Tenant record exists but ordinary tenant authentication is not permitted. Provisioning does not imply that companies, administrators, subscriptions, configuration, or other deferred resources exist.

### Active

The Tenant is eligible to participate in ordinary tenant authentication and business authorization, subject to all independent identity, membership, session, client, and permission checks.

Active status grants no role, permission, membership, subscription entitlement, or cross-tenant access.

### Suspended

The Tenant is temporarily ineligible for ordinary tenant authentication and business access. Its records and all historical references remain intact. It may be explicitly reactivated.

### Archived

The Tenant is permanently inactive, never authentication-eligible, retained for history, and unable to transition again.

## Transition matrix

| Current | Operation | Next | Authentication eligible after commit |
|---|---|---|---|
| None | Create | Provisioning | No |
| Provisioning | Activate | Active | Yes |
| Provisioning | Archive | Archived | No |
| Active | Suspend | Suspended | No |
| Active | Archive | Archived | No |
| Suspended | Reactivate | Active | Yes |
| Suspended | Archive | Archived | No |
| Archived | Any transition | Rejected | No |

All unlisted transitions are rejected, including repeated activation, repeated suspension, reactivation from Provisioning, and any unarchive operation.

## Lifecycle reason codes

Creation records `Created` for the initial Provisioning state. Every later transition requires one of `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, or `IssueResolved`; `Created` is invalid after creation. Suspend and Archive callers must explicitly provide a non-`Created` code. Actor and UTC metadata come from trusted application context, and events contain only the bounded reason code—never free-form reason text.

## Creation

`CreateTenant`:

1. receives tenant code and tenant name;
2. validates and normalizes them;
3. verifies normalized code uniqueness;
4. generates a nonempty Guid `TenantId` server-side;
5. creates the aggregate in `Provisioning`;
6. records trusted UTC and actor metadata;
7. records reason code `Created` and raises `TenantCreated`;
8. persists through the existing Platform Unit of Work.

Creation does not provision a company, administrator, subscription, branding, configuration, or notification.

## Activation

Activation is explicit and is accepted only from `Provisioning`. The status change becomes authoritative only after successful persistence.

This package does not require a first administrator or default company as an activation precondition. A future onboarding coordinator may impose additional preconditions without changing the Tenant transition graph. A tenant with no active membership remains unable to authenticate even when its status is Active.

## Suspension

After a suspension commit:

- eligibility queries return `Suspended` and false;
- the tenant is excluded from tenant selection;
- a new authentication session cannot be created;
- an existing authentication session cannot refresh;
- current-status-aware business authorization denies ordinary access;
- tenant records and references remain unchanged.

Suspension does not delete memberships, sessions, tokens, roles, permissions, or business data. FP-002 owns any session revocation or compromise behavior; FP-003 supplies the current eligibility fact.

## Reactivation

Reactivation is accepted only from `Suspended`. It does not restore expired, revoked, or compromised sessions and does not reactivate memberships or accounts. New authentication still requires every FP-001 and FP-002 eligibility rule.

## Archive

Archive is accepted from `Provisioning`, `Active`, or `Suspended`. It is terminal. It replaces the physical-delete operation in the Draft Tenant Management document.

Archive does not erase or anonymize history. Any future privacy or legal-erasure workflow requires separate requirements that preserve mandatory ERP and security references.

## Already issued access tokens

Tenant status is authoritative immediately after commit, but an already issued short-lived JWT is not cryptographically destroyed by a database status change.

The authorization rule is:

- token signature and lifetime validation remains necessary but is insufficient by itself;
- tenant selection, session creation, and refresh always read current eligibility;
- ordinary tenant business authorization must reject current non-Active status through a centralized Platform authorization integration;
- sensitive operations must also use current status and must not rely only on token age;
- high-risk operations perform a live current-status check;
- performance mechanisms such as event-driven cache invalidation may be added later only with an approved stale-state bound and invalidation design.

FP-002 Milestone 4 implements and tests this centralized policy under `DEC-AUTH-0057`. Every ordinary tenant-scoped authenticated business request performs one scoped live eligibility lookup; role and permission authorization permits only current Active status. TenantStatus is not copied into the JWT. Logout uses a separate authenticated policy so a current session can still be revoked after suspension. These Host/API and JWT changes remain outside the first FP-003 implementation milestone and are owned by FP-002.

## Concurrency and event timing

- Every command supplies an expected rowversion.
- A stale rowversion returns a conflict and raises no committed event.
- Status changes, metadata, and events persist in one Unit of Work.
- Events are dispatched only after successful commit.
- No automatic retry may silently apply a lifecycle command to a newer state; the caller must reread and deliberately retry.

## No time-driven status

Expiration, inactivity, elapsed provisioning time, unpaid billing, or subscription dates do not automatically mutate Tenant status. Any future integration must issue an explicit authorized lifecycle command and preserve its own source decision separately.
