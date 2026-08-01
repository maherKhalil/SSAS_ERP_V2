---
document_id: FP-002
title: Authentication and Token Lifecycle
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
depends_on: [FP-001, FP-003, ADR-006]
---

# Feature Package 002 — Authentication and Token Lifecycle

## Purpose

Define password authentication, account setup, post-authentication tenant selection, JWT access tokens, refresh-token rotation, sessions, logout, revocation, password reset, lockout, and future MFA compatibility.

## Confirmed baseline

- JWT bearer authentication with claims-based authorization.
- Short-lived access tokens and revocable rotating refresh tokens.
- Exactly one tenant per tenant-scoped access token.
- Automatic selection when one active membership exists; explicit selection when several exist.
- Disabled users and suspended tenants cannot obtain usable tenant access.
- Passwords and token secrets are never stored in plaintext.
- Authentication remains outside HR and GL.

## Status

Approved for implementation. The authoritative decisions are recorded in `decisions-approved.md`.

## Documents

`requirements.md`, `business-rules.md`, `domain-model.md`, `authentication-model.md`, `api-contracts.md`, `data-model.md`, `acceptance-criteria.md`, `test-scenarios.md`, `decisions-approved.md`, and `traceability-matrix.md`.

## Sprint-01 Milestone 2 boundary

Milestone 2 implements only the credential and account-action core:

- `AuthenticationAccount`;
- `AccountActionToken`;
- invitation and initial account setup core;
- password credential verification without token issuance;
- failed-attempt and lockout behavior;
- password-reset core;
- password hashing, verification, and rehash outcomes;
- secure action-token generation;
- persistence through the existing `PlatformDbContext` and `IPlatformUnitOfWork`;
- SQL Server migrations and automated tests.

Milestone 2 does not implement `AuthenticationSession`, `RefreshTokenRecord`, JWT issuance, tenant-selection transactions, HTTP authentication endpoints, cookies, CSRF, RS256 signing, logout/session APIs, Angular authentication, immutable audit storage, or App Owner/App Support authentication.

The authoritative Milestone 2 clarifications are `DEC-AUTH-0023` through `DEC-AUTH-0031` in `decisions-approved.md`.

## Sprint-01 Milestone 3 boundary

Milestone 3 implements the internal tenant-selection and authentication-session persistence core:

- internal `VerifiedIdentity` capability after successful credential verification;
- dedicated pre-tenant membership discovery owned by the verified Identity;
- authoritative FP-003 Tenant eligibility through `ITenantAuthenticationEligibilityReadService`;
- automatic resolution for one eligible membership and persisted five-minute selection proof for several;
- exact allowlisted V1 ClientId `ssas-erp-web`;
- AuthenticationSession with Active, Revoked, and Compromised status;
- RefreshTokenRecord children, exact selector/secret format, rotation history, reuse detection, and compromise;
- 30-day idle and 90-day absolute session lifetimes;
- maximum ten active sessions with deterministic oldest-session revocation;
- password-reset integration that revokes all active sessions;
- SQL Server transaction and lock-order concurrency controls;
- safe session, selection, reuse, and session-limit events;
- persistence through the existing PlatformDbContext and IPlatformUnitOfWork;
- `AddAuthenticationSessionsAndTenantSelection` and focused Domain, Application, SQL Server, security, and architecture tests.

Milestone 3 does not implement authentication HTTP endpoints, access-token/JWT issuance, RS256 keys, claims construction, ASP.NET Core authentication changes, cookies, CSRF, endpoint rate limiting, Angular authentication, public logout or session-management endpoints, authenticated password change, notification delivery, immutable audit storage, Platform-support authentication, MFA, external providers, or native clients.

The authoritative Milestone 3 clarifications are `DEC-AUTH-0032` through `DEC-AUTH-0047` in `decisions-approved.md`.

## Sprint-01 Milestone 4 boundary

Milestone 4 implements the authentication transport-security slice:

- exactly `POST /api/platform/auth/login`, `/select-tenant`, `/refresh`, and `/logout` with fixed server-side `ssas-erp-web` ClientId and generic approved Problem Details;
- default 15-minute RS256 access tokens with the exact approved claims, 8192-byte compact limit, permission-primary authorization, strict validation, and no symmetric fallback;
- deployment-mounted X.509 signing and rollover verification certificates, derived `kid`, 30-second clock skew, startup validation, and overlap;
- the exact secure rotating refresh cookie and signed session-and-refresh-bound Data Protection CSRF cookie/header design;
- exact Origin, restrictive CORS, direct/trusted-proxy, and endpoint-specific zero-queue rate-limit policies, including required shared production enforcement;
- centralized live FP-003 Active-Tenant authorization for every ordinary tenant request;
- access-token issuance before SQL commit, success-event dispatch after commit, cookie writes after commit, and explicit transport ambiguity;
- current-session-only logout with `UserLogout` and no logout-all;
- selection summaries containing only TenantId, TenantUserId, and FP-003 TenantName as TenantDisplayName;
- no-store response controls, sensitive logging prohibitions, and exact OpenAPI contracts;
- only the constraint migration `AddUserLogoutSessionRevocationReason`, with no new persistence table.

The authoritative Milestone 4 clarifications are `DEC-AUTH-0048` through `DEC-AUTH-0063` in `decisions-approved.md`.

Deferred beyond Milestone 4 are Angular authentication, browser route guards and frontend token storage implementation, session listing, revoke-another-session, logout-all, authenticated password change, password-reset/invitation email delivery and public routes, Platform-support authentication, MFA, external identity providers, OAuth/OIDC provider behavior, JWKS, native/mobile clients, service authentication, API keys, impersonation, notification delivery, immutable audit storage, full live validation of all non-Tenant authorization state on every ordinary request, and high-risk business-operation policies beyond the reusable foundation.
