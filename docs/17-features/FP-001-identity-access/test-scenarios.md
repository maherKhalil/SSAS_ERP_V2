---
document_id: FP-001-TEST
title: Identity and Access Test Scenarios
status: Approved
version: 1.0
---

# Test Scenarios

## Domain and application

- `TS-IAM-0001`: create a tenant user and verify immutable tenant ownership.
- `TS-IAM-0002`: reject duplicate email inside the same tenant.
- `TS-IAM-0003`: allow the same email in a different tenant.
- `TS-IAM-0004`: assign multiple same-tenant roles.
- `TS-IAM-0005`: reject duplicate role assignment.
- `TS-IAM-0006`: reject cross-tenant role assignment.
- `TS-IAM-0007`: resolve distinct permissions from multiple roles.
- `TS-IAM-0008`: prove role name alone grants no permissions.
- `TS-IAM-0009`: deactivate and reactivate a tenant user.
- `TS-IAM-0010`: verify no physical-delete operation exists.
- `TS-IAM-0011`: reject retirement when any active user is assigned.
- `TS-IAM-0012`: allow retirement after active assignments are removed or affected users are deactivated.
- `TS-IAM-0013`: reject new assignment to retirement-pending or retired role.
- `TS-IAM-0014`: preserve role audit history after retirement.
- `TS-IAM-0015`: reject stale concurrency version.

## Tenant selection

- `TS-IAM-0020`: automatically select one active membership.
- `TS-IAM-0021`: require selection for multiple active memberships.
- `TS-IAM-0022`: exclude deactivated memberships from selection.
- `TS-IAM-0023`: reject selecting a tenant without active membership.
- `TS-IAM-0024`: verify issued token contains exactly one tenant claim.

## API authorization

- `TS-IAM-0030`: unauthenticated protected request returns 401.
- `TS-IAM-0031`: authenticated request without permission returns 403.
- `TS-IAM-0032`: request input cannot override trusted tenant.
- `TS-IAM-0033`: tenant administrator cannot query another tenant.
- `TS-IAM-0034`: platform-support actor can perform an explicitly authorized support operation.
- `TS-IAM-0035`: tenant Administrator role does not grant platform-support access.
- `TS-IAM-0036`: support action records actor, target tenant, UTC time, and operation.

## Persistence and security

- `TS-IAM-0040`: `(TenantId, Email)` uniqueness is enforced.
- `TS-IAM-0041`: tenant filters use current context instance state.
- `TS-IAM-0042`: user deletion is not exposed or executed.
- `TS-IAM-0043`: role/user assignments enforce same tenant.
- `TS-IAM-0044`: audit values use UTC.
- `TS-IAM-0045`: logs contain no secrets or raw tokens.
- `TS-IAM-0046`: Domain and Application remain EF Core-free.
- `TS-IAM-0047`: no generic repository exists.
- `TS-IAM-0048`: Platform does not depend on HR or GL.

## Concurrency (T-062)

- `TS-IAM-0049`: a stale `ExpectedRowVersion` is rejected as a concurrency conflict and the unit of
  work is never asked to save.
- `TS-IAM-0050`: `TenantUser.RowVersion` is a concurrency token in the built model, which is the half
  of `AC-IAM-0022` the database enforces.
