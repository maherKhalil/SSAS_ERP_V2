---
document_id: FP-004-RES
title: Localization Resolution Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Localization Resolution Model

## Authoritative source

One version-controlled UTF-8-without-BOM JSON manifest, validated by its versioned JSON Schema, is authoritative. Entries are ResourceKey-ordinal and duplicate-free. Backend and framework-neutral client artifacts are deterministic outputs. SQL contains no mutable system-default definition catalog. Proposed future location: `src/BuildingBlocks/SSAS.BuildingBlocks.Localization/Catalog/localization-catalog.json`; it is not created by this documentation task.

## Culture selection

Anonymous/pre-authentication: (1) explicit current browser/session choice, (2) first supported Accept-Language value, (3) `en`.

Authenticated: (1) explicit current-session choice, (2) future persisted UserPreferredCulture, (3) TenantLocalizationSettings.TenantDefaultCulture, (4) `en`.

Only `en`/`ar` survive. Unsupported inputs fall through. Geography, Tenant name, currency, timezone, and access-token claims never infer language; a language change needs no logout. Tenant switch clears previous Tenant localization state. Until the user-profile boundary exists, Milestone 1 accepts requested culture explicitly.

## Resolution algorithm

For a supported requested culture:

1. Require trusted current Tenant and live FP-003 Active eligibility before Tenant-effective lookup. If unavailable/ineligible, skip Tenant state.
2. Use an active Tenant override only when catalog ResourceKey, culture, format, classification, overridable flag, and compatibility fingerprint match.
3. Otherwise use requested-culture system default.
4. If it is absent only in explicitly incomplete non-Production validation, use English default and diagnose missing culture.
5. Otherwise use the system-owned neutral marker: `Text unavailable` (`en`) or `النص غير متاح` (`ar`), with English marker only if the requested marker is unavailable.

Neutral marker output is non-overridable, reports KeyFallback, never includes ResourceKey in ordinary output, and may include the key only in telemetry or explicit non-Production developer diagnostics. ResolvedCulture is requested culture when that marker exists, else `en`.

Production completeness requires both defaults for every Active resource; there is no Arabic waiver. Retired resources are excluded from ordinary groups. Incompatible overrides are retained/history-visible but not rendered and cannot be restored by Undo.

## Placeholder and rendering pipeline

Parse resource text using exact braces and name grammar. Distinct names must equal the catalog set; order and repeated occurrences may differ. Resolve the effective raw template first, then optionally substitute encoded text values once; never reparse substituted values. Administration and effective GET stop after template selection. Effective POST batch performs substitution when exact resource-scoped placeholder values are supplied. Text is never markup/code. PlainText/MultilineText validation counts UTF-16 code units and preserves valid Unicode and line endings without normalization or trim.

RequestedCulture and ResolvedCulture are separate. Direction derives only from ResolvedCulture (`en/ltr`, `ar/rtl`). FormattingContext is separate; text culture does not select currency/timezone.

## Cache model

- System catalog: immutable in-process object identified by CatalogVersion.
- Tenant cache: size-bounded process-local entries keyed by TenantId, culture/group/key, CatalogVersion, and TenantLocalizationVersion as applicable.
- Revalidation: each cached Tenant version is checked against SQL at least every 15 seconds.
- Healthy operation: maximum expected cross-instance stale override visibility is 30 seconds.
- Absolute lifetime: five minutes per local entry.
- Mutation: state/history/settings version commit atomically; mutation reports success only after commit; local eviction and event dispatch happen post-commit.
- SQL validation failure: existing Tenant overrides may be served for at most 60 seconds since last successful validation. Thereafter they are untrusted and system defaults are used; telemetry/health report degradation. Never substitute another Tenant's cache entry.

Live Tenant eligibility is checked independently before every Tenant-effective use, so suspension after population immediately prevents cache reuse through the existing authorization mechanism. Distributed cache and message-bus invalidation are deferred.

## Catalog release and rollback

Validation checks schema/encoding/order/duplicates/completeness, exact parser/fingerprints, deterministic artifacts, lifecycle, ResourceVersion/CatalogVersion changes, and produces an incompatibility impact report. Wording-only changes preserve compatible overrides; policy/placeholder/format/security changes may invalidate. Security-sensitive incompatibility blocks Production; ordinary incompatibility requires explicit release review.

Production activation stores the highest successfully activated CatalogVersion. Startup refuses a lower local version, never lowers the stored value, and treats ordinary rollback as unsupported. Recovery is redeploying the compatible newer application or an explicitly prepared/reviewed catalog/database rollback procedure from verified backup that preserves columns/history and leaves incompatible overrides retained/unused.

## Delivery boundary

Milestone 1 supplies backend single/bounded-batch contracts and neutral client JSON only. Milestone 2 supplies authenticated HTTP/OpenAPI only; it exposes no anonymous pre-Tenant localization route. Angular runtime/libraries/screens are later; pre-authentication system-default use remains an engine capability, private Tenant-effective groups load only after trusted selection, and Tenant switch clears them.
