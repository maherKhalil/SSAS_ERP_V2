---
document_id: FP-005-LIFECYCLE
title: Company / Legal Entity Lifecycle Model
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Lifecycle Model

> Approved for Implementation — model reflecting the approved human decisions.

## Status semantics

### Inactive

The initial state of every created Company, and the disabled state a company returns to when deactivated. An `Inactive` company exists and is administrable but is not marked available/ready for use. Its records and history remain intact. It may be activated or archived.

### Active

`Active` is the explicit readiness / availability state. A company is `Active` only after an explicit activation. `Active` grants no membership, permission, or cross-tenant access; when future company-owned modules exist, their own rules determine what an `Active` company enables. `Active` is never the state of a just-created company.

### Archived

The Company is permanently retired, retained for history, and unable to transition again.

## Initial-state decision

Creation begins in `Inactive`, not `Active` and not a provisioning state. `Active` is treated as an explicit availability decision distinct from mere existence: creating directly as `Active` would imply readiness before an administrator — and, in future, HR/GL configuration prerequisites — has made the company ready, creating semantic debt. A dedicated `Provisioning`/`Draft` state is deliberately **not** introduced in Milestone 1; the existing two-state `Inactive`/`Active` model expresses "created but not yet available" with `Inactive`.

## Transition matrix

| Current | Operation | Next | Reason code |
|---|---|---|---|
| None | Create | Inactive | `Created` |
| Inactive | Activate | Active | non-`Created` |
| Active | Deactivate | Inactive | non-`Created` |
| Active | Archive | Archived | non-`Created` |
| Inactive | Archive | Archived | non-`Created` |
| Archived | Any transition | Rejected | — |

All unlisted transitions are rejected, including activating an already-`Active` company, deactivating an already-`Inactive` company, and any transition out of `Archived`.

## Enablement is a single reversible pair

`Activate` (`Inactive` to `Active`) and `Deactivate` (`Active` to `Inactive`) are the two directions of one reversible enablement pair. `Activate` serves both the first enablement of a newly created company and the re-enablement of a deactivated one; because Company has no separate provisioning state, a distinct `Reactivate` command/route is not defined. This keeps the enablement language consistent and avoids a redundant command.

## Lifecycle reason codes

Creation records `Created`. Every later transition requires one of `Administrative`, `Operational`, `Compliance`, `CustomerRequest`, or `IssueResolved`; `Created` is invalid after creation. Activate, Deactivate, and Archive callers must each provide an explicit non-`Created` code. Actor and UTC metadata come from trusted application context, and events contain only the bounded reason code — never free-form reason text.

## Creation

`CreateCompany`:

1. receives company code, company name, and base currency code;
2. validates and normalizes the code, trims the name, and validates the ISO-4217 base currency;
3. verifies normalized company-code uniqueness within the current tenant;
4. generates a nonempty Guid `CompanyId` server-side;
5. adopts the trusted current `TenantId` (server-assigned; never client-supplied);
6. creates the aggregate in `Inactive`;
7. records trusted UTC and actor metadata and reason code `Created`;
8. raises `CompanyCreated`;
9. persists through the existing Platform Unit of Work.

Creation provisions no user, no fiscal calendar, no chart of accounts, no numbering sequence, and no additional currency. A created company is `Inactive` and must be explicitly activated before it is available.

## Update profile

`UpdateCompanyProfile` changes only the mutable `CompanyName`, using optimistic concurrency. `CompanyCode`, `BaseCurrencyCode`, `TenantId`, `CompanyId`, and `Status` are not updatable through this operation. A successful update raises `CompanyProfileUpdated`.

## Activation and deactivation

Activation is accepted only from `Inactive`; deactivation only from `Active`. Each is explicit, carries a non-`Created` reason code, uses optimistic concurrency, and becomes authoritative only after successful persistence. Activation and deactivation change only Company lifecycle state; they do not create or remove any other record.

## Archive

Archive is accepted from `Active` or `Inactive` and is terminal. It does not erase or anonymize history. Any future privacy or legal-erasure workflow requires separate requirements that preserve mandatory ERP and audit references.

### Archive eligibility extensibility

In Milestone 1 there is no additional archive prerequisite. As dependent modules such as HR and GL are introduced, archive eligibility may acquire additional **module-owned** prerequisite checks — for example active employees, open accounting periods, or posted/unsettled accounting dependencies. Those checks are not part of Milestone 1 and are not encoded in the Platform Company Domain. When introduced, they must be evaluated through approved published module contracts/queries (or another architecture-approved boundary); the Platform Company Domain must never directly reference HR or GL Domain types, and the Milestone 1 transition graph is unchanged (`BRULE-CMP-0018`, `DEC-CMP-0027`).

## Concurrency and event timing

- Every status-changing or profile-changing command supplies an expected rowversion.
- A stale rowversion returns a conflict and raises no committed event.
- Status changes, name changes, metadata, and events persist in one Unit of Work.
- Events are dispatched only after successful commit.
- No automatic retry may silently apply a command to a newer state; the caller must reread and deliberately retry.

## No time-driven status

Expiration, inactivity, unpaid billing, or subscription dates do not automatically mutate Company status. Any future integration must issue an explicit authorized lifecycle command and preserve its own source decision separately.
