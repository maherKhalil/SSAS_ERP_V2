---
document_id: FP-004
title: Localization and Tenant Text Customization
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
depends_on:
  - ADR-005
  - ADR-007
  - ADR-008
  - ADR-009
  - FP-001
  - FP-002
  - FP-003
---

# Feature Package 004 - Localization and Tenant Text Customization

## Approval

FP-004 is approved for implementation. The 36 decisions in [decisions-approved.md](decisions-approved.md) are binding. There is no unresolved Milestone 1 implementation blocker. Production management remains subject to the immutable-audit readiness gate and catalog compatibility preflight described below.

## Purpose

Provide safe English and Arabic system text, exact language resolution, tenant-specific ordinary-text overrides, immutable override history, explicit Undo and Restore Default, and bounded tenant-aware caching without changing technical codes, authorization outcomes, or tenant boundaries.

## Approved scope

- Exactly `en` and `ar`, with culture-derived `ltr` and `rtl` direction.
- One version-controlled UTF-8-without-BOM JSON manifest validated by a versioned JSON Schema.
- Deterministic backend and framework-neutral client artifacts generated from that manifest.
- Localization-owned `TenantLocalizationSettings` and tenant override/history persistence in `PlatformDbContext`.
- PlainText and MultilineText only, exact placeholder grammar, compatibility fingerprints, immutable history, Undo, and Restore Default.
- Single and bounded-batch effective resolution with safe fallback, versioned process-local caching, and SQL revalidation.
- Future current-Tenant management API contracts under `/api/platform/localization`.
- Stable technical ProblemDetails codes plus ResourceKey and optional localized display text.

The proposed future catalog source is `src/BuildingBlocks/SSAS.BuildingBlocks.Localization/Catalog/localization-catalog.json`, with its versioned schema beside it. This package does not create that directory or either artifact.

## Milestone boundaries

### Milestone 1 - backend core

Milestone 1 includes the authoritative catalog/schema and deterministic generation contracts; backend and neutral client artifacts; value objects; exact parser and fingerprints; override aggregate and immutable history; settings/version stamp; create/update, Undo, Restore Default, and history Application contracts; single and bounded-batch resolution; local-cache abstraction; SQL version revalidation; `PlatformDbContext` persistence and SQL Server migration; and Domain, Application, SQL Server, cache-coherence, catalog-compatibility, and architecture tests.

Milestone 1 excludes HTTP endpoints, OpenAPI, Angular runtime/screens, distributed-cache providers, immutable-audit-store implementation, rich text, and import/export.

### Milestone 2 - HTTP integration

Milestone 2 implements the nine approved routes, the three code-owned permissions, exact ProblemDetails mappings, OpenAPI, route-level security and contract tests, and the Production management feature gate. No route accepts writable `TenantId`; no physical DELETE route exists.

### Angular boundary

Angular remains the approved web framework under ADR-007, but its runtime localization implementation is deferred. Milestone 1 may generate only framework-neutral client JSON. A later Angular milestone selects the client library, loads bounded groups, switches language at runtime, applies root direction, and clears prior-Tenant localization state on Tenant switch.

## Production gates

Production localization completeness requires both `en` and `ar` defaults for every Active resource. Production startup must reject a local `CatalogVersion` lower than the database's highest activated version. Production management endpoints remain disabled until immutable-audit persistence, retention, and health/readiness are approved and operational; unavailable readiness is an HTTP 503 service failure. Effective read-only resolution may operate while management is disabled for authenticated trusted live-Tenant callers; M2 exposes no anonymous localization HTTP endpoint.

## Architecture constraints

- SQL never owns mutable system defaults; it stores tenant settings, overrides, immutable versions, and activation/version state only.
- Trusted `ICurrentTenant` and live FP-003 `Active` eligibility are mandatory for tenant-effective access and management.
- Tenant query filters, write ownership, restricted relationships, rowversion, and real SQL Server verification follow ADR-005 and ADR-008.
- Domain events contain identifiers and version numbers, never full text. Correlation/request/actor/trace metadata stays in dispatch metadata, consistent with the existing dispatcher. ADR-009 wording should be clarified in a later documentation cleanup.
- Technical code, HTTP status, authorization, validation, and generic security semantics are never localized.
- Rich text, HTML, CSS, JavaScript, template execution, and Tenant-controlled direction are outside this package.

## Documents

1. [requirements.md](requirements.md)
2. [business-rules.md](business-rules.md)
3. [domain-model.md](domain-model.md)
4. [localization-resolution-model.md](localization-resolution-model.md)
5. [authorization-model.md](authorization-model.md)
6. [api-contracts.md](api-contracts.md)
7. [data-model.md](data-model.md)
8. [acceptance-criteria.md](acceptance-criteria.md)
9. [test-scenarios.md](test-scenarios.md)
10. [decisions-approved.md](decisions-approved.md)
11. [traceability-matrix.md](traceability-matrix.md)

Together with this README, exactly 12 FP-004 documents constitute the approved package.
