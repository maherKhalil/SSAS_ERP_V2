---
document_id: FP-004-REQ
title: Localization Requirements
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Requirements

## Business requirements

### BR-LOC-0001 - Bilingual experience

Approved user-facing text is available in exactly English (`en`) and Arabic (`ar`) with runtime language switching and culture-derived direction.

### BR-LOC-0002 - Tenant terminology

An eligible Tenant may customize catalog-authorized ordinary text independently by culture without changing system defaults or another Tenant's text.

### BR-LOC-0003 - Trusted defaults

One reviewed, version-controlled catalog supplies deterministic system defaults and safe fallback behavior.

### BR-LOC-0004 - Predictable personalization

Language choice follows one documented precedence and remains independent of authentication claims and regional formatting.

### BR-LOC-0005 - Historical accountability

Every Tenant text mutation appends immutable history with actor, time, lineage, and version identity.

### BR-LOC-0006 - Safe presentation

Localization cannot alter technical behavior, disclose protected state, or introduce executable content.

### BR-LOC-0007 - Tenant isolation

Tenant localization data is derived from trusted current context and never crosses Tenant boundaries in persistence, caches, APIs, events, or diagnostics.

### BR-LOC-0008 - Release resilience

Catalog evolution, cache coherence, startup compatibility, retention, and Production gates fail safely.

## Functional requirements

### FR-LOC-0101 - Supported cultures

The culture vocabulary is exactly `en` and `ar`; `en` maps to `ltr`, `ar` maps to `rtl`, and unsupported values fall through approved precedence.

### FR-LOC-0102 - Authoritative catalog

One UTF-8-without-BOM JSON manifest is authoritative. A versioned JSON Schema validates it; entries are ordered by ordinal ResourceKey; duplicates are rejected; deterministic backend and neutral client artifacts are generated from it. YAML and SQL system-default catalogs are prohibited.

### FR-LOC-0103 - Resource key validation

ResourceKey is immutable, ordinal, globally unique, module-owned, and never renamed or reused after retirement.

### FR-LOC-0104 - Default completeness

Every Active Production-complete resource has both `en` and `ar` defaults. Explicitly incomplete non-Production validation may use English fallback and must report the missing culture.

### FR-LOC-0105 - Independent overrides

One Tenant may hold independent `en` and `ar` overrides only for Active, compatible, TenantOverridable, ordinary resources.

### FR-LOC-0106 - Effective resolution

Resolution is compatible active Tenant override, requested-culture system default, English system default, then the system-owned neutral monitored fallback.

### FR-LOC-0107 - Bounded batch resolution

The resolver supports a single key and a deterministic bounded batch. The Application contract sets a positive maximum and rejects duplicates or excess before persistence/cache access.

### FR-LOC-0108 - Placeholder validation

The exact parser accepts `{name}` where name matches `[A-Za-z][A-Za-z0-9_]{0,63}`, permits `{{`/`}}` literal braces and repeated names, rejects malformed syntax, and compares distinct names ordinally and case-sensitively.

### FR-LOC-0109 - Safe formats

Only PlainText and MultilineText exist. PlainText is at most 512 UTF-16 code units, is stored in `nvarchar(512)`, and contains no control characters. MultilineText is at most 4000 UTF-16 code units, is stored in `nvarchar(4000)`, permits CR/LF/TAB, and rejects NUL and other prohibited controls.

### FR-LOC-0110 - Immutable version history

Create, update, Undo, and Restore Default append immutable versions with monotonic TenantOverrideVersion, lineage, catalog identity, fingerprints, actor, and UTC time.

### FR-LOC-0111 - Undo

The server advertises the eligible explicit lineage predecessor. Undo requires that target and expected rowversion, rejects arbitrary/incompatible targets, and appends a new Undo version.

### FR-LOC-0112 - Restore default

Restore Default retains the aggregate, sets `IsActive=false`, nulls the current value, advances CurrentVersionNumber, appends RestoredDefault, and resolves the current system default.

### FR-LOC-0113 - Reapply after restore

A later PUT reactivates the same retained aggregate identity as an update and requires its expected rowversion.

### FR-LOC-0114 - Resource administration reads

Future reads provide bounded catalog/override compatibility projections for the trusted current Tenant without exposing writable TenantId.

### FR-LOC-0115 - History read

Authorized history retrieval is bounded, newest-first with stable tie-breaking, includes lineage and compatibility metadata, and never returns another Tenant's history.

### FR-LOC-0116 - Preview

Preview validates culture, format, UTF-16 length, placeholders, classification, and editability without persistence, versions, events, shared-cache insertion, or submitted-text logging.

### FR-LOC-0117 - Language selection

Anonymous precedence is explicit browser/session choice, supported Accept-Language, `en`. Authenticated precedence is explicit current-session choice, persisted user preference when that future boundary exists, Tenant default, `en`.

### FR-LOC-0118 - Effective language metadata

Effective-text projections expose ResourceKey, Value, RequestedCulture, ResolvedCulture, Direction, ResolutionSource, CatalogVersion, ResourceVersion, and TenantLocalizationVersion when applicable.

### FR-LOC-0119 - Catalog release compatibility

Catalog validation reports retained incompatible overrides; wording-only default changes keep compatible overrides, while placeholder, format, overridable, or security changes may invalidate them without rewriting/deleting them.

### FR-LOC-0120 - Cache invalidation

TenantLocalizationVersion advances transactionally after every successful mutation; local entries are evicted post-commit and revalidated against SQL under the approved timing model.

### FR-LOC-0121 - Safe events

Past-tense events contain EventId, TenantId, aggregate/key/culture and version identifiers only. Full localized text is excluded; dispatch metadata carries correlation/request/actor/trace values.

### FR-LOC-0122 - ProblemDetails integration

API errors retain immutable technical code/status/type, add stable ResourceKey, and may add safe localized title/detail plus useful RequestedCulture/ResolvedCulture metadata.

### FR-LOC-0123 - Angular integration contract

Generated client JSON is framework-neutral in Milestone 1. Angular runtime, screens, library selection, loading, switching, and direction application are deferred.

### FR-LOC-0124 - Tenant lifecycle enforcement

Only a live FP-003 Active Tenant may manage or receive Tenant-effective text. Tenant switching clears prior-Tenant localization state; ineligible requests use no Tenant override.

### FR-LOC-0125 - Tenant settings ownership

`TenantLocalizationSettings` owns TenantDefaultCulture and TenantLocalizationVersion. UserPreferredCulture remains in a future user-profile/settings boundary; Milestone 1 accepts requested culture explicitly.

### FR-LOC-0126 - Settings initialization

Migration bootstraps existing Tenants; missing rows self-heal transactionally; concurrent inserts rely on unique TenantId, with the duplicate-key loser reloading and retrying; the version begins at 1.

### FR-LOC-0127 - Neutral Production marker

The system-owned non-overridable fallback is `Text unavailable` for `en` and `النص غير متاح` for `ar`; ordinary output never includes the missing ResourceKey, while telemetry may.

### FR-LOC-0128 - Placeholder fingerprint

Distinct names are ordinally sorted, joined by one LF, UTF-8 encoded, SHA-256 hashed, and stored as `binary(32)`.

### FR-LOC-0129 - Version concepts

CatalogSchemaVersion is positive integer; CatalogVersion is positive monotonic Int64/JSON integer/SQL bigint; ResourceVersion is positive per-key Int32; TenantLocalizationVersion and TenantOverrideVersion are positive Int64 values.

### FR-LOC-0130 - Compatibility fingerprint

ResourceKey, TextFormat, SecurityClassification, TenantOverridable, and sorted distinct placeholders are LF-joined using canonical ordinal strings, UTF-8 encoded, SHA-256 hashed, and persisted as `binary(32)` with versions.

### FR-LOC-0131 - Current/version relationship

Current state stores CurrentVersionNumber and has no CurrentVersionId/composite FK back to versions. Versions have the sole restricted FK to the aggregate and are unique by aggregate identity plus VersionNumber. Create inserts aggregate then version; existing mutations insert the version then advance current state, all within one transaction.

### FR-LOC-0132 - PUT concurrency semantics

Future PUT uses nullable `expectedRowVersion`: null is create-only; non-null is update-only. Existing-on-create, missing-on-update, and stale-update are deterministic conflicts; a restored row counts as existing.

### FR-LOC-0133 - Cache timing and degradation

Cached Tenant versions revalidate at least every 15 seconds, healthy cross-instance staleness is expected not to exceed 30 seconds, absolute local lifetime is five minutes, and failed SQL revalidation permits overrides for at most 60 seconds after last success before system-default fallback.

### FR-LOC-0134 - Rollback preflight

Production startup records the highest activated CatalogVersion and refuses a lower local version. It never silently lowers the stored version; rollback requires an approved package/review or verified-backup procedure.

### FR-LOC-0135 - Immutable audit gate

Production management remains disabled until immutable-audit persistence, retention, and health/readiness are operational. Read-only effective resolution remains available.

### FR-LOC-0136 - Audit value sourcing

After commit, the audit projector reads prior/new values from referenced immutable version rows. Restore records prior value and new inactive/default state; ordinary events/logs/telemetry omit raw text.

### FR-LOC-0137 - Regional formatting context

A framework-neutral typed contract may carry timezone, date culture, number culture, currency culture, and currency code independently of text culture; persistence and complex formatting are deferred.

### FR-LOC-0138 - Resource retirement

Retired keys remain in catalog lifecycle/history, accept no new overrides, stay out of ordinary groups, are never reused/renamed, and transfer no Tenant wording to replacement keys.

### FR-LOC-0139 - Catalog activation state

SQL stores the highest successfully activated CatalogVersion for preflight but never becomes the mutable authority for definitions/defaults.

### FR-LOC-0140 - HTTP route contract

Milestone 2 exposes exactly the nine approved current-Tenant routes, slash-style batch path, no DELETE, no writable TenantId, exact permissions, schemas, limits, projections, ProblemDetails, and OpenAPI.

### FR-LOC-0141 - HTTP rowversion representation

Milestone 2 localization HTTP transports exposed rowversions as canonical padded RFC 4648 Base64 strings compatible with .NET `System.Text.Json` byte-array representation. Expected rowversions must be nonblank, untrimmed, decodable, exact SQL-rowversion length, and canonical; Base64Url, hexadecimal, whitespace, and alternative encodings are request-validation failures.

### FR-LOC-0142 - Effective HTTP authentication boundary

All nine Milestone 2 localization routes are non-anonymous. Effective group and batch HTTP requests require authentication, trusted current Tenant context, and live eligibility, but ordinary runtime effective localization requires no `Platform.Localization.View` permission. Public/pre-Tenant HTTP exposure is deferred and no catalog public metadata or API allowlist is added in Milestone 2.

### FR-LOC-0143 - Localization HTTP concurrency mapping

The existing internal `Persistence.ConcurrencyConflict` remains unchanged. A valid but stale localization expected rowversion maps at the HTTP boundary to HTTP 409 `concurrency.conflict`; malformed or missing required rowversions are request-validation failures.

### FR-LOC-0144 - Audit-readiness service response

Production localization-management mutations fail closed with HTTP 503 `localization.audit_readiness_unavailable` when immutable-audit persistence, retention, or readiness is unavailable. The failure performs no mutation, database write, event, cache eviction, submitted-text logging, or internal-cause disclosure.

## Security requirements

### SEC-LOC-0201 - Trusted tenant context

Tenant scope comes only from validated current context; route, query, header, and body TenantId cannot select ownership.

### SEC-LOC-0202 - Immutable technical values

Localization never changes technical codes, HTTP status/type, authorization, validation, claims, permission names, resource keys, or control flow.

### SEC-LOC-0203 - Generic authentication semantics

Account, Tenant, membership, state, lockout, credential, token-validation, and authorization causes retain one approved generic outward semantic.

### SEC-LOC-0204 - Security resource classification

Catalog classification is exactly `Ordinary` or `SecuritySensitiveNonOverridable`; high-risk authentication/authorization text is non-overridable.

### SEC-LOC-0205 - Text injection defense

Stored and returned values are text only and safely encoded; HTML, script, CSS, executable URI, templates, and rich content are not supported.

### SEC-LOC-0206 - Placeholder output encoding

Placeholder values are encoded as text after resolution and never reparsed or interpreted as markup/code.

### SEC-LOC-0207 - Exact comparison

Resource identifiers, placeholder names, cultures, and canonical fingerprint inputs use ordinal comparison; no destructive Unicode normalization or automatic trim is applied to submitted text.

### SEC-LOC-0208 - Authorization separation

The exact future codes are `Platform.Localization.View`, `Platform.Localization.Manage`, and `Platform.Localization.ViewHistory`; permissions grant no cross-Tenant or lifecycle authority.

### SEC-LOC-0209 - Live eligibility

Tenant-effective reads/management require current live Active status and cannot be authorized solely by JWT or localization cache.

### SEC-LOC-0210 - Safe errors and logs

Raw submitted text, placeholder values, credentials, tokens, protected causes, and cross-Tenant content are excluded from ordinary logs, telemetry, errors, and events.

### SEC-LOC-0211 - No physical deletion

No command, route, cascade, repository, or maintenance path physically deletes overrides or immutable versions.

### SEC-LOC-0212 - Stale and incompatible safety

Incompatible or over-grace cached Tenant text is never rendered; the resolver uses system defaults without cross-Tenant substitution.

### SEC-LOC-0213 - Preview authorization

Preview requires Manage and cannot make a non-overridable resource appear editable.

### SEC-LOC-0214 - Public resolution boundary

The Milestone 1 engine may support pre-Tenant system defaults, but Milestone 2 exposes no anonymous localization HTTP route. Any future anonymous access requires an explicitly approved public contract; Tenant-effective HTTP groups require trusted Tenant selection.

### SEC-LOC-0215 - Audit readiness enforcement

Production management refuses operation when immutable-audit readiness is missing/unhealthy; bypass by configuration, cache, or direct handler is prohibited.

### SEC-LOC-0216 - Catalog release security

Security-sensitive incompatibility blocks Production release; ordinary incompatibility requires explicit reviewed acknowledgement.

### SEC-LOC-0217 - Cross-Tenant not-found safety

Invisible aggregate/resource cases follow the repository-safe not-found convention and never reveal another Tenant's existence.

### SEC-LOC-0218 - Output culture safety

RequestedCulture and ResolvedCulture report only supported culture values and cannot alter access decisions.

### SEC-LOC-0219 - Milestone 2 HTTP exposure

No Milestone 2 localization HTTP route is anonymous. Effective endpoints require authenticated trusted Tenant context; a non-Active Tenant receives no Tenant override and follows approved eligibility semantics. Arbitrary public catalog exposure is prohibited.

## Non-functional requirements

### NFR-LOC-0301 - Clean Architecture

Domain/Application contracts remain framework-neutral; Infrastructure owns JSON/SQL/cache adapters and Host owns future HTTP composition.

### NFR-LOC-0302 - Asynchronous boundaries

I/O contracts are asynchronous, cancellation-aware, and return projections rather than entities or `IQueryable`.

### NFR-LOC-0303 - Tenant-safe performance

Queries, indexes, cache keys, and batches are bounded and include Tenant/culture/version dimensions where applicable.

### NFR-LOC-0304 - SQL Server verification

Provider-specific types, constraints, rowversion, concurrency, migrations, and deletion behavior are verified on real SQL Server.

### NFR-LOC-0305 - Multi-instance coherence

Multiple instances observe committed localization changes within approved healthy/degraded timing bounds without distributed cache.

### NFR-LOC-0306 - Catalog validation

Schema, encoding/BOM, ordering, duplicates, completeness, parser, fingerprints, compatibility impact, retirement, and deterministic artifacts are build/release validated.

### NFR-LOC-0307 - Audit readiness

Management readiness and audit projection are observable, fail closed in Production, and preserve protected values.

### NFR-LOC-0308 - Accessibility and RTL usability

Future UI supports runtime direction, accessible text/focus, and safe bidi isolation without Tenant direction overrides.

### NFR-LOC-0309 - Deployment safety

Migration upgrade/downgrade/reapply and catalog startup preflight are automated release gates; Production schema is not auto-mutated unsafely.

### NFR-LOC-0310 - Quality gates

Formatting, analyzers, build, tests, architecture, traceability, link, and diff checks pass with no unrelated changes.

### NFR-LOC-0311 - Exact text measurement

Domain, API, SQL, and tests measure .NET/SQL UTF-16 code units identically, including surrogate-pair boundaries.

### NFR-LOC-0312 - Deterministic cryptography

Placeholder and compatibility fingerprints use exactly the approved canonicalization, UTF-8 encoding, SHA-256, and 32-byte storage.

### NFR-LOC-0313 - Cache bounds

Process-local cache is size-bounded, has five-minute absolute lifetime, 15-second version revalidation, 30-second healthy expected staleness, and 60-second failed-validation grace.

### NFR-LOC-0314 - Traceability completeness

Every FP-004 identifier and every approved decision, route, permission, persistence rule, and conflict has unique definition and bidirectional traceability.

### NFR-LOC-0315 - Unicode preservation

Submitted Unicode and line endings are preserved exactly when valid; visually equivalent sequences may remain distinct.
