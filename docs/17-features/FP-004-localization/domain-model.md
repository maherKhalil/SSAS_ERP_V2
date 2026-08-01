---
document_id: FP-004-DOM
title: Localization Domain Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Domain Model

## Ownership and source model

Platform owns Tenant localization settings and overrides. Module owners contribute entries to one authoritative manifest proposed at `src/BuildingBlocks/SSAS.BuildingBlocks.Localization/Catalog/localization-catalog.json`; a versioned JSON Schema sits beside it. The file is JSON only, UTF-8 without BOM, ordinally ordered by ResourceKey, duplicate-free, and generates deterministic backend plus neutral-client artifacts. SQL is not the system-default authority.

## Catalog model

`LocalizationCatalog(CatalogSchemaVersion:int, CatalogVersion:long, Resources)` is immutable in process. Each `LocalizationResourceDefinition` contains ResourceKey, lifecycle (`Active|Retired`), `en` and `ar` defaults, TextFormat, SecurityClassification, TenantOverridable, distinct PlaceholderNames, ResourceVersion:int, PlaceholderFingerprint, and CompatibilityFingerprint.

SecurityClassification is exactly `Ordinary` or `SecuritySensitiveNonOverridable`. TextFormat is exactly `PlainText` or `MultilineText`. Direction is derived from Culture only.

### Version concepts

| Concept | Exact type and scope |
|---|---|
| CatalogSchemaVersion | positive Int32; manifest structure only |
| CatalogVersion | positive monotonic Int64; JSON integer/SQL bigint; every released catalog change |
| ResourceVersion | positive Int32 per ResourceKey; defaults/metadata/lifecycle/placeholders/format/security change |
| TenantLocalizationVersion | positive Int64 per Tenant; initialized 1 and incremented after each mutation |
| TenantOverrideVersion | positive Int64 within one Tenant/ResourceKey/Culture aggregate |

## Value objects

- `CultureCode`: ordinal `en|ar`; exposes derived `ltr|rtl`.
- `ResourceKey`: immutable ordinal module-owned key.
- `LocalizedText`: preserves submitted Unicode/whitespace/line endings; validates TextFormat control rules and UTF-16 code-unit limit.
- `PlaceholderSet`: parses `{name}`, literals `{{`/`}}`, exact ASCII grammar `[A-Za-z][A-Za-z0-9_]{0,63}`, and distinct ordinal names.
- `PlaceholderFingerprint`: SHA-256 of ordinal-sorted distinct names joined by LF and UTF-8 encoded.
- `CompatibilityFingerprint`: SHA-256 of canonical ResourceKey, TextFormat, SecurityClassification, TenantOverridable, and ordinal-sorted names, LF-delimited and UTF-8 encoded.
- `FormattingContext`: optional typed timezone/date culture/number culture/currency culture/currency code; independent of text culture and not persisted in Milestone 1.

Examples: `Hello {userName}` parses `{userName}`; `{{total}} = {amount}` renders literal `{total}` plus placeholder `amount`; `{ name }`, `{}`, `{a-b}`, `{A` and `A}` are invalid. For `{z}{a}{z}`, canonical placeholder input is `a\nz` before UTF-8/SHA-256.

## TenantLocalizationSettings

Localization-owned settings is keyed uniquely by TenantId and contains TenantDefaultCulture and TenantLocalizationVersion. Existing Tenants are migration-bootstrapped; a missing row is transactionally initialized at version 1. Concurrent first writers race on unique TenantId: one insert succeeds; the duplicate-key loser reloads and retries. New-Tenant provisioning must later create the row in the approved provisioning workflow. UserPreferredCulture belongs to a future profile/settings boundary.

## TenantLocalizationOverride aggregate

Identity and immutable tuple: TenantLocalizationOverrideId, TenantId, ResourceKey, Culture. Current state: nullable CurrentValue, IsActive, CurrentVersionNumber, CatalogVersion, CompatibilityFingerprint, audit metadata, RowVersion. One row exists per tuple whether active or restored.

Operations are Create, Update, Undo, RestoreDefault, and reactivation via Update. Create requires absence; update requires existence and expected rowversion. All operations validate live Tenant, catalog editability, culture, exact length/control/placeholder rules, and compatibility.

## Immutable version

Each version contains aggregate identity, TenantId, ResourceKey, Culture, VersionNumber, nullable Value, ChangeType (`Created|Updated|Undone|RestoredDefault`), PriorLogicalVersionNumber, optional UndoTargetVersionNumber, CatalogVersion, CompatibilityFingerprint, actor and UTC time. Aggregate identity plus VersionNumber is unique.

Undo uses the server-advertised eligible lineage predecessor, never arbitrary or merely chronological selection. Restore appends an inactive/default state. The only physical relationship is the version's restricted FK to its aggregate; CurrentVersionNumber is a transactionally maintained logical reference with no FK back to versions. Create inserts aggregate then version; existing mutations insert the next version then update current state; both increment settings and commit atomically.

## Resolution result

`EffectiveLocalizedText` returns ResourceKey, Value, RequestedCulture, ResolvedCulture, Direction, ResolutionSource (`TenantOverride|SystemDefault|EnglishFallback|KeyFallback`), CatalogVersion, ResourceVersion, and applicable TenantLocalizationVersion. KeyFallback uses `Text unavailable` or `النص غير متاح` without exposing ResourceKey.

## Events and audit

Past-tense events identify EventId, TenantId, aggregate/key/culture, ChangeType, prior/current version numbers, CatalogVersion, and OccurredAt; they exclude full text. Existing dispatch metadata carries correlation/request/actor/trace. After commit, the protected audit projector reads old/new values from immutable versions. This is the approved FP-004 interpretation of ADR-009; a future ADR wording cleanup should distinguish payload from dispatch envelope.

## Service/repository boundaries

Domain/Application define catalog validator, parser/fingerprint services, aggregate repository, settings initializer, history projection, resolver, version reader, compatibility preflight, cache abstraction, and audit-readiness abstraction. Contracts are async/cancellation-aware where I/O occurs, bounded, projection-based, and expose neither EF, HTTP, cache provider types, entities, nor `IQueryable`.
