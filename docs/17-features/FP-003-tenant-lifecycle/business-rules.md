---
document_id: FP-003-BR
title: Tenant Lifecycle Business Rules
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Business Rules

### BRULE-TEN-0001 — Initial state

Every newly created Tenant begins in `Provisioning`.

### BRULE-TEN-0002 — Authentication eligibility

`IsAuthenticationEligible` is derived from status and is true only for `Active`.

### BRULE-TEN-0003 — Approved transitions

Only these transitions are permitted:

- `Provisioning` to `Active`;
- `Provisioning` to `Archived`;
- `Active` to `Suspended`;
- `Active` to `Archived`;
- `Suspended` to `Active`;
- `Suspended` to `Archived`.

### BRULE-TEN-0004 — Archived is terminal

An `Archived` Tenant cannot transition again and is never authentication-eligible.

### BRULE-TEN-0005 — No automatic lifecycle transition

Time, inactivity, subscription state, billing state, or absence of memberships never changes Tenant status automatically.

### BRULE-TEN-0006 — Immediate suspension effect

After suspension commits, the Tenant is ineligible for tenant selection, new authentication sessions, refresh, and other operations that validate current eligibility.

### BRULE-TEN-0007 — Reactivation boundary

Reactivation is permitted only from `Suspended`; it is not an unarchive operation.

### BRULE-TEN-0008 — No physical deletion

Tenant records are archived and retained. Physical deletion and delete cascades are prohibited.

### BRULE-TEN-0009 — TenantId authority

`TenantId` is a server-generated, nonempty Guid, is immutable, is never reused, and is the same identifier referenced by tenant-owned records.

### BRULE-TEN-0010 — Tenant code

Tenant code is required, limited to 64 characters, trimmed, display-preserving, normalized exactly with `Trim().ToUpperInvariant()` and no culture- or provider-specific transformation, globally unique by normalized value, immutable after creation, and matched by a `Latin1_General_100_BIN2` column and unique index.

### BRULE-TEN-0011 — Tenant name

Tenant name is required, limited to 200 characters, trimmed, preserves display casing, is mutable only through an approved Tenant update operation, and is not globally unique. LegalName is deferred.

### BRULE-TEN-0012 — Trusted transition metadata

Every transition records trusted `StatusChangedUtc`, `StatusChangedBy`, previous status, new status, and `StatusChangeReasonCode`. Creation uses `Created`; every later transition requires an applicable bounded code, with Suspend and Archive requiring an explicit non-`Created` code. Clients cannot supply audit timestamps or bypass status validation.

### BRULE-TEN-0013 — Optimistic concurrency

Every stale status-changing command fails rather than overwriting the current Tenant row.

### BRULE-TEN-0014 — Authorization plane separation

Ordinary tenant roles never grant tenant lifecycle authority. Platform authorization does not itself grant tenant business-data access.

### BRULE-TEN-0015 — Subscription independence

Tenant lifecycle status is not a subscription, billing, payment, company, or feature-entitlement status. Those concepts cannot implicitly activate, suspend, reactivate, or archive a Tenant.

### BRULE-TEN-0016 — Safe eligibility result

The authentication-eligibility contract returns exactly the requested `TenantId`, `Exists`, nullable `TenantStatus`, `IsAuthenticationEligible`, and `TenantAuthenticationIneligibilityReason`. A missing Tenant is ineligible with reason `TenantNotFound`. It returns no aggregate, `IQueryable`, authorization decision, subscription data, name, or tenant business data.
