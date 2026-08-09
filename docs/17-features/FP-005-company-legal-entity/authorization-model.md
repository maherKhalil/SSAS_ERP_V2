---
document_id: FP-005-AUTH
title: Company / Legal Entity Authorization Model
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Authorization Model

> Approved for Implementation — model reflecting the approved human decisions.

## Authorization plane

Company administration is a Platform capability performed **within** a tenant by an authenticated caller that holds the required Platform company permission and operates in a trusted current-tenant context. It differs from the Tenant lifecycle plane (`FP-003`), which is a Platform-level plane outside ordinary tenant context: company operations always target a company owned by the current tenant and never cross tenant boundaries.

User↔company assignment (which users may act within which companies, `BR-PLT-0002`) is **not** part of Milestone 1. No company-membership model or company-scoped access control is defined here; Milestone 1 company administration is tenant-level and promises no company-specific user authorization.

## Permissions

Company uses a three-permission, code-owned set following the established `Platform.<Resource>.<Action>` convention:

| Permission | Grants |
|---|---|
| `Platform.Companies.View` | Read companies within the current tenant (get by id, list, detail). |
| `Platform.Companies.Manage` | Create a company and update a company profile. |
| `Platform.Companies.Lifecycle` | Change company lifecycle state: activate, deactivate, archive. |

Rationale: reading, profile management, and higher-risk lifecycle transitions are meaningfully distinct responsibilities. Separating `Lifecycle` from `Manage` lets an administrator edit a company's display name without holding authority to deactivate or archive it. A finer split (separate activate / deactivate / archive permissions, or a separate Archive permission) is intentionally avoided in Milestone 1.

These permissions are defined in the code-owned Platform permission catalog. They are not tenant-defined. No implicit permission inheritance is assumed beyond what the existing authorization framework already provides.

## Operation classification

| Operation | Required permission |
|---|---|
| CreateCompany | `Platform.Companies.Manage` |
| UpdateCompanyProfile | `Platform.Companies.Manage` |
| GetCompanyById | `Platform.Companies.View` |
| ListCompanies | `Platform.Companies.View` |
| ActivateCompany | `Platform.Companies.Lifecycle` |
| DeactivateCompany | `Platform.Companies.Lifecycle` |
| ArchiveCompany | `Platform.Companies.Lifecycle` |

## Trusted tenant and target company

- The owning `TenantId` is derived only from the trusted current tenant context; it is never accepted from the route, body, header, claim, or query string.
- Every operation targets a `CompanyId` that must belong to the current tenant. The existing tenant query filter ensures a company owned by another tenant is not visible.
- A `CompanyId` from another tenant, or an unknown `CompanyId`, yields the same not-found result; existence is never disclosed across tenants.
- Actor and target metadata are audited through trusted server-side context.

## Company scope resolution is deferred

Milestone 1 defines no company scope-resolution mechanism. A future mechanism may use a token scope claim, a request-selected scope validated server-side, a membership-backed scope, or trusted route/context resolution; FP-005 does not choose among them (`ADR-014`). Whatever mechanism is later chosen, these invariants hold: company **status is validated live** (never trusted solely from a token claim), the company must belong to the trusted tenant, and the caller must be authorized for the company. The existing `ICurrentUser.CompanyId` and `JwtClaimTypes.CompanyId` are existing plumbing only and are not a commitment to a claim-based design. Milestone 1 populates no company scope.

## Auditing

Company lifecycle events contain domain facts only. Correlation ID, request ID, trace ID, and authenticated actor metadata remain outside Domain and use the existing event-dispatch metadata boundary. Immutable security-audit storage is not delivered by FP-005 and remains a production-release dependency; Company must not depend on the FP-004 localization-specific audit-readiness abstraction. A generalized immutable administrative-audit capability is required before production company **mutations** are enabled (see `DEC-CMP-0018`); read-only company operations are not blocked by that production gate.
