---
document_id: FP-005-DEC
title: Company / Legal Entity Decisions
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
approval: Approved for implementation (final approval-review gate passed)
---

# Decisions

> **Approved for Implementation.** The decisions below reflect the approved human architecture decisions (HUMAN-001 … HUMAN-011) and have passed the final approval-review gate; they are binding for FP-005 implementation. Identifiers are stable: where a decision's answer changed, its content was updated in place rather than renumbered.

## DEC-CMP-0001 — Company is tenant-owned

Company is always owned by exactly one Tenant. `Company` implements `ITenantOwnedEntity`, physically carries `TenantId`, and reuses the existing tenant read/write isolation rules unchanged. (`ADR-014`, `BR-CMP-0001`, `BR-CMP-0007`.)

## DEC-CMP-0002 — Company is a data-partition dimension

Company is a true data-partition root beneath the Tenant, not merely an authorization filter. Future company-owned business records are partitioned by `CompanyId` in addition to `TenantId`. (`ADR-014`, `BR-CMP-0002`.)

## DEC-CMP-0003 — CompanyId is a Guid

`CompanyId` is a server-generated, nonempty, immutable, never-reused `Guid`, because it is a cross-cutting isolation identifier and an approved cross-module boundary identifier that HR and GL will reference. Potential future reference alone would not justify a Guid; Company qualifies because it is a cross-cutting isolation dimension and an approved HR/GL boundary identifier. (`ADR-013`, `BRULE-CMP-0007`.)

## DEC-CMP-0004 — Company does not implement ICompanyOwnedEntity

Company is the company root; it is scoped by tenant, not by company. It implements `ITenantOwnedEntity` and does **not** implement `ICompanyOwnedEntity`, mirroring how `Tenant` does not implement `ITenantOwnedEntity`. (`ADR-014`, `BRULE-CMP-0016`.)

## DEC-CMP-0005 — ICompanyOwnedEntity is a separate future concept

`ICompanyOwnedEntity { Guid CompanyId }` is a separate, opt-in interface introduced with the first company-owned business record (expected in HR). Future company-owned records implement both `ITenantOwnedEntity` and `ICompanyOwnedEntity`. FP-005 Milestone 1 introduces no such interface and no company filter/write-guard machinery; `ADR-014` records the concept only. (`ADR-014`, `NFR-CMP-0308`.)

## DEC-CMP-0006 — Company code uniqueness scope

`CompanyCode` is unique **within a tenant** by normalized value, enforced by a `(TenantId, NormalizedCompanyCode)` unique index using `Latin1_General_100_BIN2`, which is authoritative under concurrent creation. The same normalized code may exist in different tenants. Normalization is exactly `Trim().ToUpperInvariant()` with ordinal comparison and no Unicode NFC/NFD normalization. (`BRULE-CMP-0008`, `AC-CMP-0002`, `AC-CMP-0003`, `TS-CMP-0028`.)

## DEC-CMP-0007 — Company code immutability, length, and character set

`CompanyCode` is required, immutable after creation, limited to 64 characters (applied to both accepted input and the normalized value), trimmed, nonempty after trim, free of control characters, and stored with display casing preserved. Unicode is permitted; the code is not ASCII-only. The 64-character limit is chosen for consistency with the established `TenantCode` limit; no further character grammar is imposed in Milestone 1. (`BRULE-CMP-0008`, `SEC-CMP-0208`.)

## DEC-CMP-0008 — Company name is mutable and not unique

`CompanyName` is required, limited to 200 characters, trimmed, display-casing preserving, mutable through the profile update operation, and not unique. The 200-character limit is chosen for consistency with the established `TenantName` limit. (`BRULE-CMP-0009`, `AC-CMP-0004`.)

## DEC-CMP-0009 — Base currency (Platform-owned configuration, immutable in M1)

The company currency attribute is `BaseCurrencyCode`: required at creation, a valid ISO-4217 alphabetic three-letter uppercase code, and **immutable** in Milestone 1. Platform owns the base-currency *configuration value*; it does **not** own functional-currency *accounting semantics* (rates, revaluation, restatement), which a future General Ledger feature defines without changing the Platform Company ownership boundary. `REQ-PLT-0012` supports company currency configuration; "required at creation" and "immutable in Milestone 1" are FP-005 design decisions, not direct `REQ-PLT-0012` wording. (Supersedes the earlier "functional currency" framing.) (`BRULE-CMP-0010`, `SEC-CMP-0208`, `AC-CMP-0005`.)

## DEC-CMP-0010 — Lifecycle states

`CompanyStatus` contains exactly `Active`, `Inactive`, and `Archived`. A newly created company is `Inactive`. (`BRULE-CMP-0002`, `domain-model`.)

## DEC-CMP-0011 — Initial state is Inactive

A newly created Company begins in `Inactive`. `Active` is an explicit readiness / availability state reached only through activation; creating directly as `Active` would create semantic debt when future HR/GL configuration prerequisites are introduced. No `Provisioning`/`Draft` state is introduced in Milestone 1; the existing two-state `Inactive`/`Active` model expresses "created but not yet available." (Supersedes the earlier "initial state Active" decision.) (`BRULE-CMP-0001`, `lifecycle-model`.)

## DEC-CMP-0012 — Archived is terminal

`Archived` is terminal; no transition out of `Archived` is permitted. (`BRULE-CMP-0003`, `AC-CMP-0008`.)

## DEC-CMP-0013 — No physical deletion

Physical Company deletion is prohibited. No delete command, repository method, permission, endpoint, cascade, or routine database operation exists; a persistence guard rejects physical deletion. Archive is the terminal retained operation. (`BRULE-CMP-0006`, `SEC-CMP-0206`, `AC-CMP-0009`.)

## DEC-CMP-0014 — Trusted tenant only

The owning `TenantId` is derived only from the trusted current tenant context and is assigned server-side at creation. It is never accepted from the route, body, header, claim, or query string, and it is immutable after creation. (`SEC-CMP-0201`, `BRULE-CMP-0015`, `AC-CMP-0011`, `AC-CMP-0013`.)

## DEC-CMP-0015 — User↔company assignment deferred

Company-membership and company-scoped access control (`BR-PLT-0002`) are not part of Milestone 1. No company-membership model is defined, and no Milestone 1 requirement promises company-specific user authorization. (`authorization-model`, `AC-CMP-0019`.)

## DEC-CMP-0016 — Company scope resolution deferred

No company scope-resolution mechanism is defined in Milestone 1. A future mechanism may use a token scope claim, a request-selected scope validated server-side, a membership-backed scope, or trusted route/context resolution; FP-005 does not choose among them. Whatever mechanism is chosen, company status is validated live (never trusted solely from a token claim), the company must belong to the trusted tenant, and the caller must be authorized for the company. The existing `ICurrentUser.CompanyId` / `JwtClaimTypes.CompanyId` are existing plumbing only, not a mandated future architecture. (`ADR-014`, `authorization-model`.)

## DEC-CMP-0017 — No Row-Level Security

SQL Server Row-Level Security is not introduced. Tenant isolation is provided by the existing query filter and write guard plus restricted deletes and the deletion guard; a DB-level backstop is out of scope. (`authorization-model`, `data-model`.)

## DEC-CMP-0018 — Immutable audit deferred; production-readiness gate required

Company emits safe audit-ready events through the existing post-commit dispatcher and relies on `IAuditableEntity` metadata for created/modified attribution, which is **not** equivalent to an immutable administrative audit trail. Immutable audit is **not an implementation blocker**: FP-005 Domain/Application/Persistence/API may be built without it. It **is a production-readiness blocker**: production Company **mutations** must not be enabled until a generalized, shared immutable administrative-audit capability is approved and operational. Read-only Company operations are not blocked by that gate. Company must not couple to the FP-004 localization-specific `LocalizationManagementAuditReadiness`; a generalized audit/readiness capability is a documented future need. (`NFR-CMP-0307`, `SEC-CMP-0205`, `HUMAN-006`.)

## DEC-CMP-0019 — Outbox deferred

FP-005 Milestone 1 is Platform-local with no cross-module workflow. It uses the existing in-process post-commit domain-event dispatcher and introduces no integration event and no transactional outbox; no requirement assumes durable delivery. Cross-module event/outbox architecture is handled separately before the first HR/GL cross-module workflow. (`NFR-CMP-0307`, `domain-model`.)

## DEC-CMP-0020 — Concurrency transport (Platform-wide standard)

Company uses optimistic concurrency via SQL `RowVersion`. The HTTP transport uses the Platform-wide canonical padded RFC 4648 Base64 rowversion convention documented in `docs/08-Development/Development-Standards.md` (API Standards → "Optimistic Concurrency (RowVersion) Transport"). Base64Url and hexadecimal are prohibited; malformed → `400 platform.rowversion_invalid`; missing-required → `400 request.invalid`; valid stale → `409 concurrency.conflict`. Company must **not** depend on the localization-owned `LocalizationRowVersionCodec`; an implementation prerequisite is to extract a neutral shared Platform/Host rowversion codec used by both Localization and Company before the Company API is implemented. No new ADR is created; the reusable convention lives in Development Standards. (`BRULE-CMP-0012`, `api-contracts`, `SEC-CMP-0207`, `HUMAN-005`.)

## DEC-CMP-0021 — Permission model

Company uses a three-permission, code-owned set: `Platform.Companies.View` (read/list/detail), `Platform.Companies.Manage` (create and update profile), and `Platform.Companies.Lifecycle` (activate, deactivate, archive). No separate Archive permission and no finer per-transition split in Milestone 1. No implicit permission inheritance is assumed beyond what the existing authorization framework already provides. (`authorization-model`, `AC-CMP-0012`.)

## DEC-CMP-0022 — Pagination limits

`ListCompanies` is page-based with `pageSize` default 50, minimum 1, maximum 200, and `pageNumber` default 1, with deterministic ordering by company name ascending then `CompanyId`, and an optional `status` filter only. Code/name search is deferred. Out-of-range paging is `400 request.invalid`. (`FR-CMP-0103`, `api-contracts`.)

## DEC-CMP-0023 — HTTP routes

The Company HTTP surface under `/api/platform/companies` is: `POST` (create), `GET` (list), `GET /{companyId}`, `PUT /{companyId}` (profile), `POST /{companyId}/activate`, `POST /{companyId}/deactivate`, and `POST /{companyId}/archive`. There is no `DELETE` route and no `reactivate` route. No route accepts a writable `TenantId`. (`api-contracts`.)

## DEC-CMP-0024 — Single reversible enablement pair

Enablement is a single reversible pair: `Activate` performs `Inactive` to `Active`, and `Deactivate` performs `Active` to `Inactive`. Because a company is created `Inactive` and has no provisioning state, `Activate` serves both first enablement and re-enablement, and a separate `Reactivate` command and route are intentionally not defined. (`BRULE-CMP-0005`, `lifecycle-model`.)

## DEC-CMP-0025 — Safe event payloads exclude display text

Company domain events carry `CompanyId`, `TenantId`, status transition, occurrence time, and the bounded reason code only. They exclude the company name and every other display text, credential, token, complete claims collection, secret, and HTTP context. (`SEC-CMP-0205`, `AC-CMP-0015`.)

## DEC-CMP-0026 — Status-change reason vocabulary

`CompanyStatusChangeReason` contains exactly `Created`, `Administrative`, `Operational`, `Compliance`, `CustomerRequest`, and `IssueResolved`. Creation records `Created`; every later transition requires a non-`Created` code; Activate, Deactivate, and Archive each require an explicit non-`Created` code. (`BRULE-CMP-0011`, `BRULE-CMP-0017`.)

## DEC-CMP-0027 — Archive eligibility extensibility (new)

In Milestone 1 a company may be archived from `Active` or `Inactive` with no additional prerequisite, and the transition graph is fixed. As dependent modules such as HR and GL are introduced, archive eligibility may acquire additional **module-owned** prerequisite checks (for example active employees, open accounting periods, or posted/unsettled accounting dependencies). Those checks are not encoded in Milestone 1 and, when introduced, must be evaluated through approved published module contracts/queries or another architecture-approved boundary; the Platform Company Domain must never directly reference HR or GL Domain types. (`BRULE-CMP-0018`, `lifecycle-model`, `HUMAN-007`.)

## Reconciled conflict register

| # | Prior conflict | Resolution |
|---|---|---|
| CONFLICT-CMP-0001 | The Draft Tenant Management functional document mixed company notes into tenant management | FP-005 is authoritative for the Company aggregate, identity, and lifecycle; the Draft company notes are superseded and migrated here |
| CONFLICT-CMP-0002 | `REQ-PLT-0012` requires per-company fiscal settings, currencies, language, and numbering | FP-005 realizes only the base-currency configuration portion now; "required at creation" and "immutable" are FP-005 design decisions (`DEC-CMP-0009`), not direct `REQ-PLT-0012` wording; fiscal calendar, additional currencies, language, and numbering are deferred |
| CONFLICT-CMP-0003 | `CON-0201` BIGINT default versus a Guid `CompanyId` | Resolved by `ADR-013`: `CompanyId` is an approved Guid exception because it is a cross-cutting isolation and approved cross-module boundary identifier; hypothetical future reference alone would not qualify |
| CONFLICT-CMP-0004 | Whether to create in `Active` or `Inactive` | Resolved by `DEC-CMP-0011`: Company is created `Inactive`; `Active` is an explicit availability state; no provisioning gate is introduced |
| CONFLICT-CMP-0005 | Earlier planning listed both Activate and Reactivate | Resolved by `DEC-CMP-0024`: a single reversible `Activate`/`Deactivate` pair; no separate `Reactivate` |
| CONFLICT-CMP-0006 | Whether the base currency may change after creation | Resolved by `DEC-CMP-0009`: immutable in Milestone 1; any future functional-currency change is a dedicated GL feature with revaluation semantics |
| CONFLICT-CMP-0007 | Whether Company should carry company-ownership filtering now | Resolved by `DEC-CMP-0005` and `NFR-CMP-0308`: `ICompanyOwnedEntity` and its machinery are deferred to the first company-owned record |
