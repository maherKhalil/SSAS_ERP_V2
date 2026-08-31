---
document_id: FP-004-AC
title: Localization Acceptance Criteria
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Acceptance Criteria

### AC-LOC-0001 - An Active resource is accepted in Production only with both `en` and `ar`
Production validation accepts an Active resource only with both `en` and `ar`; incomplete non-Production output is flagged, diagnoses culture, uses English fallback, and is not promotable.
### AC-LOC-0002 - Immutable key boundary
Manifest validation rejects duplicate/reordered, invalid, renamed, or reused retired ResourceKeys and generates byte-deterministic artifacts.
### AC-LOC-0003 - Independent tenant overrides
An eligible Tenant can maintain compatible `en` and `ar` overrides independently without changing defaults.
### AC-LOC-0004 - Tenant isolation
Every read/write/history/cache path derives TenantId from trusted context and returns no other Tenant's state.
### AC-LOC-0005 - Effective fallback
The four-step chain reports exact source/cultures and neutral Production output never exposes ResourceKey.
### AC-LOC-0006 - Default changes flow through
A wording-only default release changes unoverridden/restored results and preserves compatible overrides.
### AC-LOC-0007 - Placeholder reorder is accepted; missing, unknown or malformed placeholders are rejected
Exact parser/escaping/repetition/case/set rules accept reorder and reject every missing/unknown/malformed placeholder.
### AC-LOC-0008 - Text safety and limits
PlainText/MultilineText enforce exact control and 512/4000 UTF-16 limits before persistence and use format-matching `nvarchar(512)`/`nvarchar(4000)` columns while preserving valid text.
### AC-LOC-0009 - Generic authentication remains generic
All protected causes retain one generic code/ResourceKey and cannot be Tenant-overridden.
### AC-LOC-0010 - Technical identifiers unchanged
Localization changes no code, key, status/type, authorization, validation, claim, permission, or control flow.
### AC-LOC-0011 - Immutable versions
Each mutation appends one unmodifiable, uniquely numbered version and atomically advances current/settings versions.
### AC-LOC-0012 - Deterministic undo
Only the advertised compatible lineage predecessor plus matching rowversion succeeds; repeated Undo walks explicit lineage.
### AC-LOC-0013 - Restore keeps one inactive row, nulls the value, appends `RestoredDefault`, and resolves the default
Restore retains one inactive row, null current value, appends RestoredDefault, and resolves current default.
### AC-LOC-0014 - Reapply after restore
A later PUT requires expected rowversion and reactivates the same aggregate identity.
### AC-LOC-0015 - Concurrency
Competing writes yield one committed winner; losers receive deterministic conflict without extra version/stamp/event.
### AC-LOC-0016 - Language precedence and independence
Anonymous/authenticated precedence is exact, unsupported values fall through, switch needs no logout, and formatting is independent.
### AC-LOC-0017 - Authorization
View, Manage, and ViewHistory grant only their exact operations under trusted live Active Tenant scope.
### AC-LOC-0018 - Safe management contracts
Strict DTOs reject unknown/TenantId fields and bounded projections disclose no foreign state.
### AC-LOC-0019 - Revalidation and eviction observe their bounds and never cross tenant or culture
Version revalidation/eviction observes 15s/30s/5m/60s bounds and never crosses Tenant/culture.
### AC-LOC-0020 - Release compatibility
Validation reports incompatible retained overrides, blocks sensitive incompatibility, and requires ordinary review.
### AC-LOC-0021 - Safe events and audit dependency
Events exclude full text; dispatch metadata supplies context; audit projector reads committed versions.
### AC-LOC-0022 - Framework boundaries
Domain/Application remain provider/framework-neutral and bounded.
### AC-LOC-0023 - Angular contract
M1 generates neutral client JSON only; no Angular runtime/library/screen is introduced.
### AC-LOC-0024 - Focused milestone
M1 contains only approved backend core/migration/tests; HTTP/OpenAPI stay M2 and deferred features remain absent.
### AC-LOC-0025 - JSON catalog authority
Only one UTF-8-no-BOM JSON manifest/schema is authoritative; YAML and mutable SQL defaults are rejected.
### AC-LOC-0026 - Settings bootstrap
Migration creates one version-1 settings row for each existing Tenant.
### AC-LOC-0027 - Settings self-heal
A missing settings row initializes transactionally; concurrent first writes converge on one row and retry safely.
### AC-LOC-0028 - Security classification
Only Ordinary and SecuritySensitiveNonOverridable exist; non-overridable Preview/mutation fails.
### AC-LOC-0029 - Placeholder fingerprint
Canonical examples produce deterministic SHA-256 and exactly 32 persisted bytes.
### AC-LOC-0030 - Version types
Schema/catalog/resource/Tenant/override versions use approved positive Int32/Int64/JSON integer/SQL bigint types and scopes.
### AC-LOC-0031 - Compatibility fingerprint
Canonical policy input produces deterministic SHA-256; wording-only changes do not alter it.
### AC-LOC-0032 - No cyclic relationship
Aggregate/version creation succeeds with enforced constraints and no CurrentVersionId cycle.
### AC-LOC-0033 - PUT create-only
Null expectedRowVersion creates only when absent; existing/inactive returns 409 `localization.override_already_exists`.
### AC-LOC-0034 - PUT update-only
Non-null expectedRowVersion updates matching existing row; missing/stale return exact 409 codes.
### AC-LOC-0035 - Undo errors
Stale/no-target/wrong-target/incompatible map exactly to approved 409/409/422/422 codes.
### AC-LOC-0036 - Preview behavior
Manage-only Preview validates fully yet writes/caches/emits/logs nothing and returns encoded text only.
### AC-LOC-0037 - Live status after caching
Suspension after cache population prevents Tenant override use and management on the next authorized path.
### AC-LOC-0038 - Rollback preflight
Production startup refuses local CatalogVersion below highest activated and never lowers database state.
### AC-LOC-0039 - Immutable audit gate
Production management refuses when audit persistence/retention/readiness is absent; effective reads continue.
### AC-LOC-0040 - Audit old/new sourcing
Protected projector obtains prior/new text from immutable committed versions, including restored inactive state.
### AC-LOC-0041 - Retirement
Retired keys stay in history, cannot receive overrides, leave ordinary groups, are never reused, and transfer nothing.
### AC-LOC-0042 - Route: list resources
GET `/resources` enforces auth/View/current live Tenant/strict bounded filters/paging/safe raw-template projection/codes/cross-Tenant denial and exact OpenAPI; placeholder-bearing templates require no interpolation values.
### AC-LOC-0043 - Route: get resource
GET `/resources/{resourceKey}` enforces auth/View/current live Tenant/safe not-found/raw-template projection/codes and exact OpenAPI; placeholder-bearing templates require no interpolation values.
### AC-LOC-0044 - Route: PUT override
PUT override enforces auth/Manage/current live Tenant/strict body/UTF-16/parser/security/create-update semantics/statuses/codes and OpenAPI.
### AC-LOC-0045 - Route: Undo
POST Undo enforces auth/Manage/current live Tenant/strict target+rowversion/lineage/statuses/codes/projection and OpenAPI.
### AC-LOC-0046 - Route: Restore Default
POST restore-default enforces auth/Manage/current live Tenant/strict rowversion/retention/statuses/codes/projection and OpenAPI.
### AC-LOC-0047 - Route: history
GET history enforces auth/ViewHistory/current live Tenant/bounded stable paging/safe not-found/projection and OpenAPI.
### AC-LOC-0048 - Route: Preview
POST preview enforces auth/Manage/current live Tenant/strict schema/no side effects/safe output/statuses/codes and OpenAPI.
### AC-LOC-0049 - Route: effective
GET effective enforces authentication/trusted live Tenant/bounded selectors/culture metadata/raw effective-template projection/codes and OpenAPI; it performs no placeholder interpolation and ordinary runtime resolution does not require View.
### AC-LOC-0050 - Route: effective batch
POST `/effective/batch` enforces authentication/trusted live Tenant/strict unique bounded keys/culture/optional resource-scoped plain-string placeholder values/projection/codes and OpenAPI; malformed or unrequested maps fail request validation, missing/unknown placeholders fail policy validation, and ordinary runtime resolution does not require View.
### AC-LOC-0051 - Permission positives
Each exact permission succeeds only for its documented operations.
### AC-LOC-0052 - Permission negatives
Missing/wrong permission, ordinary user, anonymous management, Preview/Undo/Restore without Manage, and history without ViewHistory are denied.
### AC-LOC-0053 - Public/private boundary
Milestone 2 exposes no anonymous localization HTTP route. Effective HTTP requires trusted Tenant selection; future public system-default HTTP groups require an explicitly approved contract.
### AC-LOC-0054 - No writable TenantId
Every future route rejects unknown TenantId and no input channel can alter current scope.
### AC-LOC-0055 - SQL uniqueness/coherence
SQL enforces settings/tuple/inactive/version uniqueness, tuple ownership, current/history coherence, monotonic versions, and restricted deletion.
### AC-LOC-0056 - SQL concurrency
Real SQL Server proves concurrent create/update/Undo/Restore/settings initialization yield deterministic single winners.
### AC-LOC-0057 - Migration lifecycle
Real SQL Server upgrade, downgrade, and reapply preserve approved schema/data behavior.
### AC-LOC-0058 - Surrogate boundaries
Values at and above 512/4000 UTF-16 limits, including boundary surrogate pairs, behave identically in Domain/API/SQL.
### AC-LOC-0059 - Cache degradation
After 60 seconds from last successful SQL validation, Tenant overrides are excluded and health/telemetry reports degradation.
### AC-LOC-0060 - Formatting context
Changing text culture alone changes neither timezone nor currency/number/date context.
### AC-LOC-0061 - HTTP rowversion contract
Localization HTTP accepts only canonical padded RFC 4648 Base64 rowversions of exact SQL-rowversion length, emits canonical Base64, rejects Base64Url/hex/blank/whitespace/malformed/wrong-length/noncanonical values with HTTP 400 `localization.rowversion_invalid`, and maps only valid stale values to 409 `concurrency.conflict`.
### AC-LOC-0062 - Effective HTTP authentication boundary
All nine M2 routes are non-anonymous. Effective group and batch reject anonymous and untrusted-Tenant callers, permit an Active trusted Tenant without View for ordinary runtime resolution, provide no Tenant override for non-Active Tenant, and expose no arbitrary public catalog group.
### AC-LOC-0063 - Concurrency transport mapping
`Persistence.ConcurrencyConflict` remains the internal result and localization HTTP returns HTTP 409 `concurrency.conflict`; malformed or missing required rowversions are request validation, not concurrency.
### AC-LOC-0064 - Audit readiness availability
An otherwise authorized Production localization mutation proceeds only when audit readiness succeeds; otherwise it returns HTTP 503 `localization.audit_readiness_unavailable` with no SQL state change, Domain event, cache eviction, submitted-text logging, or internal-cause disclosure.
