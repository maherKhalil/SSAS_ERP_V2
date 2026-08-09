---
id: ADR-013
title: Primary Key & Identifier Strategy
category: Architecture Decision Record
version: 1.0
status: Accepted
date: 2026-08-09
owner: Solution Architecture Team
tags:
  - persistence
  - identifiers
  - primary-key
  - guid
  - multi-tenancy
  - architecture
depends_on:
  - ADR-002
  - ADR-005
  - ADR-008
  - ADR-010
used_by:
  - Platform
  - HR
  - GL
---

# ADR-013: Primary Key & Identifier Strategy

---

# Status

**Accepted**

This ADR is accepted alongside the FP-005 documentation package. It codifies an existing implemented practice; it does not change any existing identifier.

---

# Context

The persistence constraint `CON-0201` states:

> Primary Keys shall use BIGINT Identity unless otherwise approved.

The implemented Platform module already contains **both** `BIGINT IDENTITY` keys and `Guid` keys:

| Key type | Implemented aggregates / entities |
|---|---|
| `BIGINT` (`long`) | `Identity`, `TenantUser`, `Role`, `TenantUserRoleAssignment`, `RolePermissionAssignment`, `AuthenticationAccount`, `AuthenticationSession`, `RefreshTokenRecord`, `AccountActionToken`, `TenantSelectionTransaction` |
| `Guid` | `Tenant` (`TenantId`), `TenantLocalizationSettings`, `TenantLocalizationOverride`, `TenantLocalizationOverrideVersion` |
| `byte` | `LocalizationCatalogState` (bounded singleton) |
| Cross-cutting isolation column | `ITenantOwnedEntity.TenantId` is always `Guid`, independent of the row's own primary key |

The `Guid` usages are **not** violations of `CON-0201`. They are the "*unless otherwise approved*" exceptions that `CON-0201` explicitly permits, each approved by a feature package:

- `TenantId` is fixed as a server-generated, immutable, never-reused `Guid` by **FP-003 `DEC-TEN-0001`**.
- The localization aggregates use `Guid` surrogate keys under the approved **FP-004** decisions.

What was missing was a single, project-wide rule that states *when* `BIGINT IDENTITY` applies and *when* a `Guid` is the approved choice. Without that rule, each new aggregate — beginning with **FP-005 `Company`** and continuing into HR and GL — would re-litigate the same question and risk inconsistent cross-module identifiers.

Related decisions: `ADR-002` (SQL Server), `ADR-005` (shared-schema multi-tenancy with a mandatory `Guid TenantId`), `ADR-008` (EF Core, optimistic concurrency, restricted deletes), `ADR-010` (aggregate-specific repositories).

---

# Problem Statement

A decision is required because:

- `CON-0201` reads as a `BIGINT`-only default, yet approved `Guid` identifiers already exist, which appears contradictory to readers and to AI-assisted code generation.
- The next feature (FP-005 `Company`) introduces `CompanyId`, which must be referenced by **future HR and GL** records across module boundaries; its type is a one-way-door decision.
- Choosing key types ad hoc per aggregate would produce inconsistent join types, unstable cross-module contracts, and avoidable rework.

Desired outcome: one explicit rule that reconciles `CON-0201` with the approved `Guid` identifiers, decides `CompanyId`, and gives clustered-index guidance — **without changing any existing identifier**.

---

# Decision

Adopt the following project-wide primary key and identifier strategy.

1. **Default — `BIGINT IDENTITY`.** Aggregate roots, child rows, and history rows use a `BIGINT IDENTITY` surrogate primary key by default, per `CON-0201`.
2. **`Guid` is an approved exception** for identifiers that meet at least one of the qualifying conditions below. `Guid` identifiers are legitimate under the `CON-0201` "*unless otherwise approved*" clause; this ADR is the standing approval and the qualifying test.
3. **`CompanyId` is a `Guid`** because it is both a cross-cutting isolation identifier beneath the tenant and a cross-module identifier referenced by HR and GL.
4. **No existing identifier changes.** This ADR is descriptive of the established `BIGINT`/`Guid` split and prescriptive only for **new** identifiers.

---

# Default BIGINT rule

Use `BIGINT IDENTITY` when the identifier is:

- an internal aggregate or storage identifier consumed only inside its owning module;
- a child-entity or immutable-history-row identifier;
- a high-volume internal storage key where a stable, non-enumerable, cross-boundary identity is unnecessary.

`BIGINT IDENTITY` is the clustered primary key in these cases. It is compact, monotonically increasing (append-friendly for clustered indexes), and join-efficient.

---

# Guid exception rule

Use a `Guid` only when the identifier meets at least one of these conditions:

1. it is a **cross-cutting isolation identifier** carried on many tables and/or in security tokens (for example `TenantId`, and now `CompanyId`);
2. it is **explicitly published through an established module boundary** — a public contract, an integration contract or event, or an approved cross-module relationship (such as a foreign key another module references);
3. it is **required to be externally stable and non-enumerable**, where sequential enumeration would be an information-disclosure or correlation risk;
4. it is **explicitly approved** by a Feature Package or ADR and recorded in that feature's `decisions-approved.md`.

The mere *possibility* that an identifier might someday be referenced by another module is **not** sufficient to choose `Guid`. Almost any aggregate could hypothetically be referenced cross-module; that hypothetical does not qualify. Only an identifier that is *actually* published across a boundary (condition 2), or that meets one of the other conditions, uses `Guid`. Everything else uses the `BIGINT IDENTITY` default.

A `Guid` used this way is server-generated, non-empty, immutable, and never reused, consistent with `DEC-TEN-0001`.

---

# Cross-cutting isolation identifiers

`TenantId` is the canonical cross-cutting isolation identifier. It is a `Guid`, is present on every `ITenantOwnedEntity`, is resolved from the authenticated principal (`ADR-005`, `ADR-006`), and is never accepted from client input for tenant-owned writes.

`CompanyId` becomes the **second** cross-cutting isolation identifier beneath the tenant (see `ADR-014`). It is a `Guid` for the same reasons: it partitions data, will be carried on future company-owned records, and may appear as an optional current-scope claim.

Cross-cutting isolation identifiers are `Guid` regardless of the owning row's own primary-key type; a row may have a `BIGINT` primary key and still carry a `Guid TenantId` (and, in future, a `Guid CompanyId`).

---

# Cross-module identifiers

Any identifier that a module **actually publishes** for another module to store or reference — through a public contract, an integration event, or a foreign key that another module holds — is a `Guid`. A merely conceivable future reference does not qualify; the identifier must be published across a real, approved boundary.

Rationale: cross-module identifiers must be stable across a possible future service extraction (`ADR-001`), must not leak internal sequential ordering, and must be safe to generate on either side of a boundary without a shared database sequence. `CompanyId` is the first such identifier introduced by a business-facing root: it is a cross-cutting isolation dimension and will be an approved HR/GL boundary identifier.

---

# CompanyId decision

`CompanyId` is a **`Guid`**.

- It is a cross-cutting isolation identifier beneath the tenant.
- It will be referenced by future HR and GL company-owned records via `ICompanyOwnedEntity` and restricted foreign keys (`ADR-014`).
- It may later appear as an optional `company_id` current-scope claim; enumeration resistance is desirable.

`Company` internal child rows and history rows introduced by later milestones may still use `BIGINT IDENTITY` under the default rule; only the cross-cutting `CompanyId` root identifier is required to be a `Guid`.

---

# Clustered-index considerations

A random `Guid` used as the **clustered** primary key of a **high-write** table causes page splits and index fragmentation because inserts are not append-ordered. This condition has two necessary parts: the key is (a) the clustered key **and** (b) subject to a high insert rate.

- Low-write roots such as `Tenant` and `Company` do **not** meet this condition. A `Guid` clustered key is acceptable for them without special handling.
- High-write tables that would otherwise cluster on a random `Guid` must adopt one of the mitigations below.

---

# Sequential-Guid condition

Do **not** mandate sequential `Guid`s universally. Apply a sequential-`Guid` or alternate-key mitigation **only** when both clustered-index conditions above hold — a random `Guid` is the clustered key **and** the table is high-write. In that case choose one:

1. Generate sequential `Guid`s (for example `NEWSEQUENTIALID()` or an application-side sequential generator) so inserts remain approximately append-ordered; or
2. Keep a `BIGINT IDENTITY` clustered primary key and expose the `Guid` as a non-clustered unique alternate key.

For low-write roots, and for any table where the `Guid` is not the clustered key, no sequential-`Guid` requirement applies.

---

# Externally exposed IDs

Identifiers returned to clients, used in routes, or referenced across a service boundary should be `Guid` where non-enumerability matters. `BIGINT` identifiers may remain internal; where an internal `BIGINT`-keyed aggregate is exposed externally, the external contract should prefer a stable non-enumerable identifier or documented safe projection rather than leaking a dense sequential key.

---

# Child/history row strategy

Child entities and immutable history rows default to `BIGINT IDENTITY` unless they are themselves referenced across a module boundary. Existing exceptions remain valid: the FP-004 localization history rows use `Guid` under their approved feature decisions and are not changed by this ADR.

---

# Migration compatibility

- This ADR introduces no migration and changes no column type on any existing table.
- Existing `BIGINT` and `Guid` keys remain exactly as implemented.
- New tables follow this rule from their first migration. `platform.Companies` uses a `Guid CompanyId` primary key (`ADR-014`, FP-005 data model) and, per `DEC-TEN-0014`, carries a restricted foreign key to `platform.Tenants(TenantId)` from its first migration.

---

# Existing identifiers remain unchanged

No `TenantUser`, `Role`, `Identity`, authentication, assignment, or localization identifier is altered. Reclassifying any existing key is explicitly out of scope and would require its own ADR and migration with a compatibility and data-preservation plan.

---

# Decision Drivers

- Consistency across modules and with AI-generated code.
- Stability of cross-module contracts and future service extraction.
- Security: non-enumerable identifiers where correlation or disclosure is a risk.
- Performance: clustered-index health on high-write tables.
- Reconciliation of `CON-0201` with already-approved `Guid` identifiers without rework.

---

# Alternatives Considered

## Option 1 – BIGINT everywhere (literal CON-0201)

### Advantages

- Simplest single rule; compact, append-friendly clustered keys.

### Disadvantages

- Contradicts the already-approved immutable `Guid TenantId` (`DEC-TEN-0001`) and localization `Guid` keys.
- Sequential tenant/company identifiers are enumerable and leak scale.
- Would force a disruptive re-keying of existing tables. Rejected.

## Option 2 – Guid everywhere

### Advantages

- Uniform cross-boundary identity; no re-keying of `Guid` tables.

### Disadvantages

- Random `Guid` clustered keys fragment high-write tables.
- Wastes space and join cost on purely internal high-volume rows.
- Contradicts the implemented `BIGINT` internal keys and `CON-0201`'s default. Rejected.

## Option 3 – Documented hybrid (Selected)

### Advantages

- Matches what is already implemented and approved.
- Gives an explicit, testable rule for new identifiers, including `CompanyId`.
- Confines `Guid` to isolation/cross-module/exposed identifiers where it earns its cost.

### Disadvantages

- Requires developers to classify each new identifier against the rule. Accepted as a review responsibility.

---

# Rationale

The hybrid rule is chosen because it is the only option that is simultaneously consistent with the implemented code, compliant with the intent of `CON-0201`, and forward-safe for cross-module identifiers. It reframes the existing `Guid` keys as the exceptions `CON-0201` always allowed, and it makes the `CompanyId = Guid` decision a direct application of the isolation/cross-module criteria rather than a special case.

---

# Consequences

## Positive

- One project-wide identifier rule; no contradiction between `CON-0201` and approved `Guid` keys.
- `CompanyId` is settled as `Guid` before HR and GL depend on it.
- Clustered-index guidance prevents fragmentation without over-mandating sequential `Guid`s.

## Negative

- Each new identifier must be classified against the rule during design and review.
- Mixed key types require developers to be deliberate about join and index design.

---

# Implementation Guidelines

- Default new keys to `BIGINT IDENTITY`; justify any `Guid` against the exception rule in the feature's `decisions-approved.md`.
- Use `Guid` for `TenantId`, `CompanyId`, and any cross-module or externally exposed root identifier.
- Apply a sequential-`Guid` or `BIGINT`-clustered-plus-`Guid`-alternate-key mitigation only for high-write tables clustered on a random `Guid`.
- Never change an existing identifier without a dedicated ADR and migration.

---

# Compliance Rules

- Every new aggregate root records its primary-key type and, if `Guid`, the qualifying condition in its feature decisions.
- Cross-module identifiers are `Guid`.
- No feature silently re-keys an existing table.
- SQL Server integration tests verify key type, uniqueness, and rowversion behavior for each new table.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Guid clustered-key fragmentation on high-write tables | Sequential Guid or BIGINT clustered key + Guid alternate key |
| Inconsistent classification of new identifiers | Record the choice in feature decisions; enforce in review |
| Misreading this ADR as license to re-key existing tables | Explicit "existing identifiers remain unchanged" rule |

---

# Future Considerations

Revisit if a high-write business root must expose a `Guid` clustered key, if global distribution requires globally generated keys everywhere, or if a future ADR proposes re-keying an existing aggregate.

---

# Related Documents

- CON-0200 / CON-0201 – Database constraints
- ADR-002 – SQL Server
- ADR-005 – Multi-Tenancy
- ADR-008 – Entity Framework Core
- ADR-010 – Repository Pattern
- ADR-014 – Company / Legal-Entity Ownership and Scoping
- FP-003 `DEC-TEN-0001` – Guid TenantId authority
- FP-005 Company / Legal-Entity data model

---

# Review Criteria

This ADR should be reviewed if:

- A high-write business root requires a `Guid` clustered primary key.
- A regulatory or distribution requirement mandates a different key strategy.
- A future decision proposes re-keying an existing aggregate.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-09 | Solution Architecture Team | Reconciles CON-0201 with approved Guid identifiers; decides CompanyId = Guid. Accepted after final approval review. |
