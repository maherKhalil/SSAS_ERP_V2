---
document_id: FP-004-DATA
title: Localization Data Model
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Data Model

## Ownership

Localization persistence belongs to Platform `PlatformDbContext`. Tenant settings/overrides/versions implement Tenant ownership and follow centralized filters/write assignment, restricted relationships, audit metadata, rowversion, and real SQL Server tests. SQL never contains a mutable system-default resource-definition catalog.

## `platform.TenantLocalizationSettings`

| Column | SQL | Rules |
|---|---|---|
| TenantId | uniqueidentifier PK/FK | one per Tenant; restricted delete |
| TenantDefaultCulture | varchar(2) | CHECK `en|ar`, ordinal semantics |
| TenantLocalizationVersion | bigint | positive, initialized 1 |
| CreatedUtc/ModifiedUtc | datetimeoffset | UTC |
| CreatedBy/ModifiedBy | nvarchar(repository standard) nullable | existing audit convention |
| RowVersion | rowversion | concurrency |

Migration inserts one settings row/version 1 for every existing Tenant. The runtime initializer attempts insert inside the mutation transaction; unique TenantId selects one winner, duplicate-key loser clears/reloads and retries using the existing row. Settings initialization and localization-version increment are atomic. Future Tenant provisioning creates the row in its approved workflow; missing-row self-heal remains defensive.

## `platform.TenantLocalizationOverrides`

| Column | SQL | Rules |
|---|---|---|
| TenantLocalizationOverrideId | uniqueidentifier PK | immutable aggregate identity |
| TenantId | uniqueidentifier | trusted ownership; indexed/restricted FK |
| ResourceKey | nvarchar(200) BIN2 | ordinal, immutable |
| Culture | varchar(2) BIN2 | `en|ar`, immutable |
| TextFormat | varchar(24) BIN2 | immutable value format for the stored override |
| CurrentPlainTextValue | nvarchar(512) nullable | logical CurrentValue when PlainText |
| CurrentMultilineTextValue | nvarchar(4000) nullable | logical CurrentValue when MultilineText |
| IsActive | bit | retained after restore |
| CurrentVersionNumber | bigint | positive logical reference |
| CatalogVersion | bigint | positive |
| CompatibilityFingerprint | binary(32) | approved SHA-256 |
| CreatedUtc/ModifiedUtc, CreatedBy/ModifiedBy | repository audit types | UTC/actor |
| RowVersion | rowversion | optimistic concurrency |

Unique `(TenantId, ResourceKey, Culture)` applies regardless of IsActive. A CHECK requires exactly the format-matching value column when active and both value columns null when inactive; another CHECK restricts TextFormat and positive versions. Domain maps the two physical columns to one logical CurrentValue. UTF-16 code-unit semantics align .NET `string.Length` and nvarchar length; tests cover a surrogate pair straddling each boundary.

## `platform.TenantLocalizationOverrideVersions`

| Column | SQL | Rules |
|---|---|---|
| TenantLocalizationOverrideVersionId | uniqueidentifier PK | immutable row identity |
| TenantLocalizationOverrideId | uniqueidentifier | restricted FK to aggregate |
| TenantId | uniqueidentifier | ownership/index, tuple coherence |
| ResourceKey | nvarchar(200) BIN2 | immutable copy for protected projection |
| Culture | varchar(2) BIN2 | `en|ar` |
| VersionNumber | bigint | positive TenantOverrideVersion |
| TextFormat | varchar(24) BIN2 | format at this immutable version |
| PlainTextValue | nvarchar(512) nullable | logical Value when PlainText |
| MultilineTextValue | nvarchar(4000) nullable | logical Value when MultilineText |
| ChangeType | varchar(32) BIN2 | Created/Updated/Undone/RestoredDefault |
| PriorLogicalVersionNumber | bigint nullable | explicit lineage |
| UndoTargetVersionNumber | bigint nullable | target used by Undo |
| CatalogVersion | bigint | positive |
| CompatibilityFingerprint | binary(32) | exact release compatibility |
| OccurredUtc/ActorId | datetimeoffset / repository actor type | immutable audit source metadata |

Unique `(TenantLocalizationOverrideId, VersionNumber)`. A CHECK requires exactly one format-matching value except RestoredDefault, which requires both null. Composite ownership constraints/indexes prevent a version from attaching across aggregate/Tenant/tuple. Values and metadata are immutable; DbContext guards modification/deletion and all FKs restrict cascades.

## Catalog activation state

One Platform-owned singleton/row stores `HighestActivatedCatalogVersion bigint` and rowversion. Successful Production activation raises it monotonically; startup with lower local CatalogVersion fails. It is never lowered automatically and does not store definitions/defaults.

## Non-cyclic insertion and transaction

There is no CurrentVersionId or composite FK from current state to versions. The sole physical relationship is the version's restricted FK to its aggregate. For create: initialize/reload settings, insert the aggregate with CurrentVersionNumber 1, insert version 1 referencing it, increment settings, and commit. For an existing mutation: validate rowversion/lineage, insert the next version, update current fields/CurrentVersionNumber, increment settings, and commit. Application invariants plus the unique aggregate/version constraint require CurrentVersionNumber to resolve to exactly one committed version; SQL tests prove coherence and insertion with constraints enabled. Post-commit eviction/dispatch occurs only after commit.

## Fingerprints and text

PlaceholderFingerprint belongs to generated catalog artifacts and, when persisted for validation/versions, is `binary(32)`. CompatibilityFingerprint is `binary(32)`. CatalogVersion/TenantLocalizationVersion/TenantOverrideVersion are bigint; ResourceVersion is Int32 in artifacts, not a mutable SQL catalog row. Text preserves valid Unicode/whitespace/line endings; no normalization/trim. PlainText rejects all controls. Multiline permits CR/LF/TAB only among controls.

## Migrations, deletion, rollback

Migration upgrade creates settings, overrides, versions, activation state, checks/indexes/restricted FKs and bootstraps existing Tenants. Automated SQL Server tests apply upgrade, downgrade to the previous migration, and reapply, verifying no data/constraint drift. Production deployment uses reviewed scripts; no unsafe automatic schema mutation. Ordinary rollback to lower CatalogVersion is blocked; approved recovery preserves all columns/history. No physical-delete operation or cascade exists for override/version history.
