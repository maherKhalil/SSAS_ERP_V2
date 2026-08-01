---
document_id: FP-002-TRACE
title: Authentication Traceability Matrix
status: Approved for Implementation
version: 1.0
---

# Traceability Matrix

| Capability | Business and functional requirements | Security and non-functional requirements | Acceptance criteria | Test scenarios |
|---|---|---|---|---|
| Global account and local Identity | BR-AUTH-0001, BRULE-AUTH-0001, BRULE-AUTH-0013, BRULE-AUTH-0014, FR-AUTH-0101, FR-AUTH-0102 | SEC-AUTH-0201, SEC-AUTH-0206, SEC-AUTH-0210, NFR-AUTH-0301, NFR-AUTH-0302, NFR-AUTH-0305 | AC-AUTH-0001, AC-AUTH-0015, AC-AUTH-0016 | TS-AUTH-0001, TS-AUTH-0002, TS-AUTH-0003, TS-AUTH-0004, TS-AUTH-0005, TS-AUTH-0006, TS-AUTH-0007, TS-AUTH-0008, TS-AUTH-0015, TS-AUTH-0061, TS-AUTH-0068 |
| Credential verification and lockout | BR-AUTH-0001, BR-AUTH-0007, BR-AUTH-0008, BRULE-AUTH-0001, BRULE-AUTH-0011, FR-AUTH-0102, FR-AUTH-0118, FR-AUTH-0119 | SEC-AUTH-0201, SEC-AUTH-0203, SEC-AUTH-0204, SEC-AUTH-0210, NFR-AUTH-0301, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0001, AC-AUTH-0016, AC-AUTH-0017, AC-AUTH-0020 | TS-AUTH-0009, TS-AUTH-0010, TS-AUTH-0011, TS-AUTH-0012, TS-AUTH-0013, TS-AUTH-0014, TS-AUTH-0050, TS-AUTH-0054, TS-AUTH-0064 |
| Invitation and account setup | BR-AUTH-0006, BRULE-AUTH-0008, BRULE-AUTH-0012, BRULE-AUTH-0013, BRULE-AUTH-0014, FR-AUTH-0101 | SEC-AUTH-0201, SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0205, SEC-AUTH-0206, SEC-AUTH-0207, SEC-AUTH-0210, NFR-AUTH-0301, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0009, AC-AUTH-0015, AC-AUTH-0020 | TS-AUTH-0001, TS-AUTH-0002, TS-AUTH-0003, TS-AUTH-0004, TS-AUTH-0005, TS-AUTH-0015, TS-AUTH-0018, TS-AUTH-0054, TS-AUTH-0056, TS-AUTH-0062, TS-AUTH-0063, TS-AUTH-0065 |
| Password reset | BR-AUTH-0006, BR-AUTH-0007, BR-AUTH-0008, BRULE-AUTH-0008, BRULE-AUTH-0010, BRULE-AUTH-0011, BRULE-AUTH-0012, FR-AUTH-0116, FR-AUTH-0117, FR-AUTH-0124 | SEC-AUTH-0201, SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0204, SEC-AUTH-0205, SEC-AUTH-0206, SEC-AUTH-0207, SEC-AUTH-0210, NFR-AUTH-0301, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0009, AC-AUTH-0013, AC-AUTH-0014, AC-AUTH-0020 | TS-AUTH-0015, TS-AUTH-0016, TS-AUTH-0017, TS-AUTH-0018, TS-AUTH-0051, TS-AUTH-0054, TS-AUTH-0056, TS-AUTH-0062, TS-AUTH-0063, TS-AUTH-0065 |
| Tenant resolution and selection | BR-AUTH-0002, BRULE-AUTH-0001, BRULE-AUTH-0003, FR-AUTH-0103, FR-AUTH-0104, FR-AUTH-0105 | SEC-AUTH-0203, SEC-AUTH-0204, SEC-AUTH-0206, NFR-AUTH-0301, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0002, AC-AUTH-0003, AC-AUTH-0004 | TS-AUTH-0020, TS-AUTH-0021, TS-AUTH-0022, TS-AUTH-0023, TS-AUTH-0024 |
| Tenant-scoped access token | BR-AUTH-0004, BRULE-AUTH-0002, BRULE-AUTH-0004, BRULE-AUTH-0005, BRULE-AUTH-0015, FR-AUTH-0106, FR-AUTH-0121, FR-AUTH-0122, FR-AUTH-0123 | SEC-AUTH-0203, SEC-AUTH-0206, SEC-AUTH-0208, SEC-AUTH-0209, SEC-AUTH-0211, NFR-AUTH-0301, NFR-AUTH-0303, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0005, AC-AUTH-0006, AC-AUTH-0019, AC-AUTH-0020, AC-AUTH-0022 | TS-AUTH-0025, TS-AUTH-0030, TS-AUTH-0037, TS-AUTH-0054, TS-AUTH-0072, TS-AUTH-0073 |
| Refresh lifecycle | BR-AUTH-0005, BR-AUTH-0007, BRULE-AUTH-0004, BRULE-AUTH-0006, BRULE-AUTH-0007, BRULE-AUTH-0008, BRULE-AUTH-0012, FR-AUTH-0107, FR-AUTH-0108, FR-AUTH-0109, FR-AUTH-0110, FR-AUTH-0124, FR-AUTH-0125 | SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0206, SEC-AUTH-0208, SEC-AUTH-0212, NFR-AUTH-0301, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0007, AC-AUTH-0008, AC-AUTH-0009, AC-AUTH-0017, AC-AUTH-0018, AC-AUTH-0020, AC-AUTH-0021 | TS-AUTH-0031, TS-AUTH-0032, TS-AUTH-0033, TS-AUTH-0034, TS-AUTH-0035, TS-AUTH-0036, TS-AUTH-0042, TS-AUTH-0062, TS-AUTH-0066 |
| Session lifecycle | BR-AUTH-0003, BRULE-AUTH-0004, BRULE-AUTH-0009, FR-AUTH-0111, FR-AUTH-0112, FR-AUTH-0113, FR-AUTH-0114, FR-AUTH-0125 | SEC-AUTH-0203, SEC-AUTH-0208, NFR-AUTH-0301, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0010, AC-AUTH-0011, AC-AUTH-0020 | TS-AUTH-0033, TS-AUTH-0036, TS-AUTH-0038, TS-AUTH-0039, TS-AUTH-0040, TS-AUTH-0041, TS-AUTH-0054, TS-AUTH-0067 |
| Password change | BR-AUTH-0007, BRULE-AUTH-0009, BRULE-AUTH-0010, FR-AUTH-0115, FR-AUTH-0124 | SEC-AUTH-0201, SEC-AUTH-0203, SEC-AUTH-0210, NFR-AUTH-0301, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0012, AC-AUTH-0020 | TS-AUTH-0015, TS-AUTH-0019, TS-AUTH-0040, TS-AUTH-0054 |
| Eligibility state | BR-AUTH-0002, BR-AUTH-0007, BRULE-AUTH-0004, BRULE-AUTH-0011, FR-AUTH-0119, FR-AUTH-0120 | SEC-AUTH-0204, NFR-AUTH-0301, NFR-AUTH-0303, NFR-AUTH-0307 | AC-AUTH-0017, AC-AUTH-0018 | TS-AUTH-0010, TS-AUTH-0022, TS-AUTH-0034, TS-AUTH-0042 |
| Public API protection | BR-AUTH-0008, BRULE-AUTH-0003, BRULE-AUTH-0011, BRULE-AUTH-0014, FR-AUTH-0116, FR-AUTH-0118 | SEC-AUTH-0203, SEC-AUTH-0204, SEC-AUTH-0208, NFR-AUTH-0301, NFR-AUTH-0305, NFR-AUTH-0307 | AC-AUTH-0001, AC-AUTH-0013, AC-AUTH-0016, AC-AUTH-0020 | TS-AUTH-0050, TS-AUTH-0051, TS-AUTH-0052, TS-AUTH-0053, TS-AUTH-0054, TS-AUTH-0055, TS-AUTH-0056 |
| Auditability and secret protection | BR-AUTH-0009, BRULE-AUTH-0008, BRULE-AUTH-0015 | SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0209, NFR-AUTH-0307 | AC-AUTH-0009, AC-AUTH-0020 | TS-AUTH-0054, TS-AUTH-0062, TS-AUTH-0063, TS-AUTH-0073 |
| Persistence, architecture, and delivery quality | BRULE-AUTH-0006, BRULE-AUTH-0008, BRULE-AUTH-0012, BRULE-AUTH-0013, BRULE-AUTH-0015 | SEC-AUTH-0202, SEC-AUTH-0205, SEC-AUTH-0207, SEC-AUTH-0209, NFR-AUTH-0301, NFR-AUTH-0302, NFR-AUTH-0303, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0306, NFR-AUTH-0307 | AC-AUTH-0009, AC-AUTH-0020, AC-AUTH-0021 | TS-AUTH-0060, TS-AUTH-0061, TS-AUTH-0062, TS-AUTH-0063, TS-AUTH-0064, TS-AUTH-0065, TS-AUTH-0066, TS-AUTH-0067, TS-AUTH-0068, TS-AUTH-0070, TS-AUTH-0071, TS-AUTH-0072, TS-AUTH-0073, TS-AUTH-0074 |

## Previously approved decision traceability

| Decision area | Approved decisions | Mapped capability rows |
|---|---|---|
| Global account, credential, and password controls | DEC-AUTH-0001, DEC-AUTH-0002, DEC-AUTH-0004, DEC-AUTH-0005, DEC-AUTH-0006, DEC-AUTH-0023, DEC-AUTH-0024, DEC-AUTH-0027, DEC-AUTH-0028, DEC-AUTH-0031 | Global account and local Identity; credential verification and lockout |
| Invitation, reset, and action-token controls | DEC-AUTH-0003, DEC-AUTH-0016, DEC-AUTH-0017, DEC-AUTH-0025, DEC-AUTH-0026, DEC-AUTH-0029, DEC-AUTH-0030 | Invitation and account setup; password reset; auditability and secret protection |
| Access-token, signing-key, and browser controls | DEC-AUTH-0007, DEC-AUTH-0014, DEC-AUTH-0019 | Tenant-scoped access token; public API protection |
| Session, refresh, security-version, and password-change controls | DEC-AUTH-0008, DEC-AUTH-0009, DEC-AUTH-0010, DEC-AUTH-0011, DEC-AUTH-0012, DEC-AUTH-0020 | Refresh lifecycle; session lifecycle; password change |
| Tenant-selection controls | DEC-AUTH-0013 | Tenant resolution and selection |
| Explicitly deferred authentication capabilities | DEC-AUTH-0015, DEC-AUTH-0018, DEC-AUTH-0021, DEC-AUTH-0022 | Deferred client, MFA, immutable-audit, and platform-support scope |

## Sprint-01 Milestone 3 authoritative traceability

| Capability | Approved decisions | Functional requirements | Security and non-functional requirements | Acceptance criteria | Focused scenarios |
|---|---|---|---|---|---|
| Verified identity, membership discovery, and FP-003 eligibility | DEC-AUTH-0032, DEC-AUTH-0033, DEC-AUTH-0037 | FR-AUTH-0103, FR-AUTH-0104, FR-AUTH-0105, FR-AUTH-0120, FR-AUTH-0126 | SEC-AUTH-0213, SEC-AUTH-0216, NFR-AUTH-0301, NFR-AUTH-0302, NFR-AUTH-0303 | AC-AUTH-0002, AC-AUTH-0003, AC-AUTH-0004, AC-AUTH-0018, AC-AUTH-0023, AC-AUTH-0024, AC-AUTH-0027 | TS-AUTH-0020, TS-AUTH-0021, TS-AUTH-0022, TS-AUTH-0023, TS-AUTH-0087 |
| Exact ClientId and immutable session binding | DEC-AUTH-0034, DEC-AUTH-0036 | FR-AUTH-0125, FR-AUTH-0127, FR-AUTH-0134 | SEC-AUTH-0215, NFR-AUTH-0304 | AC-AUTH-0025, AC-AUTH-0026 | TS-AUTH-0036, TS-AUTH-0083 |
| Tenant-selection transaction and proof | DEC-AUTH-0038, DEC-AUTH-0039 | FR-AUTH-0105, FR-AUTH-0128 | SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0206, SEC-AUTH-0207, SEC-AUTH-0214, NFR-AUTH-0304, NFR-AUTH-0310 | AC-AUTH-0003, AC-AUTH-0028, AC-AUTH-0034, AC-AUTH-0035 | TS-AUTH-0024, TS-AUTH-0080, TS-AUTH-0082, TS-AUTH-0088, TS-AUTH-0090 |
| Session status, expiration, ownership, and revocation | DEC-AUTH-0035, DEC-AUTH-0036 | FR-AUTH-0127, FR-AUTH-0131 | SEC-AUTH-0203, NFR-AUTH-0305, NFR-AUTH-0309 | AC-AUTH-0026 | TS-AUTH-0033, TS-AUTH-0077, TS-AUTH-0078, TS-AUTH-0084 |
| Refresh ownership, format, lifetime, and atomic rotation | DEC-AUTH-0040, DEC-AUTH-0041, DEC-AUTH-0042 | FR-AUTH-0107, FR-AUTH-0108, FR-AUTH-0110, FR-AUTH-0129 | SEC-AUTH-0202, SEC-AUTH-0203, SEC-AUTH-0206, SEC-AUTH-0207, SEC-AUTH-0212, SEC-AUTH-0214, NFR-AUTH-0304, NFR-AUTH-0305, NFR-AUTH-0309, NFR-AUTH-0310 | AC-AUTH-0007, AC-AUTH-0009, AC-AUTH-0029, AC-AUTH-0030, AC-AUTH-0035 | TS-AUTH-0031, TS-AUTH-0033, TS-AUTH-0042, TS-AUTH-0062, TS-AUTH-0066, TS-AUTH-0079, TS-AUTH-0081, TS-AUTH-0090 |
| Verified reuse and session compromise | DEC-AUTH-0043 | FR-AUTH-0109, FR-AUTH-0130 | SEC-AUTH-0212, SEC-AUTH-0214, NFR-AUTH-0308, NFR-AUTH-0309 | AC-AUTH-0008, AC-AUTH-0021, AC-AUTH-0031, AC-AUTH-0034 | TS-AUTH-0032, TS-AUTH-0035, TS-AUTH-0066, TS-AUTH-0089 |
| Ten-session limit and deterministic oldest revocation | DEC-AUTH-0044 | FR-AUTH-0132 | SEC-AUTH-0203, NFR-AUTH-0305, NFR-AUTH-0308 | AC-AUTH-0032, AC-AUTH-0034 | TS-AUTH-0038, TS-AUTH-0075, TS-AUTH-0076, TS-AUTH-0085 |
| Password-reset session revocation | DEC-AUTH-0045 | FR-AUTH-0117, FR-AUTH-0124, FR-AUTH-0133 | SEC-AUTH-0201, SEC-AUTH-0205, SEC-AUTH-0212, NFR-AUTH-0308 | AC-AUTH-0014, AC-AUTH-0033, AC-AUTH-0034 | TS-AUTH-0017, TS-AUTH-0040, TS-AUTH-0086 |
| Canonical lock order and race serialization | DEC-AUTH-0046 | FR-AUTH-0135 | SEC-AUTH-0212, SEC-AUTH-0216, NFR-AUTH-0304, NFR-AUTH-0308 | AC-AUTH-0021, AC-AUTH-0034 | TS-AUTH-0085, TS-AUTH-0086, TS-AUTH-0087, TS-AUTH-0088, TS-AUTH-0089 |
| Safe session and selection events | DEC-AUTH-0047 | FR-AUTH-0128, FR-AUTH-0129, FR-AUTH-0130, FR-AUTH-0132 | SEC-AUTH-0203, SEC-AUTH-0214, NFR-AUTH-0307 | AC-AUTH-0020, AC-AUTH-0035 | TS-AUTH-0054, TS-AUTH-0073, TS-AUTH-0090 |

## Sprint-01 Milestone 2 coverage

Milestone 2 implements the rows for global account and local Identity, credential verification and lockout, invitation and account setup, password reset, and the applicable persistence, architecture, audit-ready event, and secret-protection requirements.

Tenant selection, sessions, refresh tokens, JWT issuance and validation, HTTP and browser security, signing-key rotation, authenticated password change, and end-to-end tenant eligibility remain assigned to later FP-002 milestones.

## Sprint-01 Milestone 3 coverage

Milestone 3 implements the internal tenant-selection, session, refresh-token, session-limit, password-reset session-revocation, and concurrency rows above. FP-003 is the authoritative tenant eligibility source. `DEC-AUTH-0032` through `DEC-AUTH-0047` are fully mapped to requirements, acceptance criteria, and focused scenarios.

Milestone 3 does not claim access-token/JWT issuance, HTTP or browser transport security, signing-key implementation, public logout/session administration, authenticated password change, notification delivery, Angular authentication, or Platform-support authentication.

Immutable audit persistence, platform-support authentication, external providers, passwordless flows, concrete MFA, Angular authentication, and notification delivery remain deferred as approved.

## Sprint-01 Milestone 4 authoritative traceability

| Capability | Approved decisions | Functional requirements | Security and non-functional requirements | Acceptance criteria | Focused scenarios |
|---|---|---|---|---|---|
| Exact routes, requests, statuses, and generic failures | DEC-AUTH-0048 | FR-AUTH-0136 | SEC-AUTH-0217, NFR-AUTH-0318 | AC-AUTH-0036 | TS-AUTH-0091, TS-AUTH-0092 |
| Exact claims, authorization values, and token-size cap | DEC-AUTH-0049 | FR-AUTH-0137 | SEC-AUTH-0218 | AC-AUTH-0037 | TS-AUTH-0093, TS-AUTH-0094 |
| Production X.509 RS256 provider | DEC-AUTH-0050 | FR-AUTH-0138 | SEC-AUTH-0219, SEC-AUTH-0232, NFR-AUTH-0311, NFR-AUTH-0317 | AC-AUTH-0038 | TS-AUTH-0095, TS-AUTH-0096 |
| Derived kid and deployment rollover | DEC-AUTH-0051 | FR-AUTH-0139 | SEC-AUTH-0220, NFR-AUTH-0312, NFR-AUTH-0317 | AC-AUTH-0039 | TS-AUTH-0097, TS-AUTH-0098 |
| Strict RS256 JWT validation | DEC-AUTH-0052 | FR-AUTH-0140 | SEC-AUTH-0221 | AC-AUTH-0040 | TS-AUTH-0099 |
| Refresh-cookie lifecycle | DEC-AUTH-0053 | FR-AUTH-0141 | SEC-AUTH-0222 | AC-AUTH-0041 | TS-AUTH-0100 |
| Signed CSRF and Data Protection | DEC-AUTH-0054 | FR-AUTH-0142 | SEC-AUTH-0223, SEC-AUTH-0232, NFR-AUTH-0313, NFR-AUTH-0317 | AC-AUTH-0042 | TS-AUTH-0101, TS-AUTH-0102 |
| Endpoint-specific rate limits and shared production enforcement | DEC-AUTH-0055 | FR-AUTH-0143 | SEC-AUTH-0226, NFR-AUTH-0314, NFR-AUTH-0317 | AC-AUTH-0043 | TS-AUTH-0105, TS-AUTH-0106, TS-AUTH-0107 |
| CORS, Origin, and trusted client IP | DEC-AUTH-0056 | FR-AUTH-0144 | SEC-AUTH-0224, SEC-AUTH-0225, NFR-AUTH-0317 | AC-AUTH-0044 | TS-AUTH-0103, TS-AUTH-0104 |
| Live FP-003 tenant eligibility | DEC-AUTH-0057 | FR-AUTH-0145 | SEC-AUTH-0227, NFR-AUTH-0315 | AC-AUTH-0045 | TS-AUTH-0108, TS-AUTH-0109 |
| Pre-commit access-token issuance and transport ambiguity | DEC-AUTH-0058 | FR-AUTH-0146 | SEC-AUTH-0228, NFR-AUTH-0316 | AC-AUTH-0046, AC-AUTH-0051 | TS-AUTH-0110, TS-AUTH-0111 |
| Current-session logout and UserLogout persistence | DEC-AUTH-0059 | FR-AUTH-0147, FR-AUTH-0151 | SEC-AUTH-0229, NFR-AUTH-0319 | AC-AUTH-0047, AC-AUTH-0051 | TS-AUTH-0112, TS-AUTH-0113 |
| Eligible tenant-selection summary | DEC-AUTH-0060 | FR-AUTH-0148 | SEC-AUTH-0217 | AC-AUTH-0048 | TS-AUTH-0114 |
| Sensitive HTTP response and observability controls | DEC-AUTH-0061 | FR-AUTH-0149 | SEC-AUTH-0230 | AC-AUTH-0049 | TS-AUTH-0115 |
| Safe exact OpenAPI contract | DEC-AUTH-0062 | FR-AUTH-0150 | SEC-AUTH-0231, NFR-AUTH-0318 | AC-AUTH-0050 | TS-AUTH-0116 |
| Milestone 4 persistence boundary and quality | DEC-AUTH-0063 | FR-AUTH-0151 | NFR-AUTH-0319 | AC-AUTH-0051 | TS-AUTH-0117, TS-AUTH-0118 |

Milestone 4 covers all new identifiers `FR-AUTH-0136` through `FR-AUTH-0151`, `SEC-AUTH-0217` through `SEC-AUTH-0232`, `NFR-AUTH-0311` through `NFR-AUTH-0319`, `AC-AUTH-0036` through `AC-AUTH-0051`, and `TS-AUTH-0091` through `TS-AUTH-0118`. Every `DEC-AUTH-0048` through `DEC-AUTH-0063` decision appears exactly once in this authoritative decision column.

The resulting FP-002 definition totals are 51 functional requirements, 32 security requirements, 19 non-functional requirements, 51 acceptance criteria, 103 test scenarios, and 63 approved decisions.

Angular authentication, browser route guards and storage implementation, session listing, revoke-another-session, logout-all, authenticated password change, password-reset/invitation HTTP delivery, Platform-support authentication, MFA, external identity providers, OAuth/OIDC provider behavior, JWKS, native/mobile clients, service authentication, API keys, impersonation, notification delivery, immutable audit storage, full live validation of all non-Tenant authorization state on every ordinary request, and additional high-risk business policies remain deferred and are not traced as delivered by Milestone 4.
