---
id: ADR-016
title: Platform-Support Bootstrap, Lifecycle, and Authority Administration
category: Architecture Decision Record
version: 1.3
status: Accepted
date: 2026-08-10
owner: Solution Architecture Team
tags:
  - authentication
  - authorization
  - security
  - platform-plane
  - bootstrap
  - lifecycle
depends_on:
  - ADR-006
  - ADR-015
used_by:
  - Platform
  - FP-003
---

# ADR-016: Platform-Support Bootstrap, Lifecycle, and Authority Administration

---

# Status

**Accepted**

Accepted as the Phase-3 decision-gate outcome for the platform-support authority introduced by `ADR-015`. It refines — and does not supersede — `ADR-015`; the two-plane model, scope invariants, and token-profile rules there remain in force. It records the three decisions that become security-critical before platform authority can be issued into usable tokens: genesis bootstrap, principal lifecycle/status, and authority-administration permission. These are recorded concisely in FP-003 `decisions-approved.md` as `DEC-TEN-0019`, `DEC-TEN-0020`, and `DEC-TEN-0021`.

---

# Context

`ADR-015` established the platform plane. FP-003 Phase 1 delivered the code-owned permission catalog + `Platform.Tenants.View/Manage/Lifecycle` at `PermissionScope.PlatformSupport`, the tenant-role escalation invariant, and the tenant-token defence-in-depth filter. Phase 2 delivered the persisted authority — `PlatformSupportPrincipal` (global, non-tenant-owned, anchored to the existing global `Identity`) and `PlatformPermissionAssignment` (revocable, audited) — with a catalog-validated authority read (`IPlatformSupportPermissionReadService`), physical-delete protection, and SQL verification. The Register/Grant/Revoke handlers exist but are deliberately **not** DI-registered and are unreachable.

Phase 3 will issue platform-support access tokens (`security_plane=platform`, no `tenant_id`) sourced from the persisted authority. Three questions must be resolved first, and each has a directly relevant existing mechanism:

- **Genesis bootstrap.** Nothing can create the *first* platform-support principal without a trusted, non-tenant, non-circular mechanism. The codebase already validates config-owned allow-lists at startup (`AuthenticationClientOptions.AllowedClientIds`, `AuthenticationTransportOptions.AllowedOrigins`, both `ValidateOnStart`). There is **no** EF `HasData`/data-migration seeding anywhere — identities, roles, permissions, and principals are always created at runtime.
- **Principal lifecycle.** Phase 2 gave the principal no status; authority is expressed only through revocable assignments. `AuthenticationAccount.Disable()` already exists as a *global* account kill (flips `Status` to `Disabled`, increments `SecurityVersion`, and — via the refresh handler's live re-check of `IsAuthenticationEligible` and `SecurityVersion` — revokes sessions on next refresh), but it stops **all** access (including the person's tenant business access) and cannot express a platform-only suspend that retains history and supports re-enable. The live-status-check pattern (`ITenantAuthenticationEligibilityReadService`) is the precedent for reading current status rather than trusting a token claim.
- **Authority administration permission.** The three existing permissions all govern *tenant* administration; none governs administering platform principals/assignments. `ADR-015` already states bootstrap "is not the long-term permission-management model," which implies a management permission that does not yet exist.

Related decisions: `ADR-006` (JWT/claims, optional future MFA), `ADR-015` (platform plane, token profile, `SecurityVersion + session revocation` as the live-token-invalidation mechanism), FP-003 `DEC-TEN-0010` (a status change does not cryptographically invalidate an already-issued short-lived JWT), `DEC-TEN-0018` (platform authority model).

---

# Problem Statement

Before Phase 3 issues platform tokens, define: (1) how the first/recovery platform authority is trusted without a circular dependency, tenant self-promotion, or a permanent invisible bypass; (2) how a platform-support principal is suspended immediately and re-enabled without deleting history and without over-broadly disabling the person's tenant access; and (3) which permission governs platform-authority administration so `Platform.Tenants.Manage` is never repurposed for it.

---

# Decision

## 1. Genesis bootstrap (DEC-TEN-0019)

A **configuration-owned bootstrap allow-list**, keyed by the immutable **`AuthenticationSubject`** (the external subject on the global `Identity`), authorizes **only** the genesis/recovery creation of platform authority — never ordinary platform operations.

- The configured subject must resolve to an existing `Identity` with an authentication-capable, active `AuthenticationAccount`. Bootstrap **never** creates identities.
- The trust key is `AuthenticationSubject` (immutable, unique, environment-portable). Email, username, display name, `TenantId`, and `IdentityId` are rejected as configuration keys.
- Bootstrap may perform only the minimum genesis/recovery operation: register/resolve the platform-support principal and establish the initial approved `PermissionScope.PlatformSupport` permission set. It is not per-request authorization, not an implicit super-admin, not a tenant-admin path, and not a bypass around normal platform authorization once usable authority exists. No tenant role can invoke it.
- Configuration (`PlatformSupportBootstrapOptions`) follows the established `Bind`/`Validate`/`ValidateOnStart` convention with unique, non-blank, canonical `AuthenticationSubject` entries; deployment/secret-managed; never tenant-editable.

### Usable platform authority (security-critical definition)

**Usable platform authority exists** when at least one `PlatformSupportPrincipal` simultaneously:

1. has `Status == Active`; **and**
2. is anchored to a valid, authentication-capable `Identity`/`AuthenticationAccount`; **and**
3. has at least one **active** `PlatformPermissionAssignment` whose permission name exists in `IPermissionCatalog` with `PermissionScope.PlatformSupport`.

A revoked assignment, a corrupt tenant-scoped assignment row, an unknown permission name, and a `Disabled` principal all **do not** count as usable authority.

### Bootstrap lifetime and recovery

Bootstrap is configuration-controlled, genesis/recovery-only, audited, and non-tenant-editable; it is **not** standing request authorization. Once usable platform authority exists, bootstrap is inert. Configuration may remain for disaster recovery but cannot authorize ordinary platform operations.

Recovery becomes eligible only when **no usable platform authority exists** (all principals `Disabled`, or all valid `PlatformSupport` assignments revoked). Bootstrap **must not** silently re-enable an existing `Disabled` principal: a `Disabled` principal stays `Disabled` until an explicit lifecycle re-enable. Config membership is never equivalent to "always platform-authorized" or to re-enable authority; recovery is a distinct, separately-auditable operation. *(Refined by Decision §5 / `DEC-TEN-0026` (Approved): recovery is additionally eligible when usable platform authority exists but no usable platform **administrative** authority — an Active principal holding active current-catalog `Platform.Support.Administer` — exists. This forward note does not change the §1 genesis semantics.)*

### Live usable-authority evaluation

"No usable platform authority exists" is evaluated **live against persisted current state** every time bootstrap runs, using the definition above. It considers the current principal `Status`, the current authentication-account eligibility, the active persisted assignments, and the current code-owned `IPermissionCatalog` scope. It is **never** inferred from configuration, a cached bootstrap-success flag, the mere presence of a principal row, or corrupt assignment rows.

### Bootstrap subject cardinality and deterministic selection

`PlatformSupportBootstrapOptions` **may** contain multiple unique configured `AuthenticationSubject` values (for operational recovery, rotation, and multiple environment-approved operators). Bootstrap does **not** automatically create a principal for every configured subject. A single bootstrap evaluation establishes **exactly one** usable genesis/recovery principal.

An **eligible** configured subject is (1) canonical, (2) unique, (3) resolves to an existing `Identity`, (4) has an authentication-capable, eligible `AuthenticationAccount`, and (5) does not already own a `PlatformSupportPrincipal` for a create operation. Bootstrap sorts eligible subjects by deterministic **ordinal** comparison of the canonical `AuthenticationSubject` and selects the **first** eligible subject — never configuration insertion order. Remaining configured subjects stay recovery candidates and receive no authority automatically.

### Concurrent bootstrap convergence

Two instances may observe "no usable authority" at approximately the same time. Each (1) evaluates usable authority live from persistence, (2) resolves and deterministically selects the same first eligible subject, (3) attempts registration/grant, (4) is bounded by the Phase-2 authoritative uniqueness (`UX_PlatformSupportPrincipals_IdentityId` and the active-assignment unique index), (5) treats a duplicate as an idempotent race outcome, (6) re-reads usable authority, and (7) becomes inert once usable authority exists. For the same configuration and persistence state, a bootstrap race establishes **exactly one** genesis/recovery principal — not one per instance. No distributed lock is introduced.

### Disabled-principal recovery and fail-closed

Recovery **never** changes `Disabled → Active` for an existing principal and never grants new permissions to a `Disabled` principal. When no usable authority exists, bootstrap may create a **new** `Active` recovery principal only for an eligible configured subject that does not already own a principal (same deterministic selection). Examples:

- Configured `{A, B}`; `A` exists and is `Disabled`; `B` is eligible and owns no principal → bootstrap creates `B` as the `Active` recovery principal and grants the initial `PlatformSupport` set; `A` remains `Disabled`.
- Configured `{A}` only; `A` exists and is `Disabled`; no other candidate → bootstrap **fails closed** (it must not re-enable `A` and must not duplicate a principal for `A`) and emits an operator diagnostic that no eligible recovery subject exists. Recovery then requires either adding another approved pre-existing `AuthenticationSubject` to deployment configuration, or the separately-authorized explicit Re-enable lifecycle operation once an authorized recovery actor exists. Manual SQL is a break-glass activity only if separately governed operationally; it is not the designed recovery mechanism.
- Principal `A` is `Active` but has no active catalog-valid `PlatformSupport` assignment (revoke-all) → `A` is not usable authority; if configured `B` owns no principal, bootstrap may establish `B` as the recovery principal; `A`'s assignments are not mutated automatically.

### Bootstrap grant set

The established genesis/recovery principal receives `Platform.Support.Administer` as part of its initial authority set (once that permission exists in the catalog), enabling the Phase-4 transition to normal self-hosting administration. It may also receive explicitly configured, approved initial `Platform.Tenants.*` permissions required operationally; all three are not required unless configuration selects them. The bootstrap grant set contains only code-catalog permissions with `PermissionScope.PlatformSupport`.

### Bootstrap audit and failure

Bootstrap reuses the Phase-2 audit fields with a distinguishable actor representation (e.g. `platform-bootstrap:<subject>`); no immutable-audit infrastructure is invented. Failure behaviour: configured subject absent → no authority created (logged); account not eligible → no authority; principal already exists → idempotent, no duplicate; assignment already exists → no duplicate row; usable authority already exists → bootstrap does nothing; unknown permission → fail closed; tenant-scoped permission → fail closed.

## 2. Platform-support principal lifecycle (DEC-TEN-0020)

Add a minimal status: `PlatformSupportPrincipalStatus { Active, Disabled }`, default `Active`, transitions `Active → Disabled` and `Disabled → Active` (both non-terminal). No `Suspended`/`Archived`/`Deleted` at this stage.

- **Active:** platform authority may be evaluated from active assignments.
- **Disabled:** all platform authority from the principal is unusable regardless of retained assignments. Assignments remain persisted; `Disabled` neither deletes nor revokes them.
- **Grant while Disabled is rejected** (grant requires `Active`) so administration never silently arms dormant authority. **Revoke while Disabled is allowed** (it reduces authority and supports cleanup).
- **Re-enable** (`Disabled → Active`) restores the eligibility of still-active retained assignments; it does not recreate revoked assignments. It is a privileged platform-authority lifecycle operation (no HTTP design here).
- **RowVersion** (present since Phase 2) is now authoritative for principal-lifecycle optimistic concurrency; status mutations use it.
- **Status audit and migration backfill:** persist `StatusChangedUtc` and `StatusChangedBy` in addition to the existing `IAuditableEntity` `ModifiedUtc/By`; no reason-code is introduced. The migration adds `Status` as **NOT NULL** (`nvarchar`, `BIN2` collation, `CHECK Status IN ('Active','Disabled')`, default `'Active'`) and `StatusChangedUtc`/`StatusChangedBy` as **NULLABLE**. Any principal that existed before the status migration backfills to `Status = 'Active'` with `StatusChangedUtc = NULL` and `StatusChangedBy = NULL` — a schema addition is not a lifecycle transition, so no historical transition is synthesized from `CreatedUtc`/`CreatedBy`. The first actual `Disable` or `Re-enable` populates both `StatusChangedUtc`/`StatusChangedBy`, and every subsequent transition overwrites them with the latest transition metadata; `ModifiedUtc`/`ModifiedBy` continue to be updated by the normal audit mechanism.

### Token consequences

Separate the layers explicitly:

- **Issuance:** a `Disabled` principal cannot receive a new platform access token; issuance performs a **live** principal-status eligibility check (mirroring `ITenantAuthenticationEligibilityReadService`). No token-carried status is authoritative.
- **Refresh:** platform refresh/session continuation re-reads live status; if `Disabled`, refresh is denied and the platform session is revoked per existing session conventions; no new token is issued.
- **Existing access token:** disabling a principal does **not** cryptographically invalidate an already-issued short-lived JWT merely by changing DB status (consistent with `DEC-TEN-0010`). An existing access token may remain usable until natural expiry, or until the `SecurityVersion` + session-revocation mechanism (`ADR-015`) causes rejection where it applies. Immediate cut-off is achieved by session revocation, not by the status field alone.
- **`StrictAccessTokenValidator`:** validates the platform token profile **structurally** (`security_plane=platform` present; `tenant_id` forbidden). It does **not** query SQL for live principal status on every request.

Whether the platform session reuses `AuthenticationAccount.SecurityVersion` plus `AuthenticationSession` revocation, or introduces a distinct platform-session security-version, is a Phase-3C implementation detail deferred here; the guaranteed behaviour above holds regardless. This ADR does **not** assign `PlatformSupportPrincipal` its own `SecurityVersion`. *(Resolved in Decision §4 / `DEC-TEN-0022`: the platform session reuses the global `AuthenticationAccount.SecurityVersion` in a separate `PlatformAuthenticationSession`; no principal security-version is introduced.)*

## 3. Platform-authority administration permission (DEC-TEN-0021)

Author a new permission `Platform.Support.Administer` at `PermissionScope.PlatformSupport`, governing platform-support principal registration, permission grant, permission revoke, and principal Disable/Re-enable. A single permission covers this small authority-management surface; no additional permissions are introduced.

- `Platform.Tenants.Manage` and `Platform.Tenants.Lifecycle` govern **Tenant** administration and lifecycle and **never** authorize administering `PlatformSupportPrincipal` or `PlatformPermissionAssignment`.
- Bootstrap is the only exception, and only before usable platform authority exists. Once it exists, Register/Grant/Revoke/Disable/Re-enable require `Platform.Support.Administer` through the future platform-plane authorization layer; bootstrap config never substitutes for this permission on ordinary requests.
- Catalog impact: the `PlatformSupport` catalog grows from three to four entries when `Platform.Support.Administer` is authored. Being `PlatformSupport`-scoped, it is excluded from tenant-facing catalog listings and from tenant-token claims by the Phase-1 filters; the platform authority read path returns it only when legitimately assigned.

## 4. Platform authentication session representation (DEC-TEN-0022, Phase 3C)

DEC-TEN-0020 above deferred to Phase 3C *whether the platform session reuses `AuthenticationAccount.SecurityVersion` + `AuthenticationSession` revocation or introduces a distinct model*. This subsection records the resolution (FP-003 `decisions-approved.md`, `DEC-TEN-0022`) before any Phase-3C production code; it refines and does not supersede DEC-TEN-0018/0020 or `ADR-015`.

- **Separate aggregate.** Platform authentication uses a new `PlatformAuthenticationSession` aggregate and table. The existing tenant `AuthenticationSession` is structurally tenant-bound (constructor rejects empty `TenantId`/`TenantUserId`; both are `NOT NULL` with FKs to `Tenant`/`TenantUser`; events and queries carry tenant identifiers) and remains unchanged. `TenantId`/`TenantUserId` are **not** made nullable and **no** plane discriminator weakens the tenant table. Structural separation — not a nullable flag — provides cross-plane isolation.
- **Ownership.** `PlatformAuthenticationSession` is global platform-plane, not `ITenantOwnedEntity`/`ICompanyOwnedEntity`, and carries no `TenantId`/`TenantUserId`/`CompanyId`. It is anchored to `IdentityId` **and** `PlatformSupportPrincipalId` (both required, both FK-enforced with `OnDelete(Restrict)`); its refresh-token records live in platform-session persistence separate from tenant `RefreshTokenRecord`s and are retained (physical-delete-protected, mirroring `PreventAuthenticationHistoryDeletion`).
- **Cross-plane refresh isolation (security-critical).** A platform refresh token resolves only against platform-session persistence and a tenant refresh token only against tenant-session persistence; the plane is server/persistence-owned. Tenant→platform and platform→tenant refresh switching are rejected; a refresh token never changes plane.
- **Security version.** The platform session snapshots the global `AuthenticationAccount.SecurityVersion` (the same value tenant sessions use); `PlatformSupportPrincipal` receives **no** `SecurityVersion`. Principal Disable is a platform-plane state whose immediate cutoff is platform-session revocation, not a global version bump (which would also kill tenant access).
- **Issuance/refresh eligibility.** Issuance and refresh perform live checks: account `IsAuthenticationEligible`, live `account.SecurityVersion` match, principal `Status == Active`, and at least one active catalog-valid `PermissionScope.PlatformSupport` permission. Zero permissions or `Disabled` → deny (and, on refresh, revoke the platform session and issue no token). Permissions are re-derived live from `IPlatformSupportPermissionReadService`; no stale snapshot is reused.
- **Proactive revocation.** Explicitly disabling a principal revokes that principal's active platform sessions (platform-only; tenant sessions untouched), blocking refresh immediately. Consistent with `DEC-TEN-0010`, an already-issued short-lived platform access JWT is not cryptographically invalidated and expires naturally; `StrictAccessTokenValidator` stays stateless/structural (profile selected by `security_plane`; platform branch forbids `tenant_id`/`tenant_user_id`/`role`/`company_id`; no DB lookup). Re-enable does not revive revoked sessions.
- **Separation of concerns.** A distinct `PlatformAccessTokenClaimsProvider` and a typed platform issuer path keep the tenant claims provider/issuer unchanged and make the security plane server-selected, never caller-selected. Platform-session creation consumes a trusted `VerifiedIdentity`, never a caller-supplied `IdentityId`. No HTTP route, `PlatformPermissionAuthorizationHandler`, or `RequirePlatformPermission` is introduced in Phase 3C (Phase 4).
- **Persistence impact.** An additive migration introduces `platform.PlatformAuthenticationSessions` and a platform refresh-token structure plus a platform revocation reason (e.g. `PlatformPrincipalIneligible`); the tenant session schema is untouched.

## 5. Platform request-plane authorization and HTTP exposure (DEC-TEN-0023–0026, Phase 4)

Phase 3C left platform authorization and HTTP exposure out of scope. Phase 4A (committed) delivered the request-authorization primitives; the remaining request-plane / HTTP-exposure decisions are recorded in FP-003 `decisions-approved.md` as `DEC-TEN-0023`–`DEC-TEN-0026` (**Approved for Implementation**). This subsection summarises them; it refines and does not supersede DEC-TEN-0018/0020/0022 or `ADR-015`.

- **Phase-4A authorization primitives (delivered).** A dedicated `PlatformPermissionAuthorizationHandler` and `RequirePlatformPermission(...)` convention authorize a request iff it is authenticated, carries exactly one ordinal-exact `security_plane=platform` claim, and the **requested** permission is catalog-known with `PermissionScope.PlatformSupport` **and** present as an exact permission claim. The handler is **stateless** (claims + code-owned `IPermissionCatalog` only) — no DB, principal-status, or session lookup — preserving the DEC-TEN-0020/0022 rule that an already-issued short-lived access JWT keeps its authority until natural expiry. The dynamic `PlatformPermission:` policy prefix is structurally distinct from the tenant `Permission:`/`Role:` prefixes, and tenant authorization is unchanged.
- **Platform HTTP authentication, server-owned plane (DEC-TEN-0023).** A verified identity obtains a platform session over a **dedicated** platform login route (credentials only) whose server flow is the trusted `DEC-TEN-0022` creation path: `VerifyPasswordCredentialsCommandHandler` → `VerifiedIdentity` → resolve principal by `IdentityId` → `Active` + ≥1 catalog-valid `PlatformSupport` permission → `PlatformAuthenticationSessionCreator`. The caller **cannot** supply `security_plane`, principal id, permissions, or `SecurityVersion`; the plane is server-derived from the route **and** persisted authority. A dedicated platform refresh route resolves only the platform store (tenant/platform cross-refresh rejected). A dedicated PLATFORM-ONLY platform logout revokes the current platform session — but this Application capability **does not yet exist** (Phase 3C added no platform current-session revocation; only the tenant equivalent exists), so 4B must add a new command (conceptually `RevokeCurrentPlatformAuthenticationSessionCommand`) that resolves the target session from the **validated `session_id` claim** (never a caller-supplied id) in the platform store only, revokes the platform session and blocks refresh, and leaves the tenant session, `SecurityVersion`, and the already-issued access JWT untouched (JWT valid until expiry). Login collapses all failure modes to a generic external authentication failure (no enumeration). **The create-vs-disable concurrency item (L1) must be closed by serialization** — a transactionally effective `FOR UPDATE`/locking read of the principal authority state in the creation transaction — **before 4B ships; correctness must not depend on deployment isolation settings (the isolation-assumption alternative is withdrawn).**
- **Request-plane taxonomy (DEC-TEN-0024, resolves the F3C-4 question).** Three authenticated-policy classes — tenant-authenticated, platform-authenticated (structural/claims-based, DB-free), and plane-neutral (explicit, justified only) — replace bare `RequireAuthenticatedUser` on plane-specific routes, enforced by an architecture/API test that inspects endpoint authorization metadata from the test Host (no runtime DB/service). The current bare-authenticated endpoints (`/api/platform/auth/logout`, localization `effective`/`batch`) are classified **TENANT-ONLY** (verified tenant-context behaviour).
- **Authority read authorization (DEC-TEN-0025).** Platform authority read/list operations require `Platform.Support.Administer`; no new read permission is added in Phase 4. Reads use transport DTOs (paginated principal list with stable ordering; assignment history including revoked records; a separate current active-permission-names projection; no EF entities, no secrets).
- **Last-admin / self-disable / recovery (DEC-TEN-0026).** Self-disable, self-revoke, and last-admin removal are allowed with no preventive guard. Recovery distinguishes **usable platform authority** (any Active principal with ≥1 catalog-valid `PlatformSupport` permission) from **usable platform *administrative* authority** (an Active principal holding `Platform.Support.Administer`): genesis/recovery bootstrap becomes eligible when **either** no usable platform authority exists **or** usable platform authority exists but no usable platform *administrative* authority exists (e.g. the last `Administer` was revoked while `Platform.Tenants.View` remains). **This refines `DEC-TEN-0019` recovery eligibility for administrative-authority loss and does not rewrite `DEC-TEN-0019`'s genesis history.** Recovery still only creates a **new** `Active` principal for an eligible configured subject that owns no principal, never re-enables a `Disabled` principal, never grants `Administer` to an arbitrary existing principal, and — if no eligible configured recovery subject exists — remains **fail-closed** (governed break-glass may be required; automatic recovery is not guaranteed in every environment). Eligibility is evaluated **live** against persisted state, never from a still-valid access JWT. An administrator's already-issued access JWT may retain authority until expiry (stateless); Disable proactively revokes sessions and revoke applies at next refresh. The administrative-recovery predicate is **bootstrap/recovery state logic, not per-request authorization** (no new per-request DB lookup). Ownership: a dedicated **4D-0** sub-slice lands the predicate refinement before HTTP `Revoke`/`Disable` (4D) is complete (SQL gate: yes).
- **Invariants preserved.** No new JWT claim (no `principal_id`); no per-request DB authorization; the Phase-3C token profile, `StrictAccessTokenValidator`, and tenant authorization are unchanged; Phase 4 exposes platform-authority administration only (tenant management remains Phase 5).

---

# Decision Drivers

- Security: no circular bootstrap, no tenant self-promotion, no permanent invisible bypass, no `Platform.Tenants.*` repurposing.
- Reuse: the established config-allow-list, live-status-check, and `SecurityVersion`/session-revocation mechanisms.
- Least privilege: a single narrow authority-administration permission; a platform-only suspend distinct from the global account disable.
- Minimalism: the smallest lifecycle model (two states) that safely supports emergency disable, re-enable, and status-aware token issuance.

---

# Alternatives Considered

## Bootstrap

### Option 1 – DB seed / data-migration genesis

Rejected: no `HasData`/seed pattern exists in the codebase; runtime creation is the universal convention.

### Option 2 – Standing configuration-as-authority (config identity always platform-authorized)

Rejected: a permanent, invisible request-time bypass that defeats normal platform authorization and disable semantics.

### Option 3 – Manual SQL administration as the normal model

Rejected by `ADR-015`; permitted only as an unaudited last resort, not the operating model.

### Option 4 – External-IdP group claim mapped to bootstrap authority

Deferred: `ADR-015` lists an external identity provider as a *future* platform-support authority source.

### Option 5 (Selected) – Config allow-list authorizing genesis/recovery only

Accepted: startup-validated, immutable-subject-keyed, genesis/recovery-only, audited, inert once usable authority exists.

## Lifecycle

### Option A – No principal status (authority only via active assignments)

Rejected: no clean platform-only suspend; emergency disable requires the over-broad account disable or a non-atomic revoke-all that does not support retain-and-re-enable.

### Option B – Boolean `IsEnabled`

Rejected in favour of an enum for consistency with `TenantStatus`/`AuthenticationAccountStatus`/`RoleStatus` and future extensibility.

### Option C (Selected) – Enum `{ Active, Disabled }`

Accepted: smallest model that safely supports emergency disable, re-enable, retained history, and status-aware issuance.

### Option D – `{ Active, Suspended, Disabled }` / add `Archived`

Rejected as premature: no concrete Phase-3 semantics for the extra states.

---

# Rationale

Each decision reuses an existing, proven mechanism rather than inventing one: the config allow-list mirrors `AllowedClientIds`/`AllowedOrigins`; the live status check mirrors `ITenantAuthenticationEligibilityReadService`; token invalidation reuses `SecurityVersion` + session revocation as `ADR-015` already anticipated; and the two-state lifecycle mirrors the enum-status convention used across the domain. The authority-administration permission closes the one genuine gap — that no existing permission governs platform-principal administration — without repurposing tenant permissions.

---

# Consequences

## Positive

- The first/recovery platform authority is trusted without circularity, tenant self-promotion, or a standing bypass.
- A platform-only emergency suspend with retained history and clean re-enable, distinct from the global account disable.
- Status-aware token issuance/refresh; the Phase-2 `RowVersion` gains a genuine consumer.
- A precise, least-privilege authority-administration permission; `tenant_id` and `Platform.Tenants.*` meanings stay intact.

## Costs

- A new bootstrap configuration section + startup validator + genesis/recovery gate.
- A small additive migration (principal `Status` + `StatusChangedUtc/By`) before Phase-3 issuance.
- A fourth `PlatformSupport` catalog entry (`Platform.Support.Administer`) when authored.
- A stronger security-test matrix (bootstrap eligibility, disable/re-enable, token status-gating).

---

# Implementation Guidelines

Phase ownership (adjust numbering to existing docs):

- **Phase 3A** — principal `Active/Disabled` domain model + `StatusChangedUtc/By`, the status migration, lifecycle persistence/application support, tests + SQL.
- **Phase 3B** — bootstrap configuration + genesis/recovery gate; author `Platform.Support.Administer` in the catalog; tests.
- **Phase 3C** — platform token/session profile, `security_plane=platform`, platform token issuance, live status eligibility, claims sourced from `IPlatformSupportPermissionReadService`, platform refresh/session behaviour, `StrictAccessTokenValidator` platform profile.
- **Phase 4** — platform request authorization + HTTP exposure of platform-authority administration only, authorized by `Platform.Support.Administer` (`DEC-TEN-0023`–`DEC-TEN-0026`), in gated slices:
  - **4A (committed)** — `PlatformPermissionAuthorizationHandler`, `PlatformPermissionRequirement`, `PlatformPermission:` dynamic policy, `RequirePlatformPermission`, Host DI, anti-escalation + real signed-JWT pipeline tests. SQL gate: no.
  - **4B** — platform authentication HTTP exposure: dedicated platform login/refresh/logout routes with server-owned plane selection (`DEC-TEN-0023`), including the **new** platform current-session-revoke command; **close L1 by serialization** first (real SQL concurrency proof). SQL gate: yes (real session create/refresh + concurrency).
  - **4C** — platform authority read/query surface (`DEC-TEN-0025`), Application/read-only; may precede 4B. SQL gate: yes (real read verification).
  - **4D-0** — administrative-recovery predicate refinement (`DEC-TEN-0026`): distinguish usable *administrative* authority and make recovery eligible on its loss; bootstrap/recovery state logic only (no per-request authorization change). Must land before the 4D `Revoke`/`Disable` HTTP endpoints are complete. SQL gate: yes.
  - **4D** — platform authority-administration HTTP endpoints (Register/Grant/Revoke/Disable/Re-enable/read) gated by `RequirePlatformPermission(Platform.Support.Administer)`; Application handlers already exist. SQL gate: yes.
  - **4E** — request-plane taxonomy hardening (`DEC-TEN-0024`), endpoint classification, architecture metadata guard, and final Phase-4 regression. SQL gate: no new SQL (policy/regression; regression may rerun existing SQL suites).
- **Phase 5** — Tenant management HTTP endpoints (`Platform.Tenants.View`/`Manage`/`Lifecycle`), not exposed in Phase 4.

MFA/strong-auth remains a Production-readiness gate (`ADR-015`), not a dev/test blocker. Nothing in this ADR is implemented by the documentation task that records it.

# Compliance Rules

- Bootstrap security invariants: (1) bootstrap cannot re-enable a `Disabled` principal; (2) bootstrap cannot grant new permissions to a `Disabled` existing principal; (3) bootstrap cannot duplicate a principal for the same identity; (4) bootstrap establishes at most one new genesis/recovery principal per bootstrap convergence event; (5) configured recovery candidates receive no authority automatically; (6) bootstrap is inert as soon as usable authority exists; (7) every successful genesis/recovery operation is audited; (8) no tenant role/user can trigger bootstrap through application APIs. Bootstrap is keyed only by immutable `AuthenticationSubject` and evaluates usable authority live from persistence; it never creates identities.
- A `Disabled` principal cannot receive or refresh a platform token; grant is rejected; revoke is allowed; assignments are retained.
- `StrictAccessTokenValidator` performs no live DB status lookup.
- Platform-authority administration requires `Platform.Support.Administer`; `Platform.Tenants.*` never administers platform principals/assignments.
- `Platform.Support.Administer` is `PlatformSupport`-scoped and is excluded from tenant roles, tenant-facing catalog listings, and tenant-token claims.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Bootstrap becomes a permanent authorization bypass | Genesis/recovery-only, inert once usable authority exists, audited, non-tenant-editable |
| Config membership treated as standing authority | "Usable platform authority" definition + recovery-only eligibility; never a re-enable of a `Disabled` principal |
| Emergency disable is over-broad (kills tenant access) | Platform-only principal `Disabled` status, distinct from `AuthenticationAccount.Disable()` |
| Stale token used after disable | Live status check at issuance/refresh + `SecurityVersion`/session revocation for immediate cut-off; short-lived access tokens expire |
| `Platform.Tenants.Manage` misused for platform-principal admin | Dedicated `Platform.Support.Administer`; explicit non-repurposing rule |
| Tenant admin self-promotion via bootstrap | No tenant-IAM path; immutable-subject config only |

---

# Future Considerations

Revisit when: an external identity provider becomes the platform-support authority source; MFA/strong-auth is designed; the platform-session refresh/re-authentication and security-version model is finalized (Phase 3C); or additional principal states (`Suspended`/`Archived`) gain concrete requirements.

---

# Related Documents

- ADR-006 – JWT Authentication and Claims-Based Authorization (MFA compatibility)
- ADR-015 – Platform-Plane Authentication and Authorization (two-plane model, token profile, `SecurityVersion` + session revocation)
- FP-003 – `decisions-approved.md` (`DEC-TEN-0010`, `DEC-TEN-0018`, `DEC-TEN-0019`, `DEC-TEN-0020`, `DEC-TEN-0021`), `authorization-model.md`, `acceptance-criteria.md`, `test-scenarios.md`, `traceability-matrix.md`

---

# Review Criteria

This ADR should be reviewed when: bootstrap is superseded by an external IdP authority source; a per-request live platform-status check is required for high-risk operations; additional principal lifecycle states are introduced; or the platform-session security-version model is finalized.

---

# Revision History

| Version | Date | Author | Description |
|----------|------|--------|-------------|
| 1.0 | 2026-08-10 | Solution Architecture Team | Records the Phase-3 decision-gate outcome: genesis bootstrap, principal lifecycle/status, and authority-administration permission. Accepted after final approval review. |
| 1.1 | 2026-08-11 | Solution Architecture Team | Adds the Phase-3C session-profile subsection (Decision §4) resolving the platform authentication session representation deferred by DEC-TEN-0020: a separate `PlatformAuthenticationSession` aggregate with structural cross-plane refresh isolation, recorded as FP-003 `DEC-TEN-0022`. Refines, does not supersede, prior decisions. |
| 1.2 | 2026-08-12 | Solution Architecture Team | Adds the Phase-4 request-plane / HTTP-exposure subsection (Decision §5) summarising the delivered Phase-4A authorization primitives and the proposed `DEC-TEN-0023`–`DEC-TEN-0026` (platform HTTP authentication + server-owned plane selection; authenticated request-plane taxonomy resolving the F3C-4 question; authority read authorization; last-admin/self-disable/recovery policy), and expands the Phase-4 implementation guideline into gated slices 4A–4E. Proposed pending focused Phase-4 decision review; refines, does not supersede, prior decisions. |
| 1.3 | 2026-08-12 | Solution Architecture Team | Applies the decision-review corrections to §5 and approves it (DEC-TEN-0023–0026 move from Proposed to **Approved for Implementation** after the focused Phase-4 review and its narrow re-review). Corrections: distinguishes *usable platform administrative authority* from *usable platform authority* and makes bootstrap recovery eligible on loss of Administer-capable authority (refining `DEC-TEN-0019` recovery eligibility, not its genesis history); records that the platform current-session-revoke command is new 4B work with `session_id`-claim-only resolution; requires L1 serialization (isolation-assumption alternative withdrawn); adds the **4D-0** administrative-recovery-predicate sub-slice before 4D Revoke/Disable. |
