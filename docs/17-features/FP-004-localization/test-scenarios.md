---
document_id: FP-004-TS
title: Localization Test Scenarios
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Test Scenarios

## Catalog, Domain, and resolution

- **TS-LOC-0001:** accept one UTF-8-no-BOM JSON manifest/schema and byte-identical deterministic backend/client artifacts.
- **TS-LOC-0002:** reject YAML, BOM, duplicate/out-of-order/invalid/renamed/reused ResourceKeys.
- **TS-LOC-0003:** validate complete Active metadata/defaults and explicitly incomplete non-Production handling.
- **TS-LOC-0004:** derive `en/ltr`, `ar/rtl`; reject unsupported and Tenant-controlled direction.
- **TS-LOC-0005:** create independent compatible English/Arabic overrides.
- **TS-LOC-0006:** reject unknown/retired/incompatible/non-overridable/security-sensitive mutations.
- **TS-LOC-0007:** enforce format-specific controls and 512/4000 UTF-16 limits.
- **TS-LOC-0008:** reject executable/markup/template content pathways and encode output as text.
- **TS-LOC-0009:** accept Arabic placeholder reordering and repeated valid occurrences.
- **TS-LOC-0010:** reject whitespace, empty, nested, unmatched, invalid-name, missing, added, and wrong-case placeholders.
- **TS-LOC-0011:** format encoded values once after resolution and never reparse them.
- **TS-LOC-0012:** resolve compatible active Tenant override with complete metadata.
- **TS-LOC-0013:** resolve requested default and observe compatible wording-only catalog change.
- **TS-LOC-0014:** exercise English and neutral fallback; ordinary output never contains ResourceKey.
- **TS-LOC-0015:** resolve deterministic single/bounded batch; reject duplicate/oversize and preserve cancellation.
- **TS-LOC-0016:** prove both culture precedences, unsupported fallthrough, switching, Tenant clearing, and formatting independence.

## History and concurrency

- **TS-LOC-0017:** append immutable Created/Updated versions with monotonic numbers, metadata, lineage, and fingerprints.
- **TS-LOC-0018:** Undo the advertised eligible predecessor and append Undo.
- **TS-LOC-0019:** repeated Undo walks explicit lineage rather than alternating chronological rows.
- **TS-LOC-0020:** reject no-predecessor, wrong/incompatible target, wrong tuple, and stale Undo.
- **TS-LOC-0021:** Restore retains inactive aggregate/null value and appends RestoredDefault.
- **TS-LOC-0022:** reapply after restore updates same identity with rowversion.
- **TS-LOC-0023:** race all mutation pairs; exactly one rowversion writer commits.
- **TS-LOC-0024:** prove no command/repository/cascade physically deletes aggregate/history.

## Authorization, security, architecture

- **TS-LOC-0025:** ordinary authenticated user resolves runtime text but receives 403 for management.
- **TS-LOC-0026:** authorized admin sees only trusted current Tenant; forged TenantId never changes scope.
- **TS-LOC-0027:** Provisioning/Suspended/Archived/missing Tenant denies management/effective override despite valid JWT.
- **TS-LOC-0028:** preserve 401/403/code/type/status while only safe display fields localize.
- **TS-LOC-0029:** all protected FP-002 causes retain one generic non-enumerating resource in both cultures.
- **TS-LOC-0030:** exact permission codes are code-catalog capabilities with no extra authority.
- **TS-LOC-0031:** real SQL migration verifies types/checks/indexes/BIN2/rowversion/restricted FKs/delete guards.
- **TS-LOC-0032:** mutation atomically advances current/history/Tenant version; eviction/events occur post-commit.
- **TS-LOC-0033:** two process caches observe changes within healthy bounds without contamination.
- **TS-LOC-0034:** release validation covers change/retirement/replacement/incompatibility/rollback.
- **TS-LOC-0035:** scan events/logs/errors/cache diagnostics for forbidden text/secrets/cross-Tenant values.
- **TS-LOC-0036:** architecture/scope and full solution gates enforce M1 boundaries.

## Focused parser, version, settings, and SQL Server tests

- **TS-LOC-0037:** validate `{{`/`}}` examples and literal-brace fingerprint exclusion.
- **TS-LOC-0038:** verify ordinal distinct-name sorting, LF/UTF-8/SHA-256 placeholder fingerprint and `binary(32)`.
- **TS-LOC-0039:** verify compatibility canonical fields, wording stability, policy-change invalidation, and `binary(32)`.
- **TS-LOC-0040:** validate positive CatalogSchemaVersion/ResourceVersion Int32 and Catalog/Tenant/override Int64 boundaries.
- **TS-LOC-0041:** bootstrap settings for every existing Tenant during migration.
- **TS-LOC-0042:** self-heal a missing settings row transactionally at version 1.
- **TS-LOC-0043:** race first settings creation; duplicate-key loser reloads/retries and one row remains.
- **TS-LOC-0044:** enforce unique settings Tenant ownership and restricted Tenant deletion.
- **TS-LOC-0045:** enforce aggregate tuple uniqueness while active.
- **TS-LOC-0046:** enforce the same tuple uniqueness while inactive/restored.
- **TS-LOC-0047:** verify current-state/history version and tuple coherence.
- **TS-LOC-0048:** verify monotonic TenantOverrideVersion under sequential/racing operations.
- **TS-LOC-0049:** real SQL concurrent update produces one winner/conflict.
- **TS-LOC-0050:** real SQL concurrent Undo produces one winner/conflict and valid lineage.
- **TS-LOC-0051:** real SQL concurrent Restore produces one winner/conflict and one inactive row.
- **TS-LOC-0052:** enforce Undo lineage ownership within aggregate/Tenant/culture.
- **TS-LOC-0053:** PlainText surrogate-pair boundary at 512 UTF-16 units accepts/rejects exactly.
- **TS-LOC-0054:** Multiline surrogate-pair boundary at 4000 UTF-16 units accepts/rejects exactly.
- **TS-LOC-0055:** every successful mutation increments TenantLocalizationVersion once; failures do not.
- **TS-LOC-0056:** SQL bigint CatalogVersion/highest-activation comparison handles Int64 boundaries.
- **TS-LOC-0057:** apply localization migration over prior schema/data.
- **TS-LOC-0058:** downgrade to previous migration under approved test conditions.
- **TS-LOC-0059:** reapply migration and verify equivalent schema/bootstrap.
- **TS-LOC-0060:** reject physical/restricted deletion and preserve all retained history.
- **TS-LOC-0061:** insert aggregate/version/current pointer with constraints enabled and no cyclic FK.

## Focused authorization and cache tests

- **TS-LOC-0062:** View positive for list/get/effective and negative for mutations/history.
- **TS-LOC-0063:** Manage positive for PUT/Preview/Undo/Restore and negative without Manage.
- **TS-LOC-0064:** ViewHistory positive for history and history denied without it.
- **TS-LOC-0065:** anonymous management denied; only approved public system-default groups accessible.
- **TS-LOC-0066:** private Tenant-effective groups denied before trusted Tenant selection.
- **TS-LOC-0067:** inactive Tenant denied for every operation and safe not-found prevents cross-Tenant disclosure.
- **TS-LOC-0068:** suspend Tenant after cache population; next path cannot use override.
- **TS-LOC-0069:** reject unknown/forged TenantId across route/query/header/body and strict DTOs.
- **TS-LOC-0070:** Preview performs no persistence/version/event/shared-cache/logged text and rejects non-overridable resources.

## Dedicated Milestone 2 route tests

- **TS-LOC-0071:** GET resources verifies exact path/method/auth/View/Tenant/live/filter/paging/schema/status/errors/isolation/OpenAPI.
- **TS-LOC-0072:** GET resource verifies exact path/method/auth/View/Tenant/live/safe-not-found/projection/errors/OpenAPI.
- **TS-LOC-0073:** PUT override verifies exact path/auth/Manage/Tenant/live/strict schema/limits/create-update conflicts/projection/OpenAPI.
- **TS-LOC-0074:** POST Undo verifies exact path/auth/Manage/Tenant/live/strict lineage/rowversion/exact 409/422 mappings/OpenAPI.
- **TS-LOC-0075:** POST restore-default verifies exact path/auth/Manage/Tenant/live/strict rowversion/retention/errors/OpenAPI.
- **TS-LOC-0076:** GET history verifies exact path/auth/ViewHistory/Tenant/live/bounded stable paging/safe projection/OpenAPI.
- **TS-LOC-0077:** POST preview verifies exact path/auth/Manage/Tenant/live/strict validation/no side effects/safe output/OpenAPI.
- **TS-LOC-0078:** GET effective verifies exact path/auth/View/Tenant/live/bounds/culture metadata/projection/errors/OpenAPI.
- **TS-LOC-0079:** POST effective/batch verifies slash path/auth/View/Tenant/live/strict unique bounded keys/projection/errors/OpenAPI.

## Cache, rollback, audit, and remaining focused gates

- **TS-LOC-0080:** revalidate each cached Tenant version no later than 15 seconds.
- **TS-LOC-0081:** healthy two-instance mutation becomes visible within expected 30-second maximum.
- **TS-LOC-0082:** expire process-local entry at five-minute absolute lifetime.
- **TS-LOC-0083:** failed SQL validation serves override no later than 60 seconds from last success, then defaults.
- **TS-LOC-0084:** degraded fallback emits health/telemetry but no text/key in ordinary output and no cross-Tenant substitution.
- **TS-LOC-0085:** mutation never reports success before commit and failed commit emits no eviction/event/version.
- **TS-LOC-0086:** Production startup accepts equal/higher local CatalogVersion and records monotonic activation.
- **TS-LOC-0087:** Production startup refuses lower local version and does not lower stored activation.
- **TS-LOC-0088:** reviewed rollback preserves schema/history/incompatible rows and rejects ordinary unprepared rollback.
- **TS-LOC-0089:** Production management refuses when audit persistence is absent.
- **TS-LOC-0090:** Production management refuses when retention/readiness is absent/unhealthy while read-only effective resolution succeeds.
- **TS-LOC-0091:** audit projector reads committed prior/new immutable versions, including Restore inactive state.
- **TS-LOC-0092:** Domain event payload excludes full text; dispatch metadata carries correlation/request/actor/trace.
- **TS-LOC-0093:** resource retirement excludes ordinary groups but preserves authorized history and blocks new overrides.
- **TS-LOC-0094:** replacement key receives no automatic wording/history transfer.
- **TS-LOC-0095:** preserve Unicode without normalization/trim and distinguish visually equivalent sequences.
- **TS-LOC-0096:** PlainText rejects every control; Multiline permits only CR/LF/TAB and preserves line endings.
- **TS-LOC-0097:** text culture change does not infer timezone/currency/date/number context.
- **TS-LOC-0098:** validate all 32 decisions, 28 conflicts, identifiers, references, links, and exact document metadata.
- **TS-LOC-0099:** Milestone 2 ProblemDetails/OpenAPI preserve technical semantics, generic security behavior, and useful requested/resolved culture.
