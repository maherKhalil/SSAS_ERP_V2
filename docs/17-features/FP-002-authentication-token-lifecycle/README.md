---
document_id: FP-002
title: Authentication and Token Lifecycle
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
depends_on: [FP-001, ADR-006]
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
