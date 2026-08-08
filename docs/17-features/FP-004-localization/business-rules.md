---
document_id: FP-004-BRULE
title: Localization Business Rules
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Business Rules

### BRULE-LOC-0001 - Culture vocabulary

Only ordinal lowercase `en` and `ar` are supported. `en` is `ltr`; `ar` is `rtl`. Direction has no per-resource or Tenant override.

### BRULE-LOC-0002 - Formatting independence

Text culture does not infer timezone, date culture, number culture, currency culture, or currency code.

### BRULE-LOC-0003 - Key immutability

A ResourceKey is globally unique, never renamed in place, and never reused after retirement.

### BRULE-LOC-0004 - Key convention

ResourceKey uses an approved module-owned ordinal convention and deterministic manifest ordering; display category is metadata, not ownership.

### BRULE-LOC-0005 - Complete definitions

Every Active Production resource has `en` and `ar` defaults, format, security classification, TenantOverridable, placeholder definition, lifecycle, ResourceVersion, and compatibility fingerprint.

### BRULE-LOC-0006 - Differences only

SQL stores only Tenant settings, overrides, immutable versions, and activation/version state; it never stores a mutable authoritative system-default catalog.

### BRULE-LOC-0007 - One aggregate per tuple

Exactly one retained aggregate may exist per TenantId/ResourceKey/Culture regardless of active state.

### BRULE-LOC-0008 - Immutable ownership

TenantId, ResourceKey, Culture, aggregate identity, and version identity never change after creation.

### BRULE-LOC-0009 - Resolution order

Resolve compatible active Tenant override, requested default, English default, then the culture-specific neutral marker (or English marker). KeyFallback output never reveals ResourceKey.

### BRULE-LOC-0010 - Override eligibility

Only Active, compatible, `Ordinary`, TenantOverridable resources accept create/update/Undo/Restore/Preview; retired or security-sensitive resources do not.

### BRULE-LOC-0011 - Placeholders

Tokens use `{name}` with `[A-Za-z][A-Za-z0-9_]{0,63}`. Names are ASCII, ordinal, case-sensitive; whitespace/nesting/empty/unmatched braces are invalid; `{{` and `}}` are literals; repeated occurrences are allowed; compatibility compares distinct-name sets.

### BRULE-LOC-0012 - Text-only rendering

Values and formatted placeholders are encoded text and never interpreted as HTML, scripts, CSS, templates, URIs, or Angular expressions.

### BRULE-LOC-0013 - Version append

Every successful mutation appends exactly one immutable version and advances current/Tenant versions in the same transaction.

### BRULE-LOC-0014 - Undo is not restore

Undo applies only the current server-advertised logical predecessor, requires expected rowversion and target version, appends an Undo version, and never toggles the latest two chronological rows.

### BRULE-LOC-0015 - Restore is not undo

Restore Default appends RestoredDefault, retains the row inactive with null current value, and makes later PUT an update of that identity.

### BRULE-LOC-0016 - Concurrency

Rowversion protects update, Undo, and Restore; null expectedRowVersion is create-only and non-null is update-only; concurrent creates yield one success and one deterministic conflict.

### BRULE-LOC-0017 - No physical deletion

Overrides and version rows are retained through Restore, retirement, incompatibility, migration, and ordinary maintenance; all relationships restrict deletion.

### BRULE-LOC-0018 - Live tenant restriction

Only a live Active Tenant can receive Tenant-effective text or management. Cached values/JWT do not override current status; suspension after cache population prevents reuse.

### BRULE-LOC-0019 - Technical boundary

Localization never changes codes, ResourceKeys, permission names, status, type, validation, authorization, claims, or sensitive generic semantics.

### BRULE-LOC-0020 - Release compatibility

Incompatible overrides remain stored/history-visible but inactive for rendering; security incompatibility blocks Production and ordinary incompatibility requires reviewed acknowledgement.

### BRULE-LOC-0021 - Unicode preservation and measurement

Valid text is stored exactly without trimming or NFC/NFD/NFKC/NFKD. Limits count UTF-16 code units: PlainText 512, MultilineText 4000. Visually equivalent sequences may remain distinct.

### BRULE-LOC-0022 - Control characters

PlainText rejects CR, LF, TAB, NUL, and every control character. MultilineText permits CR/LF/TAB and preserves submitted line endings, but rejects NUL and all other prohibited controls.

### BRULE-LOC-0023 - Fingerprints

Placeholder fingerprint is SHA-256 over ordinally sorted distinct names LF-joined as UTF-8. Compatibility fingerprint is SHA-256 over canonical ResourceKey, TextFormat, SecurityClassification, TenantOverridable, and sorted names, each LF-delimited. Both persist as `binary(32)`.

### BRULE-LOC-0024 - Version types

CatalogSchemaVersion is a positive integer; CatalogVersion and TenantLocalizationVersion are positive Int64/SQL bigint; ResourceVersion is positive Int32; TenantOverrideVersion is positive Int64 within its aggregate.

### BRULE-LOC-0025 - Settings initialization

Settings starts at version 1. Migration creates one row per existing Tenant. Missing rows self-heal transactionally; concurrent insert loser reloads the unique TenantId row and retries.

### BRULE-LOC-0026 - Current/history ordering

Within one transaction, determine next version, insert immutable version, update current state/CurrentVersionNumber, increment TenantLocalizationVersion, commit, then evict local cache and dispatch. No cyclic CurrentVersionId FK exists.

### BRULE-LOC-0027 - Cache safety

Revalidate cached Tenant version at least every 15 seconds; expect healthy cross-instance visibility within 30 seconds; expire entries absolutely at five minutes; after failed SQL validation trust overrides for no more than 60 seconds since last success, then use system defaults.

### BRULE-LOC-0028 - Production compatibility and audit gates

Production startup refuses a local CatalogVersion lower than the highest activated database version. Production management refuses operation until immutable-audit persistence, retention, and readiness succeed.

### BRULE-LOC-0029 - Event and audit value boundary

Events carry identifiers/version numbers, not full text. Dispatch envelope metadata carries correlation/request/actor/trace. The post-commit audit projector obtains old/new text from committed immutable versions through protected access.

### BRULE-LOC-0030 - Retirement and replacement

Retired keys remain in lifecycle/history, accept no new overrides, stay out of ordinary groups, and transfer no wording; replacement always uses a new key.

### BRULE-LOC-0031 - HTTP rowversion validation

Localization HTTP accepts and emits only canonical padded RFC 4648 Base64 rowversions. Inputs are not trimmed and must decode to the exact SQL-rowversion length; blank, whitespace, Base64Url, hexadecimal, malformed, wrong-length, and noncanonical values are request-validation failures.

### BRULE-LOC-0032 - Effective HTTP authentication

Every Milestone 2 localization route is non-anonymous. Effective group and batch routes require an authenticated caller, trusted current Tenant, and live eligibility, while ordinary runtime effective resolution has no localization administrative permission requirement.

### BRULE-LOC-0033 - Concurrency transport boundary

Only a valid stale expected rowversion maps to HTTP 409 `concurrency.conflict`; the internal `Persistence.ConcurrencyConflict` error is not renamed. Invalid or missing required rowversions never map to concurrency conflict.

### BRULE-LOC-0034 - Audit readiness availability

Production immutable-audit readiness failure is HTTP 503 `localization.audit_readiness_unavailable`, not 403. It prevents mutation before Domain work, database state change, event, cache eviction, submitted-text logging, or disclosure of the internal reason.
