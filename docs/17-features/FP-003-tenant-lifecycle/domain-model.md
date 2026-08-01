---
document_id: FP-003-DOM
title: Tenant Lifecycle Domain Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Domain Model

## Bounded context

**Platform Tenant Lifecycle**

This bounded context owns the existence, stable identity, display identity, lifecycle state, and authentication eligibility of a tenant. It does not own tenant business data, subscriptions, companies, authentication sessions, or support authorization.

## Tenant aggregate

`Tenant` is the aggregate root.

Fields:

- `TenantId: Guid`;
- `TenantCode`;
- `NormalizedTenantCode`;
- `TenantName`;
- `Status`;
- `CreatedUtc`, `CreatedBy`;
- `ModifiedUtc`, `ModifiedBy`;
- `StatusChangedUtc`, `StatusChangedBy`;
- `StatusChangeReasonCode`;
- SQL Server `RowVersion`.

`NormalizedTenantName` is not stored because TenantName is not globally unique. `LegalName` is deferred.

## Responsibilities

The aggregate:

- creates a Tenant in `Provisioning`;
- preserves immutable `TenantId` and tenant code;
- enforces the approved transition graph;
- derives authentication eligibility from status;
- records safe status-change metadata;
- raises safe lifecycle events;
- rejects stale writes through persistence rowversion;
- exposes no physical-delete behavior.

Global uniqueness of normalized tenant code remains a database-backed invariant coordinated by the Application layer and unique index.

## Value objects

### TenantCode

- required;
- trimmed display value;
- maximum length 64 characters;
- normalized using `Trim().ToUpperInvariant()`;
- exact ordinal comparison;
- immutable after creation.

### TenantName

- required;
- trimmed;
- display casing preserved;
- maximum length 200 characters;
- mutable only through an approved Tenant update operation;
- not globally unique.

### TenantStatusChangeReasonCode

This bounded domain value contains exactly `Created`, `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, and `IssueResolved`. Creation records `Created`; every lifecycle transition records a code, and Suspend and Archive require an explicit non-`Created` code. Domain events carry only the code and never free-form reason text.

## Enumeration

`TenantStatus` contains exactly:

- `Provisioning`;
- `Active`;
- `Suspended`;
- `Archived`.

## Domain events

- `TenantCreated`;
- `TenantActivated`;
- `TenantSuspended`;
- `TenantReactivated`;
- `TenantArchived`.

Events may contain TenantId, previous and new status, occurrence time, and safe reason code. Correlation, request, actor, and trace metadata remain outside Domain and are attached by the existing dispatch infrastructure.

Events contain no credentials, tokens, complete claims, billing information, subscription secrets, or HTTP context.

## Repository contract

Per ADR-010, define one aggregate-specific `ITenantRepository` in Platform Application and one implementation in Platform Infrastructure.

It may expose only domain-focused operations such as:

- get by TenantId;
- get by normalized tenant code;
- test normalized code uniqueness;
- add Tenant.

It exposes no generic CRUD, delete method, `IQueryable`, authorization behavior, or transaction management.

## Authentication eligibility contract

`ITenantAuthenticationEligibilityReadService` is an Application read contract, not a repository and not a cross-tenant business-data service.

Operation:

```text
GetEligibilityAsync(TenantId tenantId, CancellationToken cancellationToken)
```

Safe result:

- `TenantId`;
- `Exists`;
- nullable `TenantStatus`;
- computed `IsAuthenticationEligible`;
- `TenantAuthenticationIneligibilityReason`.

`TenantAuthenticationIneligibilityReason` contains exactly `None`, `TenantNotFound`, `Provisioning`, `Suspended`, and `Archived`. A missing Tenant returns `Exists = false`, null status, false eligibility, and `TenantNotFound`; an Active Tenant returns `None`. The Boolean and reason are derived inside the implementation and cannot be supplied by a caller. Tenant name is omitted from this security-focused result; ordinary tenant lookup queries may return a safe display name separately.
