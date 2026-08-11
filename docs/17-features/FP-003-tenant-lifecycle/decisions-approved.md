---
document_id: FP-003-DEC
title: Approved Tenant Lifecycle Decisions
status: Approved for Implementation
version: 1.2
sprint: Sprint-01
module: Platform
approved_date: 2026-08-01
---

# Approved Decisions

The decisions below are binding for FP-003 implementation. They preserve the existing `DEC-TEN-*` identifiers from the review package.

## DEC-TEN-0001 — Authoritative identifier

Use the existing server-generated, nonempty Guid `TenantId` as the immutable `platform.Tenants` primary key. Do not add a BIGINT Tenant key, maintain a second Tenant identifier, reuse a TenantId, or accept it from a client during creation.

## DEC-TEN-0002 — Status vocabulary

Use exactly `Provisioning`, `Active`, `Suspended`, and `Archived`. Only an existing `Active` Tenant is authentication-eligible; a missing Tenant is ineligible.

## DEC-TEN-0003 — Transition graph

Permit only Create to Provisioning, Provisioning to Active, Provisioning to Archived, Active to Suspended, Active to Archived, Suspended to Active, and Suspended to Archived. Archived is terminal. No transition is triggered automatically by time, inactivity, billing, payment, or subscription state.

## DEC-TEN-0004 — Tenant code

TenantCode is required, immutable, limited to 64 characters, trimmed, and stored with its display casing preserved. Its normalized value is exactly `Trim().ToUpperInvariant()`, with no culture-specific or provider-specific transformation. NormalizedTenantCode is globally unique and its column and unique index use the approved BIN2 collation.

## DEC-TEN-0005 — Tenant name

TenantName is required, limited to 200 characters, trimmed, and stored with its display casing preserved. It is not globally unique and has no NormalizedTenantName solely for uniqueness. TenantName is mutable only through an approved Tenant update operation; such an operation is not part of the first implementation milestone.

## DEC-TEN-0006 — Legal name

LegalName is deferred to a later legal or customer-profile feature and is not part of FP-003.

## DEC-TEN-0007 — Physical deletion

Physical Tenant deletion is prohibited. Provide no delete command, repository method, permission, endpoint, cascade, or routine database operation. Archive is the terminal retained lifecycle operation and supersedes the former Draft `DELETE /api/platform/tenants/{id}` contract.

## DEC-TEN-0008 — Platform aggregate and query filtering

Tenant is a Platform-level aggregate in the existing Platform persistence boundary. It does not implement `ITenantOwnedEntity`, receives no tenant query filter, and receives no automatic TenantId assignment from `ICurrentTenant`. Reading Tenant does not disable isolation filters on tenant-owned data.

## DEC-TEN-0009 — Lifecycle reason metadata

Persist `StatusChangedUtc`, `StatusChangedBy`, and `StatusChangeReasonCode`. The bounded reason codes are exactly `Created`, `ProvisioningCompleted`, `Administrative`, `Security`, `Compliance`, `Operational`, `CustomerClosure`, and `IssueResolved`. Creation records `Created`; every transition records a code, and Suspend and Archive require an explicit non-`Created` code. Safe events contain the code only and no free-form reason text, secrets, or billing detail.

## DEC-TEN-0010 — Already issued access tokens

A Tenant status change does not cryptographically invalidate an already issued short-lived JWT, which may remain valid until expiry. It cannot be refreshed when current Tenant status is ineligible. Tenant selection, session creation, refresh, and operations that validate current status use current eligibility. Before ordinary tenant APIs are production-enabled, a centralized current-status authorization policy must deny non-Active status. High-risk operations use a live current-status check. Middleware, JWT, cache, and invalidation implementation are outside the first milestone.

## DEC-TEN-0011 — Eligibility result shape

The result contains exactly `TenantId`, `Exists`, nullable `TenantStatus`, `IsAuthenticationEligible`, and `TenantAuthenticationIneligibilityReason`. Reason values are exactly `None`, `TenantNotFound`, `Provisioning`, `Suspended`, and `Archived`. A missing Tenant returns false Exists, null status, false eligibility, and TenantNotFound; Active returns true Exists, Active, true eligibility, and None. TenantName is omitted.

## DEC-TEN-0012 — Platform authorization permissions

Create, read, list, activate, suspend, reactivate, and archive are Platform-level operations. Ordinary tenant roles never authorize them, and Platform lifecycle authority grants no tenant business-data access. Exact permission identifiers and Platform-support authentication remain deferred; no Tenant HTTP endpoint is included in the first milestone. The eligibility query is an internal trusted Platform authentication or authorization contract, not an end-user permission decision.

> **Resolution (2026-08-10).** The deferred portion of this decision — the platform-support authentication model and the exact permission identifiers — is now resolved by `ADR-015` (Platform-Plane Authentication and Authorization) and recorded in `DEC-TEN-0018`. The platform-plane classification, the tenant-role prohibition, and the no-business-data-access boundary stated here remain unchanged.

## DEC-TEN-0013 — Legacy TenantId reconciliation

Reconciliation is staged and environment-specific. First inventory distinct legacy TenantIds and produce an operator-reviewed mapping of each TenantId to code, name, and status. Fail on missing or duplicate mappings. Never infer lifecycle state, create placeholder metadata, or silently mark a legacy Tenant Active. Commit no production data or environment-specific mapping to the repository.

## DEC-TEN-0014 — Tenant foreign keys

The first milestone creates the Tenant aggregate, `platform.Tenants`, and its own constraints but does not auto-backfill legacy TenantIds or retrofit all existing foreign keys. After approved reconciliation, a dedicated enforcement migration verifies complete coverage, fails on orphans, and adds restricted foreign keys while preserving composite same-tenant constraints. Existing candidates include TenantUsers, Roles, tenant-user role assignments, role-permission assignments where TenantId is present, and invitation-bound AccountActionTokens. Every new table introduced after FP-003—including session, tenant-authentication, and future module-root tables—has its Tenant foreign key from its first migration.

## DEC-TEN-0015 — Provisioning and first administrator

Creating a Tenant creates only the Tenant in Provisioning. Activation creates no company or first administrator. A later onboarding coordinator may compose those deferred capabilities explicitly. Active status alone does not authenticate a caller without every independent identity, membership, session, and authorization prerequisite.

## DEC-TEN-0016 — Subscription independence

Subscription, billing, and payment state are separate concepts and never implicitly change Tenant status. Any future coupling uses an explicit authorized lifecycle command.

## DEC-TEN-0017 — Immutable audit dependency

FP-003 emits safe audit-ready events through the existing post-commit dispatcher. Immutable audit storage is a separate production-release dependency and is not implemented in the first milestone.

## DEC-TEN-0018 — Platform-plane authentication and authorization

Resolves the platform-support authentication and permission deferral in `DEC-TEN-0012`, governed by `ADR-015`. Binding for the FP-003 Tenant HTTP transport milestone.

- **Security plane.** Tenant administration is platform-plane. The system has two explicit planes — tenant and platform — with distinct token shapes, permission scopes, and authorization handlers. Tenant-plane behavior (FP-005 Company, Localization, tenant IAM) is unchanged.
- **Token profile.** A platform-support principal authenticates through a dedicated platform-support token profile carrying `security_plane=platform`. The platform token carries **no** `tenant_id` and **no** `tenant_user_id`, and never fakes a system tenant, uses `Guid.Empty`, reuses the target `TenantId`, or selects an arbitrary tenant. Tenant and platform token shapes are mutually exclusive; a token combining `security_plane=platform` with `tenant_id` is rejected.
- **Permission identifiers.** `Platform.Tenants.View` (get, list), `Platform.Tenants.Manage` (create), and `Platform.Tenants.Lifecycle` (activate, suspend, reactivate, archive).
- **Permission scope.** All three are `PermissionScope.PlatformSupport`.
- **Separate platform authority.** Platform permissions are sourced only from a global, non-tenant-owned `PlatformSupportPrincipal` assignment anchored to the existing global `Identity`, independently revocable and auditable — never from tenant roles. A code/config-owned bootstrap allow-list may create only the genesis principal. Tenant administrators cannot create or modify this authority through tenant IAM.
- **Escalation invariant.** Tenant roles may hold only `PermissionScope.Tenant` permissions; `Role.AssignPermission`'s rejection of non-`Tenant` scope is a permanent security boundary. The tenant token claim path must additionally filter emitted permissions to `PermissionScope.Tenant` as defense in depth.
- **Dedicated handler.** Platform routes use `RequirePlatformPermission(...)`, enforced by a dedicated `PlatformPermissionAuthorizationHandler` requiring an authenticated principal, a validated platform token profile, and the exact `PlatformSupport` permission. It does not require `ICurrentTenant`, `TenantAuthorizationContext`, caller-tenant eligibility, or target-tenant Active status. The existing tenant handler is unchanged.
- **Target TenantId semantics.** In `/api/platform/tenants/{tenantId}` the identifier is the target aggregate only and never establishes `ICurrentTenant` or caller scope. Authorization is independent of target lifecycle status, so `Provisioning` and `Suspended` targets can be administered where the transition permits.
- **GetPlatformActor.** `ApplicationExecutionContext.GetPlatformActor` remains Application-layer defense in depth asserting a trusted actor identity exists; it is not authorization and receives no permission logic.
- **Production strong authentication.** Mandatory MFA/strong authentication is a production-readiness requirement for platform-support token issuance; the Production issuance flow must not enable platform-support access without it. It does not block implementing the platform-plane architecture in development/test.
- **Audit posture unchanged.** `DEC-TEN-0017` is unchanged: FP-003 emits safe audit-ready post-commit events; immutable audit storage remains a production-release dependency and is not a per-mutation runtime gate.

## DEC-TEN-0019 — Platform-support genesis bootstrap

Governed by `ADR-016`. Resolves how the first (and recovery) platform-support authority is trusted before any platform authorization layer exists. Not implemented yet.

- **Trust source.** A configuration-owned bootstrap allow-list keyed by the immutable `AuthenticationSubject` authorizes only the genesis/recovery creation of platform authority. Email, username, display name, `TenantId`, and `IdentityId` are rejected as configuration keys.
- **Prerequisite identity.** The configured subject must resolve to an existing `Identity` with an authentication-capable, active `AuthenticationAccount`. Bootstrap never creates identities.
- **Capability.** Bootstrap may only register/resolve the platform-support principal and establish the initial approved `PermissionScope.PlatformSupport` set. It is never per-request authorization, an implicit super-admin, a tenant-admin path, or a bypass around normal platform authorization once usable authority exists. No tenant role can invoke it.
- **Usable platform authority (definition).** Usable platform authority exists when at least one `PlatformSupportPrincipal` simultaneously (1) has `Status == Active`, (2) is anchored to a valid authentication-capable identity/account, and (3) has at least one active assignment whose permission exists in `IPermissionCatalog` with `PermissionScope.PlatformSupport`. A revoked assignment, a corrupt tenant-scoped row, an unknown permission, and a `Disabled` principal do not count.
- **Lifetime and recovery.** Bootstrap is configuration-controlled, genesis/recovery-only, audited, non-tenant-editable, and inert once usable authority exists. Recovery is eligible only when no usable authority exists; bootstrap must not implicitly re-enable a `Disabled` principal — config membership is never "always authorized" or re-enable authority.
- **Live evaluation.** "No usable platform authority exists" is evaluated live against persisted current state (principal `Status`, authentication-account eligibility, active persisted assignments, current `IPermissionCatalog` scope). It is never inferred from configuration, a cached bootstrap-success flag, the mere presence of a principal row, or corrupt assignment rows.
- **Subject cardinality and deterministic selection.** `PlatformSupportBootstrapOptions` may contain multiple unique configured `AuthenticationSubject` values. Bootstrap does not create a principal for every configured subject; a single evaluation establishes exactly one usable genesis/recovery principal. Eligible subjects (canonical, unique, resolving to an existing `Identity` with an eligible `AuthenticationAccount`, and — for a create — not already owning a principal) are sorted by ordinal comparison of the canonical `AuthenticationSubject`; the first is selected (never configuration insertion order). Remaining configured subjects stay recovery candidates and receive no authority automatically.
- **Concurrent convergence.** Concurrent instances evaluate usable authority live, deterministically select the same first eligible subject, and are bounded by the Phase-2 uniqueness (`UX_PlatformSupportPrincipals_IdentityId`, active-assignment unique index); duplicates are idempotent race outcomes. For the same configuration and persistence state, a bootstrap race establishes exactly one genesis/recovery principal, not one per instance. No distributed lock is introduced.
- **Recovery model.** Recovery never changes `Disabled → Active` and never grants to a `Disabled` principal. It may create a new `Active` recovery principal only for an eligible configured subject that owns no principal. If a `Disabled` principal is the only configured subject with no other eligible candidate, bootstrap fails closed (no re-enable, no duplicate) and emits an operator diagnostic; recovery then requires an additional approved pre-existing `AuthenticationSubject` in configuration or the separately-authorized explicit Re-enable lifecycle operation. An `Active` principal with no active catalog-valid `PlatformSupport` assignment is not usable authority; recovery may establish an eligible configured candidate without mutating the existing principal's assignments. Manual SQL is a governed break-glass activity only, not the designed recovery mechanism.
- **Audit and failure.** Bootstrap reuses the Phase-2 audit fields with a distinguishable actor representation (e.g. `platform-bootstrap:<subject>`); no immutable-audit infrastructure is invented. Missing subject / ineligible account → no authority; existing principal → idempotent; existing assignment → no duplicate; usable authority already exists → no-op; unknown or tenant-scoped permission → fail closed.

## DEC-TEN-0020 — Platform-support principal lifecycle

Governed by `ADR-016`. Adds a minimal principal status so platform authority can be suspended immediately and re-enabled without deleting history or over-broadly disabling the person's tenant access. Not implemented yet.

- **States.** `PlatformSupportPrincipalStatus { Active, Disabled }`, default `Active`; transitions `Active → Disabled` and `Disabled → Active` (both non-terminal). No `Suspended`/`Archived`/`Deleted` at this stage.
- **Semantics.** `Active` evaluates authority from active assignments. `Disabled` makes all platform authority unusable regardless of retained assignments; assignments remain persisted (not deleted, not revoked). Grant while `Disabled` is rejected; revoke while `Disabled` is allowed. Re-enable restores eligibility of still-active retained assignments (it does not recreate revoked ones).
- **Concurrency and audit.** The existing Phase-2 `RowVersion` is authoritative for lifecycle optimistic concurrency. Persist `StatusChangedUtc`/`StatusChangedBy` in addition to `ModifiedUtc/By`; no reason-code is introduced.
- **Migration and backfill.** `Status` is added `NOT NULL` (`nvarchar`, `BIN2` collation, `CHECK Status IN ('Active','Disabled')`, default `'Active'`); `StatusChangedUtc` and `StatusChangedBy` are `NULLABLE`. Any principal existing before the status migration backfills to `Status = 'Active'` with `StatusChangedUtc = NULL` and `StatusChangedBy = NULL` — a schema addition is not a lifecycle transition, so no historical transition is synthesized from `CreatedUtc`/`CreatedBy`. The first actual `Disable`/`Re-enable` populates both, and every subsequent transition overwrites them.
- **Token consequences.** Platform token issuance and refresh perform a live principal-status check (mirroring `ITenantAuthenticationEligibilityReadService`) and are denied for `Disabled`; no token-carried status is authoritative. Disabling does not cryptographically invalidate an already-issued short-lived JWT (consistent with `DEC-TEN-0010`); an existing access token may remain usable until natural expiry or until the `SecurityVersion` + session-revocation mechanism (`ADR-015`) applies. `StrictAccessTokenValidator` validates the platform profile structurally and performs no live DB status lookup. Whether the platform session reuses `AuthenticationAccount.SecurityVersion` + `AuthenticationSession` revocation or a distinct platform-session version is a Phase-3C detail (resolved by `DEC-TEN-0022`: a separate `PlatformAuthenticationSession` reusing the global `AuthenticationAccount.SecurityVersion`); `PlatformSupportPrincipal` is not assigned its own `SecurityVersion` here.

## DEC-TEN-0021 — Platform-authority administration permission

Governed by `ADR-016`. Defines the permission that governs administering platform authority, so tenant permissions are never repurposed. Not implemented yet.

- **New permission.** `Platform.Support.Administer`, `PermissionScope.PlatformSupport`, governing platform-support principal registration, permission grant, permission revoke, and principal Disable/Re-enable. A single permission covers this small surface; no additional permissions are introduced.
- **Non-repurposing.** `Platform.Tenants.Manage` and `Platform.Tenants.Lifecycle` govern Tenant administration and lifecycle and never authorize administering `PlatformSupportPrincipal` or `PlatformPermissionAssignment`.
- **Bootstrap exception.** Bootstrap is the only exception, and only before usable platform authority exists. Once it exists, Register/Grant/Revoke/Disable/Re-enable require `Platform.Support.Administer` through the future platform-plane authorization layer. The genesis/recovery principal receives `Platform.Support.Administer` in its initial authority set (once authored) to enable the Phase-4 transition to normal self-hosting administration, and may also receive explicitly configured, approved initial `Platform.Tenants.*` permissions; the bootstrap grant set contains only code-catalog permissions with `PermissionScope.PlatformSupport`.
- **Catalog impact.** When authored, the `PlatformSupport` catalog grows from three to four entries. Being `PlatformSupport`-scoped, `Platform.Support.Administer` is excluded from tenant-facing catalog listings and tenant-token claims by the Phase-1 filters; the platform authority read path returns it only when legitimately assigned.

## DEC-TEN-0022 — Platform authentication session representation and cross-plane refresh isolation

Governed by `ADR-016` (Phase-3C session-profile subsection). `ADR-016` DEC-TEN-0020 explicitly deferred *whether the platform session reuses `AuthenticationAccount.SecurityVersion` + `AuthenticationSession` revocation or introduces a distinct model* to Phase 3C; this decision resolves it before any Phase-3C production code is written. It does not change any already-approved decision. Not implemented yet.

**Context.** The existing `AuthenticationSession` aggregate is structurally tenant-bound: its constructor rejects `TenantUserId <= 0` and `TenantId == Guid.Empty`, both columns are `NOT NULL` with foreign keys to `Tenant` and the composite `(TenantId, TenantUserId)` → `TenantUser`, every session domain event carries `TenantId`/`TenantUserId`, and session-limit/locator queries key on `IdentityId`. A platform-support principal legitimately has no tenant and no tenant user, so it cannot be represented by that aggregate without weakening the tenant invariants that protect the tenant plane.

- **Separate aggregate (binding).** Platform authentication uses a new, separate `PlatformAuthenticationSession` aggregate and persistence table. The tenant `AuthenticationSession` aggregate, table, refresh-token records, foreign keys, events, and queries remain **unchanged and tenant-bound**. `TenantId`/`TenantUserId` are **not** made nullable, and **no** generic plane discriminator is added to the tenant table by weakening its tenant invariants. Structural separation — not a nullable flag — provides cross-plane isolation.
- **Ownership.** `PlatformAuthenticationSession` is a global platform-plane record. It is **not** `ITenantOwnedEntity` and **not** `ICompanyOwnedEntity`, receives no tenant query filter, and **must not** contain `TenantId`, `TenantUserId`, or `CompanyId`. It is anchored to `IdentityId` **and** `PlatformSupportPrincipalId`; both are required.
- **Why store `PlatformSupportPrincipalId`.** Although `IdentityId` maps to at most one principal (`UX_PlatformSupportPrincipals_IdentityId`), the principal id is stored explicitly for explicit authority binding, targeted revocation on principal Disable, audit clarity, structural FK integrity, and to avoid repeated identity→principal inference during session operations. No duplicate authority model is created; the assignments remain the sole authority source.
- **Security version.** `PlatformAuthenticationSession.SecurityVersionAtCreation` snapshots the global `AuthenticationAccount.SecurityVersion` — the same account version tenant sessions use. `PlatformSupportPrincipal` is **not** assigned its own `SecurityVersion` (consistent with `ADR-016` DEC-TEN-0020); principal Disable is a separate platform-plane state, not a version bump (bumping the global account version would also kill the person's tenant access).
- **Conceptual fields.** Following existing `AuthenticationSession` conventions where applicable: `PlatformAuthenticationSessionId`, `IdentityId`, `PlatformSupportPrincipalId`, `ClientId`, `TokenFamilyId`, `SecurityVersionAtCreation`, `Status`, `CreatedUtc`, `LastRefreshedUtc`/last-activity, `IdleExpiresUtc`, `AbsoluteExpiresUtc`, `RevokedUtc`, `RevokedBy`, `RevocationReason`, compromise metadata, `RowVersion`, plus a session-owned refresh-token relationship. Exact repository-conventional names are an implementation detail; the ownership/profile fields (no tenant fields; `IdentityId` + `PlatformSupportPrincipalId` required; account-sourced `SecurityVersionAtCreation`) are binding.
- **Refresh-token storage.** Platform refresh-token records are stored in platform-session persistence (a platform-specific child, e.g. `PlatformRefreshTokenRecord`), separate from tenant `RefreshTokenRecord` rows. Tenant refresh-token rows are **not** made polymorphic to save a table.
- **Refresh plane binding (security-critical).** A platform refresh token resolves **only** against platform-session persistence; a tenant refresh token resolves **only** against tenant-session persistence. The plane is persistence-owned and server-owned; **no request parameter** selects tenant or platform for an existing refresh token.
- **Cross-plane switching.** Tenant refresh token → platform token is **rejected**; platform refresh token → tenant token is **rejected**. A refresh token never changes plane. A user may separately establish a session in another plane only through that plane's own approved authentication flow.
- **Token profile (restates `ADR-015`/DEC-TEN-0018).** The platform access token carries `security_plane=platform` exactly once and forbids `tenant_id`, `tenant_user_id`, `role`, `company_id`, any principal-status claim, and any bootstrap/config claim. Required application claims: `identity_id`, `session_id`, `client_id`, `security_version`, `security_plane=platform`, plus one or more active catalog-valid `PermissionScope.PlatformSupport` permission claims. The tenant profile is unchanged: `tenant_id` present, `security_plane` absent or exactly `tenant`; legacy tenant tokens without the claim remain valid; **no tenant-issuer change is required in Phase 3C**.
- **Zero-permission behavior (binding, derived from DEC-TEN-0019).** A principal with zero active catalog-valid `PermissionScope.PlatformSupport` permissions is not usable authority and is not eligible for a platform session/token. Issuance is denied; refresh is denied, the current platform session is revoked, and no new token is issued.
- **Disabled principal (binding, restates DEC-TEN-0020).** At issuance, `Status == Disabled` → deny. At refresh, live principal status is re-read; `Disabled` → deny refresh, revoke the current platform session, issue no token. An already-issued short-lived platform access JWT is **not** cryptographically invalidated by DB status alone and may remain usable until natural expiry; `StrictAccessTokenValidator` stays stateless.
- **Proactive revocation on Disable.** When a `PlatformSupportPrincipal` is explicitly Disabled, all active `PlatformAuthenticationSession`s belonging to that principal are revoked as part of the platform lifecycle/application workflow (platform-only effect; refresh is blocked immediately; tenant sessions are untouched). This extends nothing in DEC-TEN-0020's guaranteed behavior: it does **not** invalidate already-issued stateless access JWTs, which still expire naturally. The refresh-time deny+revoke guarantee of DEC-TEN-0020 holds independently. The proactive revocation is integrated in implementation slice 3C-4 alongside the platform-session capability; exposing the Disable operation over HTTP remains Phase 4.
- **Re-enable.** `Disabled → Active` re-enable does **not** reactivate revoked `PlatformAuthenticationSession`s; revoked sessions stay revoked, and a newly eligible principal must establish a new platform session.
- **Account Disable / SecurityVersion.** `AuthenticationAccount.Disable` is global; a live `SecurityVersion` mismatch invalidates continuation in both tenant and platform refresh flows under existing refresh rules. The global `SecurityVersion` is **not** the platform-only disable mechanism (it would kill tenant access); platform-only cutoff is platform-session revocation.
- **Session limits.** Platform session-limit accounting is separate from tenant accounting because the stores are separate. If `AuthenticationPolicy.MaximumActiveSessions` is reused, it applies independently within platform-session persistence; tenant sessions are never counted against platform sessions or vice versa. No new platform-specific numeric limit is introduced.
- **Revocation reasons.** The platform session uses the applicable session reasons — `SecurityStateChanged`, `IdentityIneligible`, `Administrative`, `UserLogout`, `SessionLimitExceeded`, refresh reuse/compromise — plus a platform-specific `PlatformPrincipalIneligible` (exact naming to follow the current enum/CHECK convention) covering Disabled-principal and zero-permission continuation denial. Tenant-specific `MembershipIneligible`/`TenantIneligible` are **not** carried into the platform set; no new shared reason hierarchy is created.
- **Persistence impact.** Phase 3C requires an **additive** migration introducing new structures only — `platform.PlatformAuthenticationSessions` and a platform refresh-token structure. Existing tenant `AuthenticationSessions`/`RefreshTokenRecords` schema is **not** changed. Foreign keys: `PlatformAuthenticationSession.IdentityId` → `Identity` and `PlatformAuthenticationSession.PlatformSupportPrincipalId` → `PlatformSupportPrincipal`, both `OnDelete(Restrict)`; the refresh-token child is session-owned following the retained-history convention.
- **Physical-delete protection.** Consistent with the existing `PreventAuthenticationHistoryDeletion` guard (which retains `AuthenticationSession`/`RefreshTokenRecord`) and the Phase-2 authority-retention guards, `PlatformAuthenticationSession` and its refresh-token history are retained security records and must be protected from physical deletion (revoke/soft-state only).
- **Concurrency.** `PlatformAuthenticationSession` mirrors the existing session concurrency convention with a single `RowVersion` concurrency token; refresh rotation/revocation remain concurrency-safe. No second concurrency token is introduced.
- **Refresh security model.** Platform refresh: resolve the opaque locator only in the platform store; lock the session per the existing pattern; verify usable; verify account `IsAuthenticationEligible`; verify live `account.SecurityVersion == SecurityVersionAtCreation`; verify the `PlatformSupportPrincipal` still exists and `Status == Active`; re-read catalog-valid active `PlatformSupport` permissions; on zero permissions or `Disabled` → revoke + deny; rotate the refresh token under the existing compromise/reuse semantics; issue a new platform token. No stale permission snapshot is reused.
- **Refresh reuse/compromise.** Platform reuse detection marks the platform session compromised/revoked under the existing model, as a parallel platform implementation. It does **not** affect tenant sessions unless account-level global compromise policy already does; no cross-plane compromise propagation is invented.
- **StrictAccessTokenValidator (structural).** `security_plane` absent → tenant legacy branch; `security_plane == "tenant"` → tenant branch; `security_plane == "platform"` → platform branch; anything else → reject. Platform branch requires exactly one of `iss, aud, sub, jti, iat, nbf, exp, identity_id, session_id, client_id, security_version, security_plane`; requires zero `tenant_id`, `tenant_user_id`, `role`, and `company_id`; requires non-blank, duplicate-free `permission`. **No DB lookup and no live principal-status lookup.**
- **Claims provider / issuer separation.** The tenant `AccessTokenClaimsProvider` remains tenant-only and unchanged. Phase 3C introduces a distinct `PlatformAccessTokenClaimsProvider` (exact name per repository convention) sourcing permissions only from `IPlatformSupportPermissionReadService` (no caller-provided list). The issuer exposes a typed platform issue path (e.g. `Issue(PlatformAccessTokenClaims …)`), never `IssueToken(bool isPlatform)` / `IssueToken(string plane)`; the security plane is selected by the server-side type/path, not caller data.
- **Session creation trust source.** Platform-session creation is an Application-level operation that consumes trusted authentication output (`VerifiedIdentity` or the exact existing verified-account context) and never an arbitrary caller-supplied `IdentityId`. Flow: verified identity → live account eligibility → resolve platform principal → `Status == Active` → ≥1 catalog-valid permission → create `PlatformAuthenticationSession` → issue platform access/refresh pair. **No HTTP route in Phase 3C.**
- **Bootstrap transition.** After Phase 3B has persisted platform authority, token issuance uses only `Identity`, `AuthenticationAccount`, `PlatformSupportPrincipal`, and `PlatformPermissionAssignment`. Bootstrap subject lists/configuration do **not** participate in token issuance.
- **Rejected alternatives.** (a) Reusing `AuthenticationSession` with nullable `TenantId`/`TenantUserId` or a plane discriminator — rejected: weakens the mature tenant aggregate's invariants and makes cross-plane isolation depend on a runtime check a bug could bypass. (b) A single dual-plane session/claims/issuer path — rejected: concentrates both planes' security in one type where a future edit leaks tenant relaxations into the platform path. (c) A distinct `PlatformSupportPrincipal.SecurityVersion` — rejected by DEC-TEN-0020.
- **Consequences.** Structural cross-plane isolation (a refresh token can never resolve across planes); zero blast radius on the tenant session schema; a platform-only immediate cutoff via platform-session revocation; a small additive migration and a stronger security-test matrix (schema shape, cross-plane lookup, disable/zero-permission/reuse). Phase-4 request authorization (`PlatformPermissionAuthorizationHandler`, `RequirePlatformPermission`, Host policy, admin exposure) remains out of scope.
- **Implementation slices.** 3C-1 claims/profile abstractions + `PlatformAccessTokenClaimsProvider` + typed issuer path; 3C-2 `StrictAccessTokenValidator` platform branch + regression/mixed-plane tests; 3C-3 `PlatformAuthenticationSession` domain/persistence + additive migration + SQL schema tests; 3C-4 platform session creation + refresh/rotation/revocation + proactive-revoke-on-Disable + SQL tests; 3C-5 full validation/architecture regression.
- **Test implications.** Acceptance criteria `AC-TEN-0054`–`AC-TEN-0077` and test scenarios `TS-TEN-0093`–`TS-TEN-0117` (SQL schema/FK/create/refresh/mismatch/disable/zero-permission/reuse/multi-session/tenant-unaffected/cross-plane-lookup/re-enable/physical-delete, plus validator profile/mixed-plane/legacy scenarios).

## Reconciled conflict register

| # | Prior conflict | Approved resolution |
|---|---|---|
| 1 | Draft Tenant Management mixed lifecycle with company, subscription, branding, localization, and notifications | FP-003 is authoritative for lifecycle and authentication eligibility; the other capabilities remain deferred and non-authoritative |
| 2 | Draft physical-delete route and permission | Superseded by terminal Archive under DEC-TEN-0007 |
| 3 | Draft TenantName uniqueness | Superseded by non-unique TenantName under DEC-TEN-0005 |
| 4 | General BIGINT guidance versus established Guid TenantId | Existing Guid TenantId remains authoritative under DEC-TEN-0001 |
| 5 | Existing tenant-owned Platform tables lack a Tenant principal foreign key | Use the staged enforcement process in DEC-TEN-0014 |
| 6 | Legacy TenantIds lack trustworthy lifecycle metadata | Use operator-reviewed, environment-specific reconciliation under DEC-TEN-0013 |
| 7 | JWTs may remain cryptographically valid after suspension | Current-state authorization requirements are fixed by DEC-TEN-0010 |
| 8 | Platform-support permissions are not defined | First milestone stayed internal under DEC-TEN-0012; the platform-support authentication model and permission identifiers are now resolved by ADR-015 and DEC-TEN-0018 |
| 9 | Broad provisioning expected a company and first administrator | Tenant creation remains Provisioning-only under DEC-TEN-0015 |
| 10 | Draft associated subscription with lifecycle | Lifecycle is independent under DEC-TEN-0016 |
| 11 | Immutable audit storage does not yet exist | Emit safe events and retain the production dependency under DEC-TEN-0017 |
| 12 | FP-001 and FP-002 required Tenant status without an implementation source | Approved FP-003 is the source consumed by those workflows |
| 13 | Draft documentation lacked an exact status vocabulary and graph | DEC-TEN-0002 and DEC-TEN-0003 define both exactly |
| 14 | The first platform-support authority could not be created without a trusted, non-circular, non-tenant mechanism | Genesis/recovery config bootstrap keyed by immutable AuthenticationSubject under DEC-TEN-0019 and ADR-016 |
| 15 | A platform-support principal had no way to be suspended immediately without over-broadly disabling all access | Minimal `Active`/`Disabled` principal status with live status checks under DEC-TEN-0020 and ADR-016 |
| 16 | No permission governed administering platform authority; `Platform.Tenants.Manage` risked being repurposed | New `Platform.Support.Administer` permission under DEC-TEN-0021 and ADR-016 |
| 17 | The tenant `AuthenticationSession` is structurally tenant-bound and cannot represent a non-tenant platform session; ADR-016 deferred the platform session representation to Phase 3C | Separate `PlatformAuthenticationSession` aggregate/table with structural cross-plane refresh isolation under DEC-TEN-0022 and ADR-016 |
