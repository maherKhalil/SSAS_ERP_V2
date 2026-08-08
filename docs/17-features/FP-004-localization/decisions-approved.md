---
document_id: FP-004-DEC
title: Localization Decisions Approved
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Decisions Approved

All decisions below are binding. Rationale is stated compactly; detailed contracts live in the linked package documents.

## DEC-LOC-0001 - Authoritative catalog
One UTF-8-without-BOM JSON manifest plus versioned JSON Schema is authoritative, ordinal/duplicate-free, and deterministically generates backend/neutral-client artifacts. SQL/YAML are not authorities. Rationale: one reviewable source prevents drift.
## DEC-LOC-0002 - Completeness and missing behavior
Exactly `en`/`ar`; Production Active resources require both. Non-Production may be explicitly incomplete with diagnostics/English fallback. Neutral markers are `Text unavailable` and `النص غير متاح`, system-owned/non-overridable, and ordinary output hides ResourceKey. Rationale: safe bilingual completeness.
## DEC-LOC-0003 - Settings ownership
Localization-owned TenantLocalizationSettings holds TenantDefaultCulture and TenantLocalizationVersion; user preference is a future profile boundary. Rationale: avoid expanding FP-003 lifecycle.
## DEC-LOC-0004 - Language precedence
Anonymous: session, Accept-Language, en. Authenticated: session, future user preference, Tenant default, en. No geographic/claim inference; Tenant switch clears state. Rationale: explicit predictable choice.
## DEC-LOC-0005 - Sensitive classification
High-risk auth/authz text is SecuritySensitiveNonOverridable; ordinary labels may be overridable. No intermediate category. Rationale: preserve generic non-enumerating semantics.
## DEC-LOC-0006 - Permissions
Exact future codes: `Platform.Localization.View`, `.Manage`, `.ViewHistory`; Manage includes Preview/Undo/Restore. Rationale: matches code-owned naming and least privilege.
## DEC-LOC-0007 - API placement
Nine routes use `/api/platform/localization`, slash batch route, trusted current Tenant, and no DELETE/TenantId. Rationale: existing Platform current-Tenant convention.
## DEC-LOC-0008 - Text normalization and limits
Preserve valid text/whitespace/line endings without Unicode normalization or trim; count UTF-16 units; PlainText 512/no controls, Multiline 4000/CR-LF-TAB only. Rationale: exact .NET/SQL agreement.
## DEC-LOC-0009 - Undo lineage
Server advertises explicit eligible predecessor; request supplies target+rowversion; successful Undo appends history and repeated Undo walks lineage. Rationale: deterministic, non-arbitrary history.
## DEC-LOC-0010 - Compatibility
Approved fingerprint/impact report controls retained incompatible overrides; wording-only changes remain compatible, sensitive incompatibility blocks Production. Rationale: safe catalog evolution without rewriting Tenant text.
## DEC-LOC-0011 - Restore representation
Restore retains one inactive aggregate with null value/new RestoredDefault version; later PUT is rowversion update. Rationale: immutable ownership/history and current defaults.
## DEC-LOC-0012 - Cache model
Local bounded cache + SQL TenantLocalizationVersion: 15s revalidation, 30s healthy expected staleness, 5m lifetime, 60s failure grace then defaults. Rationale: bounded multi-instance coherence without deferred infrastructure.
## DEC-LOC-0013 - Immutable audit gate
Development/testing may proceed, but Production management is disabled until immutable-audit persistence/retention/readiness; read-only resolution remains. Rationale: version history is not administrative audit.
## DEC-LOC-0014 - API localization
Errors use technical code + ResourceKey + optional title/detail; statuses/outcomes stay authoritative, with useful RequestedCulture/ResolvedCulture. Rationale: machine stability plus display flexibility.
## DEC-LOC-0015 - Direction
Direction is culture-derived only (`en/ltr`, `ar/rtl`); no resource/Tenant exception. Rationale: smallest safe initial policy.
## DEC-LOC-0016 - Milestone boundary
M1 is backend core/migration/tests; M2 is HTTP/permissions/OpenAPI/routes; Angular runtime later. Rationale: independently verifiable delivery.
## DEC-LOC-0017 - Retirement
Retired keys are retained, never reused/renamed, accept no new overrides, leave ordinary groups, and transfer no wording. Rationale: stable semantics/history.
## DEC-LOC-0018 - Formatting boundary
Typed regional formatting context is independent and persistence/complex formatting are deferred. Rationale: language is not region.
## DEC-LOC-0019 - Settings initialization
Migration bootstraps existing Tenants; missing row self-heals; unique TenantId resolves concurrent first creation by reload/retry; version starts 1. Rationale: deterministic provisioning compatibility.
## DEC-LOC-0020 - PUT concurrency
Nullable expectedRowVersion is create-only when null/update-only otherwise; exact 409 codes cover existing/missing/stale; inactive exists. Rationale: unambiguous idempotent intent.
## DEC-LOC-0021 - Undo errors
Stale 409 `concurrency.conflict`; none 409 `localization.undo_not_available`; invalid 422 `localization.undo_target_invalid`; incompatible 422 `localization.undo_target_incompatible`. Rationale: exact client recovery.
## DEC-LOC-0022 - Preview
Preview requires Manage and has full validation but no persistence/version/event/shared-cache/logged text. Rationale: mutation-equivalent authorization without side effects.
## DEC-LOC-0023 - Cache timing/degradation
Approved exact cache timing and fallback/health behavior is mandatory under healthy and failed SQL validation. Rationale: measurable staleness/safety.
## DEC-LOC-0024 - Tenant lifecycle/cache
Live Active eligibility precedes Tenant cache use/management; suspension invalidates eligibility independently. Rationale: cache cannot become authorization.
## DEC-LOC-0025 - Rollback compatibility
Production records highest activated CatalogVersion and refuses lower local version; rollback requires prepared review/backup recovery, never silent lowering. Rationale: prevent old code/catalog corruption.
## DEC-LOC-0026 - Audit readiness
Production management feature gate requires immutable store, retention, and healthy readiness. Rationale: enforce the audit dependency operationally.
## DEC-LOC-0027 - Audit values
Events omit full text; post-commit audit reads old/new from immutable committed versions, including Restore state. Rationale: meet audit content without leaking event payloads.
## DEC-LOC-0028 - ADR-009 interpretation
TenantId/EventId/OccurredAt remain payload facts; correlation/request/actor/trace are attached to dispatched record/envelope consistent with current dispatcher/FP-003. ADR wording cleanup is a follow-up, not an M1 blocker. Rationale: preserve Domain purity and existing convention.
## DEC-LOC-0029 - ProblemDetails strategy
Technical code remains authoritative; stable ResourceKey and optional safe title/detail are display aids; sensitive causes remain generic. Rationale: localization cannot change behavior.
## DEC-LOC-0030 - Direction policy
Culture alone controls direction; per-resource exceptions are deferred pending evidence. Rationale: prevents Tenant-controlled presentation code.
## DEC-LOC-0031 - Formatting context
Framework-neutral timezone/date/number/currency context is independent; no silent inference from `en`/`ar`. Rationale: avoid regional errors.
## DEC-LOC-0032 - Resource retirement
Lifecycle retention/replacement rules are final and require a new key for replacement. Rationale: immutable meaning and traceable history.
## DEC-LOC-0033 - HTTP rowversion transport
Milestone 2 transports every exposed localization rowversion as canonical padded RFC 4648 Base64, matching .NET `System.Text.Json` byte-array representation. Inputs must be nonblank, untrimmed, successfully decoded, exact SQL-rowversion length, and canonical; Base64Url and hexadecimal are rejected. Rationale: one interoperable wire representation without changing Domain/Application semantics.
## DEC-LOC-0034 - Milestone 2 effective HTTP boundary
All nine Milestone 2 localization routes are non-anonymous. The effective group and batch routes require an authenticated caller with trusted current Tenant context and live eligibility, but no localization administrative permission for ordinary runtime effective resolution. Public/pre-Tenant HTTP exposure, catalog audience metadata, and API allowlists are deferred. Rationale: do not expose arbitrary catalog groups without an approved public contract.
## DEC-LOC-0035 - Localization concurrency HTTP mapping
The internal `Persistence.ConcurrencyConflict` error remains unchanged and maps only at the localization HTTP boundary to HTTP 409 `concurrency.conflict`. A malformed rowversion is request validation, not concurrency. Rationale: preserve repository internals while giving clients a stable HTTP contract.
## DEC-LOC-0036 - Audit-readiness service failure
When Production immutable-audit readiness is unavailable, an otherwise authorized localization-management mutation fails closed with HTTP 503 `localization.audit_readiness_unavailable`. It is operational, not authorization, and leaks no internal cause or submitted text. Rationale: a granted permission cannot make an unavailable required service ready.

## Conflict register

| Conflict | Affected sources | FP-004 identifiers | Decision | Status | Milestone | Remaining dependency |
|---|---|---|---|---|---|---|
| CONFLICT-LOC-0001 | ADR-007; no existing localizer | FR-LOC-0102, AC-LOC-0025 | DEC-LOC-0001 | Resolved by approved decision | M1 | create approved manifest/schema during implementation |
| CONFLICT-LOC-0002 | ADR-007; no Angular workspace | FR-LOC-0123, AC-LOC-0023 | DEC-LOC-0016 | Deferred with explicit non-blocking dependency | Later Angular | Angular workspace/library decision |
| CONFLICT-LOC-0003 | ADR-003; module key ownership | FR-LOC-0103 | DEC-LOC-0001 | Resolved by approved decision | M1 | module catalog contributions |
| CONFLICT-LOC-0004 | bilingual goal; incomplete defaults | FR-LOC-0104, FR-LOC-0127 | DEC-LOC-0002 | Resolved by approved decision | M1 | content supplied for release |
| CONFLICT-LOC-0005 | customization goal; security | SEC-LOC-0204 | DEC-LOC-0005 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0006 | FP-002 generic errors | SEC-LOC-0203 | DEC-LOC-0005 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0007 | current ProblemDetails titles | FR-LOC-0122 | DEC-LOC-0029 | Deferred with explicit non-blocking dependency | M2 | Host HTTP integration |
| CONFLICT-LOC-0008 | localization vs technical codes | SEC-LOC-0202 | DEC-LOC-0014 | Resolved by approved decision | M1/M2 | M2 display integration |
| CONFLICT-LOC-0009 | rich text possibility; no sanitizer | FR-LOC-0109 | DEC-LOC-0008 | Resolved by approved decision | M1 | rich text separate feature |
| CONFLICT-LOC-0010 | ADR-007 RTL; no client | FR-LOC-0101 | DEC-LOC-0015 | Deferred with explicit non-blocking dependency | Angular | client runtime |
| CONFLICT-LOC-0011 | language vs regional settings | FR-LOC-0137 | DEC-LOC-0031 | Resolved by approved decision | M1 contract | settings persistence later |
| CONFLICT-LOC-0012 | anonymous login culture | FR-LOC-0117 | DEC-LOC-0004 | Resolved by approved decision | M1/M2 | browser implementation later |
| CONFLICT-LOC-0013 | FP-003 aggregate ownership | FR-LOC-0125 | DEC-LOC-0003 | Resolved by approved decision | M1 | provisioning integration later |
| CONFLICT-LOC-0014 | history vs current defaults | FR-LOC-0112 | DEC-LOC-0011 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0015 | chronological vs logical Undo | FR-LOC-0111 | DEC-LOC-0009 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0016 | placeholder catalog evolution | FR-LOC-0130 | DEC-LOC-0010 | Resolved by approved decision | M1 | release impact review |
| CONFLICT-LOC-0017 | no physical deletion baseline | SEC-LOC-0211 | DEC-LOC-0011 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0018 | no localization permissions | SEC-LOC-0208 | DEC-LOC-0006 | Deferred with explicit non-blocking dependency | M2 | code catalog update |
| CONFLICT-LOC-0019 | platform-support authority absent | SEC-LOC-0201 | DEC-LOC-0007 | Deferred with explicit non-blocking dependency | Future | separate support feature |
| CONFLICT-LOC-0020 | no cache foundation | FR-LOC-0133 | DEC-LOC-0023 | Resolved by approved decision | M1 | implement local abstraction |
| CONFLICT-LOC-0021 | missing Arabic production behavior | FR-LOC-0104 | DEC-LOC-0002 | Resolved by approved decision | M1 | translated content |
| CONFLICT-LOC-0022 | SQL defaults temptation | BRULE-LOC-0006 | DEC-LOC-0001 | Resolved by approved decision | M1 | none |
| CONFLICT-LOC-0023 | API response localization | FR-LOC-0122 | DEC-LOC-0029 | Deferred with explicit non-blocking dependency | M2 | routes/ProblemDetails |
| CONFLICT-LOC-0024 | Angular milestone boundary | FR-LOC-0123 | DEC-LOC-0016 | Deferred with explicit non-blocking dependency | Angular | workspace/library |
| CONFLICT-LOC-0025 | immutable audit store absent | FR-LOC-0135 | DEC-LOC-0026 | Production gate | Production | approved immutable audit implementation/retention/readiness |
| CONFLICT-LOC-0026 | ADR-009 payload wording vs dispatcher | FR-LOC-0121 | DEC-LOC-0028 | Deferred with explicit non-blocking dependency | Docs follow-up | ADR wording cleanup |
| CONFLICT-LOC-0027 | Tenant lifecycle/cache staleness | FR-LOC-0124 | DEC-LOC-0024 | Resolved by approved decision | M1 | reuse live eligibility |
| CONFLICT-LOC-0028 | migration/catalog rollback | FR-LOC-0134 | DEC-LOC-0025 | Production gate | Production | reviewed rollback package or verified backup procedure |
| CONFLICT-LOC-0029 | no HTTP rowversion convention | FR-LOC-0141 | DEC-LOC-0033 | Resolved by approved decision | M2 | implement canonical validation/output |
| CONFLICT-LOC-0030 | public/pre-Tenant HTTP exposure lacks catalog metadata | FR-LOC-0142 | DEC-LOC-0034 | Resolved by approved decision | M2 | future explicitly approved public contract |
| CONFLICT-LOC-0031 | internal and HTTP concurrency code differ | FR-LOC-0143 | DEC-LOC-0035 | Resolved by approved decision | M2 | boundary mapping only |
| CONFLICT-LOC-0032 | audit readiness previously resembled authorization denial | FR-LOC-0144 | DEC-LOC-0036 | Resolved by approved decision | M2 | fail-closed operational response |

All prior Milestone 1 blockers are resolved. Deferred items are outside M1 and non-blocking; CONFLICT-LOC-0025 and CONFLICT-LOC-0028 are explicit Production gates.
