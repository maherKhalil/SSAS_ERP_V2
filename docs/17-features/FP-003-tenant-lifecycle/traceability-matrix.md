---
document_id: FP-003-TRACE
title: Tenant Lifecycle Traceability Matrix
status: Approved for Implementation
version: 1.2
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
| Platform-plane authentication and authorization (ADR-015) | BR-TEN-0005, BRULE-TEN-0014 | SEC-TEN-0203, SEC-TEN-0204 | NFR-TEN-0303 | AC-TEN-0021, AC-TEN-0022, AC-TEN-0023, AC-TEN-0024, AC-TEN-0025, AC-TEN-0026, AC-TEN-0027, AC-TEN-0028, AC-TEN-0029, AC-TEN-0030 | TS-TEN-0045, TS-TEN-0046, TS-TEN-0047, TS-TEN-0048, TS-TEN-0049, TS-TEN-0050, TS-TEN-0051, TS-TEN-0052, TS-TEN-0053, TS-TEN-0054, TS-TEN-0055, TS-TEN-0056, TS-TEN-0057, TS-TEN-0058, TS-TEN-0059 | DEC-TEN-0012, DEC-TEN-0018 |
| Platform-support bootstrap, lifecycle, and authority administration (ADR-016) | BR-TEN-0005, BRULE-TEN-0014 | SEC-TEN-0203, SEC-TEN-0204 | NFR-TEN-0303 | AC-TEN-0031 through AC-TEN-0053 | TS-TEN-0060 through TS-TEN-0092 | DEC-TEN-0019, DEC-TEN-0020, DEC-TEN-0021 |
| Platform authentication session and token profile (ADR-016 Phase 3C) | BR-TEN-0005, BRULE-TEN-0014 | SEC-TEN-0203, SEC-TEN-0204 | NFR-TEN-0303 | AC-TEN-0054 through AC-TEN-0077 | TS-TEN-0093 through TS-TEN-0117 | DEC-TEN-0022 |
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
| ADR-015 | Tenant/Platform security planes, non-tenant platform token, PlatformSupport permission scope, dedicated platform handler, target-TenantId semantics | DEC-TEN-0018; AC-TEN-0021 through AC-TEN-0030; TS-TEN-0045 through TS-TEN-0059 |
| ADR-016 | Genesis bootstrap, principal Active/Disabled lifecycle, live status checks, Platform.Support.Administer permission | DEC-TEN-0019, DEC-TEN-0020, DEC-TEN-0021; AC-TEN-0031 through AC-TEN-0053; TS-TEN-0060 through TS-TEN-0092 |
| ADR-016 (Phase 3C session profile) | Separate platform authentication session, cross-plane refresh isolation, `security_plane=platform` token profile, live issuance/refresh eligibility, structural stateless validator, reused account SecurityVersion | DEC-TEN-0022; AC-TEN-0054 through AC-TEN-0077; TS-TEN-0093 through TS-TEN-0117 |
| FP-001 decisions | DEC-IAM-0001, DEC-IAM-0003, DEC-IAM-0004, DEC-IAM-0012, DEC-IAM-0013, DEC-IAM-0017, DEC-IAM-0018 | Separate Platform authorization, tenant selection eligibility, suspension denial, event/audit dependency, no destructive identity-history behavior |
| FP-001 rules and requirements | BRULE-IAM-0002, BRULE-IAM-0003, BRULE-IAM-0007, BRULE-IAM-0015, BRULE-IAM-0022; FR-IAM-0114, FR-IAM-0116, FR-IAM-0117, FR-IAM-0123 | Active-tenant prerequisite, trusted TenantId, no tenant-role Platform authority, no request override |
| FP-001 acceptance and tests | AC-IAM-0006, AC-IAM-0007, AC-IAM-0016, AC-IAM-0021; TS-IAM-0020 through TS-IAM-0024 | Tenant selection includes only active lifecycle status; suspended tenant is ineligible |
| FP-002 decisions | DEC-AUTH-0008, DEC-AUTH-0013, DEC-AUTH-0020, DEC-AUTH-0021, DEC-AUTH-0022, DEC-AUTH-0057 | Session and selection workflows consume current eligibility; FP-002 Milestone 4 performs one scoped live eligibility lookup for every ordinary tenant-scoped request and keeps logout separately available; audit and support-authentication boundaries remain separate |
| FP-002 rules and requirements | BR-AUTH-0002, BR-AUTH-0007; BRULE-AUTH-0004, BRULE-AUTH-0011; FR-AUTH-0103 through FR-AUTH-0105, FR-AUTH-0110, FR-AUTH-0120 | FP-003 supplies trusted tenant status for discovery, session creation, and refresh |
| FP-002 acceptance and tests | AC-AUTH-0002 through AC-AUTH-0004, AC-AUTH-0018; TS-AUTH-0020 through TS-AUTH-0024, TS-AUTH-0034 | Only Active tenants are eligible; suspended tenant decisions are tested |

## Platform-plane authorization traceability (ADR-015)

Every platform-plane security concept traces from `ADR-015` through `DEC-TEN-0018` to an acceptance criterion and a test scenario, leaving no orphan decision.

| Security concept | ADR / decision | Acceptance criteria | Test scenarios |
|---|---|---|---|
| Platform token profile (`security_plane=platform`, no `tenant_id`) | ADR-015, DEC-TEN-0018 | AC-TEN-0025 | TS-TEN-0049, TS-TEN-0050 |
| Platform-support authentication required on routes | ADR-015, DEC-TEN-0018 | AC-TEN-0021, AC-TEN-0022, AC-TEN-0023 | TS-TEN-0045, TS-TEN-0046, TS-TEN-0047, TS-TEN-0048 |
| `PermissionScope.PlatformSupport` permission scope | ADR-015, DEC-TEN-0018 | AC-TEN-0023, AC-TEN-0024 | TS-TEN-0048, TS-TEN-0052, TS-TEN-0053 |
| Tenant-role escalation prevention + claims-provider filter | ADR-015, DEC-TEN-0018 | AC-TEN-0024, AC-TEN-0030 | TS-TEN-0052, TS-TEN-0053, TS-TEN-0054 |
| Route-target `{tenantId}` semantics (target only, not caller scope) | ADR-015, DEC-TEN-0018 | AC-TEN-0026 | TS-TEN-0051 |
| Target-status independence of authorization | ADR-015, DEC-TEN-0018 | AC-TEN-0027, AC-TEN-0028 | TS-TEN-0055, TS-TEN-0056, TS-TEN-0057 |
| Tenant-plane non-regression (Company, Localization) | ADR-015, DEC-TEN-0018 | AC-TEN-0029 | TS-TEN-0058, TS-TEN-0059 |

## Platform-support bootstrap/lifecycle traceability (ADR-016)

Every Phase-3 authority concept traces from `ADR-016` through its decision to an acceptance criterion and a test scenario.

| Security concept | ADR / decision | Acceptance criteria | Test scenarios |
|---|---|---|---|
| Genesis bootstrap (immutable subject, existing identity, no self-promotion) | ADR-016, DEC-TEN-0019 | AC-TEN-0031, AC-TEN-0032, AC-TEN-0036 | TS-TEN-0060, TS-TEN-0061, TS-TEN-0062, TS-TEN-0065 |
| Usable-authority definition + genesis/recovery-only + idempotency | ADR-016, DEC-TEN-0019 | AC-TEN-0033, AC-TEN-0034, AC-TEN-0035, AC-TEN-0037 | TS-TEN-0063, TS-TEN-0064, TS-TEN-0066, TS-TEN-0067 |
| Principal Active/Disabled lifecycle + concurrency | ADR-016, DEC-TEN-0020 | AC-TEN-0038, AC-TEN-0041, AC-TEN-0042 | TS-TEN-0068, TS-TEN-0069, TS-TEN-0070, TS-TEN-0071, TS-TEN-0072 |
| Disabled denies token issuance/refresh; token profile | ADR-016, DEC-TEN-0020 | AC-TEN-0039, AC-TEN-0040 | TS-TEN-0073, TS-TEN-0074, TS-TEN-0075, TS-TEN-0076, TS-TEN-0077 |
| Authority-administration permission + non-repurposing | ADR-016, DEC-TEN-0021 | AC-TEN-0043, AC-TEN-0044, AC-TEN-0045 | TS-TEN-0078, TS-TEN-0079, TS-TEN-0080 |
| Status migration backfill (Active default; nullable transition metadata) | ADR-016, DEC-TEN-0020 | AC-TEN-0046, AC-TEN-0047 | TS-TEN-0081, TS-TEN-0082, TS-TEN-0083, TS-TEN-0084 |
| Bootstrap cardinality, deterministic selection, concurrent convergence | ADR-016, DEC-TEN-0019 | AC-TEN-0048, AC-TEN-0049, AC-TEN-0050 | TS-TEN-0085, TS-TEN-0086, TS-TEN-0087, TS-TEN-0090 |
| Disabled-principal recovery + fail-closed | ADR-016, DEC-TEN-0019 | AC-TEN-0051, AC-TEN-0052 | TS-TEN-0088, TS-TEN-0089 |
| Live persistence-backed usable-authority evaluation | ADR-016, DEC-TEN-0019 | AC-TEN-0053 | TS-TEN-0091, TS-TEN-0092 |

## Platform authentication session and token profile traceability (ADR-016 Phase 3C, DEC-TEN-0022)

Every Phase-3C session/token concept traces from `ADR-016`/`DEC-TEN-0022` through an acceptance criterion to a test scenario.

| Security concept | ADR / decision | Acceptance criteria | Test scenarios |
|---|---|---|---|
| Separate platform session aggregate; no tenant fields; identity+principal anchors | ADR-016, DEC-TEN-0022 | AC-TEN-0054, AC-TEN-0055, AC-TEN-0056 | TS-TEN-0093, TS-TEN-0094, TS-TEN-0095 |
| Cross-plane refresh isolation (tenant↔platform switching rejected) | ADR-016, DEC-TEN-0022 | AC-TEN-0057, AC-TEN-0058 | TS-TEN-0103 |
| Platform token profile / forbidden claims / legacy tenant compatibility | ADR-015/016, DEC-TEN-0018/0022 | AC-TEN-0059, AC-TEN-0060, AC-TEN-0074, AC-TEN-0075 | TS-TEN-0109, TS-TEN-0113, TS-TEN-0114, TS-TEN-0115, TS-TEN-0116, TS-TEN-0117 |
| Structural stateless validator (profile branching, no DB lookup) | ADR-016, DEC-TEN-0022 | AC-TEN-0069 | TS-TEN-0109, TS-TEN-0110, TS-TEN-0111, TS-TEN-0112 |
| Zero-permission issuance/refresh fail-closed | ADR-016, DEC-TEN-0019/0022 | AC-TEN-0061, AC-TEN-0062 | TS-TEN-0099, TS-TEN-0106 |
| Disabled issuance/refresh deny + proactive revocation; tenant sessions unaffected | ADR-016, DEC-TEN-0020/0022 | AC-TEN-0063, AC-TEN-0064, AC-TEN-0065, AC-TEN-0076 | TS-TEN-0098, TS-TEN-0102, TS-TEN-0107, TS-TEN-0108 |
| Re-enable does not revive revoked sessions | ADR-016, DEC-TEN-0022 | AC-TEN-0066 | TS-TEN-0104 |
| Reused account SecurityVersion; no principal SecurityVersion | ADR-015/016, DEC-TEN-0020/0022 | AC-TEN-0067, AC-TEN-0068 | TS-TEN-0097 |
| Live permission re-derivation; bootstrap config excluded from issuance | ADR-016, DEC-TEN-0022 | AC-TEN-0070, AC-TEN-0071 | TS-TEN-0096, TS-TEN-0106 |
| Independent platform session limit; reuse/compromise semantics | ADR-016, DEC-TEN-0022 | AC-TEN-0072, AC-TEN-0073 | TS-TEN-0100, TS-TEN-0101 |
| Physical-delete protection; retained platform session history | ADR-016, DEC-TEN-0022 | AC-TEN-0054 | TS-TEN-0105 |
| Trusted session-creation source (no caller IdentityId); FKs/Restrict | ADR-016, DEC-TEN-0022 | AC-TEN-0056, AC-TEN-0077 | TS-TEN-0094, TS-TEN-0095 |

## Deferred traceability

Subscription, billing, company provisioning, first-administrator provisioning, branding, localization, notification delivery, tenant UI, Platform-support authentication, immutable audit storage, authentication sessions, refresh tokens, JWT issuance, and public Tenant endpoints require their own approved feature packages or later milestones.

FP-002 Milestone 4 provides cross-package implementation coverage for `DEC-TEN-0010`, `SEC-TEN-0208`, `AC-TEN-0019`, and `TS-TEN-0044` through `DEC-AUTH-0057`, `FR-AUTH-0145`, `SEC-AUTH-0227`, `NFR-AUTH-0315`, `AC-AUTH-0045`, `TS-AUTH-0108`, and `TS-AUTH-0109` without changing FP-003 identifiers or ownership.
