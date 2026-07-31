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

`requirements.md`, `business-rules.md`, `domain-model.md`, `authentication-model.md`, `api-contracts.md`, `data-model.md`, `acceptance-criteria.md`, `test-scenarios.md`, `decisions-approved.md`, `decisions-approved.md`, and `traceability-matrix.md`.
