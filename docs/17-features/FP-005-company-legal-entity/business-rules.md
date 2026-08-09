---
document_id: FP-005-BR
title: Company / Legal Entity Business Rules
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Business Rules

> Approved for Implementation — rules reflecting the approved human decisions.

### BRULE-CMP-0001 — Initial state

Every newly created Company begins in `Inactive`. `Active` is an explicit readiness / availability state reached only through activation; creating directly as `Active` is not permitted, so that no availability is implied before an administrator (and, in future, module configuration prerequisites) makes the company ready. No `Provisioning` or `Draft` state is introduced in Milestone 1.

### BRULE-CMP-0002 — Approved transitions

Only these transitions are permitted:

- `None` to `Inactive` (Create);
- `Inactive` to `Active` (Activate);
- `Active` to `Inactive` (Deactivate);
- `Active` to `Archived` (Archive);
- `Inactive` to `Archived` (Archive).

### BRULE-CMP-0003 — Archived is terminal

An `Archived` Company cannot transition again.

### BRULE-CMP-0004 — No automatic lifecycle transition

Time, inactivity, subscription state, billing state, or any external schedule never changes Company status automatically. Every transition is an explicit authorized command.

### BRULE-CMP-0005 — Single reversible enablement pair

Enablement is a reversible two-state pair: `Activate` performs `Inactive` to `Active`, and `Deactivate` performs `Active` to `Inactive`. `Activate` serves both the first enablement of a newly created (`Inactive`) company and the re-enablement of a deactivated company; there is no separate provisioning state, so a distinct `Reactivate` command and route are intentionally not defined.

### BRULE-CMP-0006 — No physical deletion

Company records are archived and retained. Physical deletion and delete cascades are prohibited.

### BRULE-CMP-0007 — CompanyId authority

`CompanyId` is a server-generated, nonempty Guid, is immutable, is never reused, and is the identifier future company-owned records reference.

### BRULE-CMP-0008 — Company code

Company code is required, limited to 64 characters, trimmed, display-casing preserving, normalized exactly with `Trim().ToUpperInvariant()` and no culture- or provider-specific transformation, unique **within a tenant** by normalized value, immutable after creation, and matched by a `Latin1_General_100_BIN2` column and a per-tenant unique index. It must be nonempty after trimming and must contain no control characters. Specifically:

- the 64-character length limit applies to the **normalized value** as well as to the accepted input; a value whose normalized form exceeds 64 characters is rejected;
- no Unicode NFC/NFD (or NFKC/NFKD) normalization is performed; normalization is exactly `Trim().ToUpperInvariant()`;
- SQL uniqueness is enforced on the stored `NormalizedCompanyCode` using the `Latin1_General_100_BIN2` binary collation, which is authoritative under concurrent creation;
- the original display casing is preserved separately in `CompanyCode`.

Unicode company codes are permitted; the code is not restricted to ASCII. No further character grammar is imposed in Milestone 1; a stricter grammar, if ever required, is a separate decision.

### BRULE-CMP-0009 — Company name

Company name is required, limited to 200 characters, trimmed, preserves display casing, is mutable only through the approved company profile update operation, and is not unique.

### BRULE-CMP-0010 — Base currency

`BaseCurrencyCode` is required and is a valid ISO-4217 alphabetic three-letter code stored in uppercase. It is captured at creation as the company's base / default currency configuration, owned by Platform, and is immutable in Milestone 1. Platform does not own functional-currency accounting semantics; a future General Ledger feature may define those without changing the Platform Company ownership boundary or requiring a change to this attribute. `REQ-PLT-0012` supports company currency configuration; "required at creation" and "immutable in Milestone 1" are FP-005 design decisions.

### BRULE-CMP-0011 — Trusted transition metadata

Every transition records trusted `StatusChangedUtc`, `StatusChangedBy`, previous status, new status, and `StatusChangeReasonCode`. Creation records `Created`; every later transition requires an applicable non-`Created` code. Activate, Deactivate, and Archive each require an explicit non-`Created` code. Clients cannot supply audit timestamps or bypass status validation, and events carry only the bounded reason code — never free-form reason text.

### BRULE-CMP-0012 — Optimistic concurrency

Every stale status-changing or profile-changing command fails rather than overwriting the current Company row.

### BRULE-CMP-0013 — Authorization plane

Company administration is authorized only by explicit Platform company permissions evaluated within the trusted current tenant. Platform company authority grants no access to another tenant, and a company owned by another tenant is never disclosed.

### BRULE-CMP-0014 — Subscription independence

Company lifecycle status is not a subscription, billing, payment, or feature-entitlement status. Those concepts never implicitly activate, deactivate, or archive a company.

### BRULE-CMP-0015 — Immutable tenant ownership

`TenantId` is assigned once from the trusted current tenant context when the Company is created and is never changed afterward. A company cannot be moved between tenants.

### BRULE-CMP-0016 — Company ownership classification

Company implements `ITenantOwnedEntity` and is filtered and write-guarded by the existing tenant rules. Company does **not** implement `ICompanyOwnedEntity`; it is the company root and is scoped by tenant, not by company.

### BRULE-CMP-0017 — Status change reason vocabulary

`CompanyStatusChangeReason` contains exactly `Created`, `Administrative`, `Operational`, `Compliance`, `CustomerRequest`, and `IssueResolved`. Creation records `Created`; `Created` is invalid for any later transition.

### BRULE-CMP-0018 — Archive eligibility extensibility

In Milestone 1, a company may be archived from `Active` or `Inactive` with no additional prerequisite. As dependent modules such as HR and GL are introduced, archive eligibility may acquire additional **module-owned** prerequisite checks — for example active employees, open accounting periods, or posted/unsettled accounting dependencies. These checks are not encoded in Milestone 1. When introduced, they must be evaluated through approved published module contracts or queries (or another architecture-approved boundary); the Platform Company Domain must never directly reference HR or GL Domain types, and the Milestone 1 transition graph does not change.
