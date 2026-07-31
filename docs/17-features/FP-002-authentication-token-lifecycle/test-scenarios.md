---
document_id: FP-002-TEST
title: Authentication Test Scenarios
status: Draft
version: 0.1
---

# Test Scenarios

## Domain/Application

Invitation activation; expired/wrong-purpose action tokens; password verification and rehash; lockout; password change/reset; independent sessions; single-session and all-session revocation.

## Tenant selection

Automatic one-membership selection; explicit multiple-membership selection; inactive membership exclusion; arbitrary tenant rejection; selection transaction blocked from business APIs; one-tenant claim issuance.

## Token lifecycle

JWT issuance/validation; refresh rotation; reuse detection; idle and absolute expiry; disabled user/membership/tenant rejection; concurrent refresh; client binding; signing-key overlap.

## API/security

Generic login failure; reset enumeration prevention; rate limits; CSRF protection for cookie refresh/logout; correlation ID without secrets; only documented anonymous endpoints.

## SQL Server

Migration from empty database; unique normalized login; exact unique token hashes; retained token history; atomic refresh concurrency; restricted deletes; rowversion conflict mapping.

## Architecture

Domain/Application EF-free; no generic repository; no HR/GL dependency; no secrets in source.
