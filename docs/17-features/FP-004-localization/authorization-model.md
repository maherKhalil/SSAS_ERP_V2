---
document_id: FP-004-AUTHZ
title: Localization Authorization Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Authorization Model

## Code-owned permissions

The repository convention is `Platform.<Capability>.<Action>`. Milestone 2 therefore proposes exactly:

| Capability | Exact code | Allows |
|---|---|---|
| View localization resources | `Platform.Localization.View` | list/get administrative views |
| Manage localization overrides | `Platform.Localization.Manage` | create/update, Preview, Undo, Restore Default |
| View localization history | `Platform.Localization.ViewHistory` | history retrieval |

These codes remain documentation proposals until Milestone 2 updates the code-owned permission catalog. They grant no lifecycle, business-data, platform-support, or cross-Tenant authority. Ordinary runtime text resolution needs no administrative permission.

## Operation matrix

| Operation | Authentication | Permission | Tenant/live rules |
|---|---|---|---|
| ordinary authenticated runtime resolution | authenticated | none | trusted current Tenant and live Active before Tenant override |
| list/get administration | authenticated | View | current Tenant, live Active |
| effective group/batch HTTP | authenticated | none for ordinary runtime effective resolution | trusted current Tenant, live Active; non-anonymous in M2 |
| create/update override | authenticated | Manage | current Tenant, live Active, editable resource |
| Preview | authenticated | Manage | current Tenant, live Active, no persistence/cache/event/logged text |
| Undo/Restore Default | authenticated | Manage | current Tenant, live Active, rowversion/lineage rules |
| history | authenticated | ViewHistory | current Tenant, live Active |

No request accepts writable TenantId. Any incidental/unknown TenantId field is rejected by strict schema binding; route/query/header attempts cannot affect trusted scope. Invisible resources/aggregates use repository-safe not-found behavior and do not disclose another Tenant.

## Live lifecycle and cache

Current FP-003 status is authoritative. Provisioning, Suspended, Archived, missing, or not-yet-trusted Tenant context denies management and Tenant-effective HTTP results. A JWT, permission, or populated localization cache cannot override live eligibility. The Milestone 1 engine can resolve pre-authentication system defaults, but M2 exposes no anonymous localization HTTP route. Tenant switch clears previous localization state; suspension after cache population prevents reuse.

## Security-sensitive text

Catalog classification is exactly `Ordinary` or `SecuritySensitiveNonOverridable`. High-risk authentication/authorization messages that could reveal account, Tenant, membership, state, lockout, credential, token-validation cause, or authorization internals are system-owned and non-overridable. All approved internal causes continue to map to one generic outward ResourceKey. Ordinary labels may be overridable only when catalog metadata permits. There is no partially implemented constrained category.

## Preview

Preview requires Manage, uses trusted Tenant, accepts no TenantId, validates culture/length/format/placeholders/classification/editability, safely encodes text-only output, and performs no persistence, version increment, event, shared-cache insertion, or submitted-text logging.

## ProblemDetails and audit gate

Authorization status and immutable technical code/type never localize. ResourceKey and optional safe title/detail are display aids; sensitive causes remain generic. Production management is feature-gated off until immutable-audit persistence, configured retention, and audit readiness/health succeed. A failed audit-readiness check is operational HTTP 503 `localization.audit_readiness_unavailable`, not authorization denial. Effective read-only resolution remains available to authenticated trusted live-Tenant callers. Version history is not the immutable administrative audit store.

## Required security verification

Focused tests cover positive/negative View, Manage, ViewHistory; Preview/Undo/Restore requiring Manage; history denied without ViewHistory; ordinary effective resolution without View; anonymous effective-route denial; no writable/forged TenantId; inactive Tenant; pre-selection denial; cross-Tenant safe absence; and suspension after cache population.
