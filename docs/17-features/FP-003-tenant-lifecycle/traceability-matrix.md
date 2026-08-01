---
document_id: FP-003-TRACE
title: Tenant Lifecycle Traceability Matrix
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Traceability Matrix

## FP-003 internal traceability

| Capability | Business requirements and rules | Functional and security requirements | Non-functional requirements | Acceptance criteria | Test scenarios | Approved decisions |
|---|---|---|---|---|---|---|
| Authoritative Tenant identity | BR-TEN-0001, BR-TEN-0006, BRULE-TEN-0009 | FR-TEN-0101, SEC-TEN-0201 | NFR-TEN-0304 | AC-TEN-0001 | TS-TEN-0001, TS-TEN-0008, TS-TEN-0020 | DEC-TEN-0001 |
| Tenant code | BR-TEN-0006, BRULE-TEN-0010 | FR-TEN-0101 | NFR-TEN-0304 | AC-TEN-0002 | TS-TEN-0002, TS-TEN-0010, TS-TEN-0023 | DEC-TEN-0004 |
| Tenant display name and legal-name boundary | BR-TEN-0006, BRULE-TEN-0011 | FR-TEN-0101 | NFR-TEN-0305 | AC-TEN-0003 | TS-TEN-0003, TS-TEN-0010 | DEC-TEN-0005, DEC-TEN-0006 |
| Safe Tenant reads | BR-TEN-0001, BR-TEN-0005, BRULE-TEN-0016 | FR-TEN-0102, FR-TEN-0103, SEC-TEN-0204 | NFR-TEN-0301, NFR-TEN-0305 | AC-TEN-0004, AC-TEN-0016 | TS-TEN-0011, TS-TEN-0017, TS-TEN-0018, TS-TEN-0037 | DEC-TEN-0011, DEC-TEN-0012 |
| Status vocabulary and lifecycle transitions | BR-TEN-0003, BRULE-TEN-0001, BRULE-TEN-0003, BRULE-TEN-0004, BRULE-TEN-0007 | FR-TEN-0104, FR-TEN-0105, FR-TEN-0106, FR-TEN-0107, SEC-TEN-0201 | NFR-TEN-0301 | AC-TEN-0005, AC-TEN-0006, AC-TEN-0009, AC-TEN-0010 | TS-TEN-0004, TS-TEN-0005, TS-TEN-0006, TS-TEN-0013 | DEC-TEN-0002, DEC-TEN-0003 |
| No automatic or subscription-driven transition | BR-TEN-0003, BRULE-TEN-0005, BRULE-TEN-0015 | FR-TEN-0104, FR-TEN-0105, FR-TEN-0106, FR-TEN-0107 | NFR-TEN-0302 | AC-TEN-0006, AC-TEN-0020 | TS-TEN-0005, TS-TEN-0019, TS-TEN-0032 | DEC-TEN-0015, DEC-TEN-0016 |
| Authentication eligibility | BR-TEN-0002, BRULE-TEN-0002, BRULE-TEN-0006, BRULE-TEN-0016 | FR-TEN-0108, SEC-TEN-0202 | NFR-TEN-0301, NFR-TEN-0305 | AC-TEN-0007, AC-TEN-0008, AC-TEN-0016 | TS-TEN-0004, TS-TEN-0012, TS-TEN-0016, TS-TEN-0017 | DEC-TEN-0002, DEC-TEN-0011 |
| Already issued access tokens and ordinary API access | BR-TEN-0002, BRULE-TEN-0006 | FR-TEN-0108, SEC-TEN-0208 | NFR-TEN-0307 | AC-TEN-0019 | TS-TEN-0044 | DEC-TEN-0010 |
| Platform authorization plane | BR-TEN-0005, BRULE-TEN-0014 | SEC-TEN-0203, SEC-TEN-0204 | NFR-TEN-0303 | AC-TEN-0012, AC-TEN-0013 | TS-TEN-0015, TS-TEN-0016, TS-TEN-0037, TS-TEN-0040, TS-TEN-0041 | DEC-TEN-0012 |
| Historical preservation and no deletion | BR-TEN-0004, BRULE-TEN-0008 | SEC-TEN-0206 | NFR-TEN-0304 | AC-TEN-0010, AC-TEN-0011 | TS-TEN-0006, TS-TEN-0026, TS-TEN-0031, TS-TEN-0042 | DEC-TEN-0007 |
| Trusted metadata, events, and audit readiness | BR-TEN-0008, BRULE-TEN-0012 | SEC-TEN-0205 | NFR-TEN-0307 | AC-TEN-0015 | TS-TEN-0007, TS-TEN-0029, TS-TEN-0035 | DEC-TEN-0009, DEC-TEN-0017 |
| Optimistic concurrency | BR-TEN-0003, BRULE-TEN-0013 | SEC-TEN-0207 | NFR-TEN-0304 | AC-TEN-0014 | TS-TEN-0014, TS-TEN-0025, TS-TEN-0043 | DEC-TEN-0003 |
| Platform persistence and tenant isolation | BR-TEN-0001, BR-TEN-0007 | SEC-TEN-0204, SEC-TEN-0206 | NFR-TEN-0302, NFR-TEN-0303, NFR-TEN-0304 | AC-TEN-0018 | TS-TEN-0020, TS-TEN-0024, TS-TEN-0027, TS-TEN-0028, TS-TEN-0033, TS-TEN-0036, TS-TEN-0038 | DEC-TEN-0008, DEC-TEN-0014 |
| Legacy migration reconciliation | BR-TEN-0001, BR-TEN-0004, BR-TEN-0007 | SEC-TEN-0201, SEC-TEN-0206 | NFR-TEN-0304 | AC-TEN-0017 | TS-TEN-0021, TS-TEN-0022, TS-TEN-0028 | DEC-TEN-0013, DEC-TEN-0014 |
| Architecture, quality, and focused milestone | BR-TEN-0007 | SEC-TEN-0204, SEC-TEN-0205 | NFR-TEN-0302, NFR-TEN-0303, NFR-TEN-0305, NFR-TEN-0306, NFR-TEN-0307 | AC-TEN-0020 | TS-TEN-0030, TS-TEN-0031, TS-TEN-0032, TS-TEN-0033, TS-TEN-0034, TS-TEN-0035 | DEC-TEN-0006, DEC-TEN-0015, DEC-TEN-0016, DEC-TEN-0017 |

## External requirement traceability

| External source | External IDs | FP-003 coverage |
|---|---|---|
| Master Platform requirements | REQ-PLT-0001, REQ-PLT-0002, REQ-PLT-0004, REQ-PLT-0005 | BR-TEN-0001 through BR-TEN-0007; FR-TEN-0101 through FR-TEN-0108; AC-TEN-0007, AC-TEN-0008, AC-TEN-0018 |
| Master Platform business rules | BR-PLT-0001, BR-PLT-0003, BR-PLT-0004, BR-PLT-0005, BR-PLT-0100 | Tenant isolation, retained Archive, audit-ready events, trusted UTC, and current-status authentication eligibility |
| ADR-005 | Tenant identity, tenant isolation, activation/suspension, Platform administration | Guid Tenant aggregate, no query filter on Tenant itself, retained tenant-owned filters, Platform authorization boundary |
| FP-001 decisions | DEC-IAM-0001, DEC-IAM-0003, DEC-IAM-0004, DEC-IAM-0012, DEC-IAM-0013, DEC-IAM-0017, DEC-IAM-0018 | Separate Platform authorization, tenant selection eligibility, suspension denial, event/audit dependency, no destructive identity-history behavior |
| FP-001 rules and requirements | BRULE-IAM-0002, BRULE-IAM-0003, BRULE-IAM-0007, BRULE-IAM-0015, BRULE-IAM-0022; FR-IAM-0114, FR-IAM-0116, FR-IAM-0117, FR-IAM-0123 | Active-tenant prerequisite, trusted TenantId, no tenant-role Platform authority, no request override |
| FP-001 acceptance and tests | AC-IAM-0006, AC-IAM-0007, AC-IAM-0016, AC-IAM-0021; TS-IAM-0020 through TS-IAM-0024 | Tenant selection includes only active lifecycle status; suspended tenant is ineligible |
| FP-002 decisions | DEC-AUTH-0008, DEC-AUTH-0013, DEC-AUTH-0020, DEC-AUTH-0021, DEC-AUTH-0022 | Session and selection workflows consume current eligibility; audit and support-authentication boundaries remain separate |
| FP-002 rules and requirements | BR-AUTH-0002, BR-AUTH-0007; BRULE-AUTH-0004, BRULE-AUTH-0011; FR-AUTH-0103 through FR-AUTH-0105, FR-AUTH-0110, FR-AUTH-0120 | FP-003 supplies trusted tenant status for discovery, session creation, and refresh |
| FP-002 acceptance and tests | AC-AUTH-0002 through AC-AUTH-0004, AC-AUTH-0018; TS-AUTH-0020 through TS-AUTH-0024, TS-AUTH-0034 | Only Active tenants are eligible; suspended tenant decisions are tested |

## Deferred traceability

Subscription, billing, company provisioning, first-administrator provisioning, branding, localization, notification delivery, tenant UI, Platform-support authentication, immutable audit storage, authentication sessions, refresh tokens, JWT issuance, and public Tenant endpoints require their own approved feature packages or later milestones.
