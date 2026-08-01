---
document_id: FP-003-AUTH
title: Tenant Lifecycle Authorization Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Authorization Model

## Authorization plane

Tenant lifecycle operations are Platform-level operations outside ordinary tenant administration.

An ordinary tenant role, including a role named Administrator, does not authorize creating, activating, suspending, reactivating, archiving, listing, or administering Tenant lifecycle records.

App Owner/App Support authorization remains a separate Platform authorization plane under FP-001 and FP-002. Concrete support authentication, mandatory MFA, support permissions, impersonation, and target-tenant workflows remain deferred.

## Operation classification

| Operation | Authorization classification |
|---|---|
| CreateTenant | Explicit Platform lifecycle permission required |
| GetTenant | Explicit Platform lifecycle read permission required |
| ListTenants | Explicit Platform lifecycle read permission required |
| ActivateTenant | Explicit Platform lifecycle permission required |
| SuspendTenant | Explicit Platform lifecycle permission required |
| ReactivateTenant | Explicit Platform lifecycle permission required |
| ArchiveTenant | Explicit Platform lifecycle permission required |
| GetTenantAuthenticationEligibility | Internal trusted Platform authentication/authorization caller; not an end-user permission decision |

Exact permission identifiers remain deferred until Platform-support authentication and authorization are defined. The first implementation milestone contains no HTTP endpoints.

## Authentication-eligibility contract

`ITenantAuthenticationEligibilityReadService` reports current lifecycle fact. It does not authorize the caller, issue claims, validate a tenant role, or grant access to business data.

The caller remains responsible for:

- authenticating the Identity where applicable;
- validating active membership;
- validating session and client state;
- evaluating roles and permissions;
- enforcing tenant-owned repository isolation.

## Target TenantId

Platform lifecycle commands necessarily target a Platform-owned Tenant by TenantId. This is not the same as accepting a tenant override in an ordinary tenant operation.

Requirements:

- the route or command TenantId identifies only the lifecycle aggregate to administer;
- explicit Platform authorization is checked before lifecycle data is disclosed or changed;
- the target cannot establish `ICurrentTenant` for business-data access;
- lifecycle administration does not permit querying another tenant's business tables;
- target and actor metadata are audited through trusted server-side context.

## Ordinary tenant access

An Active Tenant is only one authorization prerequisite. It grants no membership, role, permission, company access, or Platform-support authority.

For an ineligible Tenant:

- pre-authentication workflows return generic authentication outcomes where FP-002 requires them;
- authenticated business authorization denies ordinary access according to the approved centralized status-enforcement policy;
- lifecycle status is not inferred from a JWT claim supplied by the client.

## Auditing

Lifecycle events contain domain facts. Correlation ID, request ID, trace ID, and authenticated actor metadata remain outside Domain and use the existing event-dispatch metadata boundary.

Immutable security-audit storage is not delivered by FP-003 and remains a production-release dependency.
