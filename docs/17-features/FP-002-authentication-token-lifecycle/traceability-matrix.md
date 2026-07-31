---
document_id: FP-002-TRACE
title: Authentication Traceability Matrix
status: Draft
version: 0.1
---

# Traceability Matrix

| Capability | Requirements | Acceptance criteria | Tests |
|---|---|---|---|
| Credential login | BR-AUTH-0001, FR-AUTH-0102 | AC-AUTH-0001 | login and enumeration tests |
| Tenant resolution | BR-AUTH-0002, FR-AUTH-0103–0106 | AC-AUTH-0002–0006 | tenant-selection tests |
| Sessions | BR-AUTH-0003, FR-AUTH-0107, 0111–0114 | AC-AUTH-0010–0011 | session tests |
| Access JWT | BR-AUTH-0004, FR-AUTH-0106, 0121–0123 | AC-AUTH-0005–0006, 0019, 0022 | JWT/key tests |
| Refresh lifecycle | BR-AUTH-0005, FR-AUTH-0107–0110 | AC-AUTH-0007–0009, 0021 | rotation/reuse/concurrency tests |
| Invitation/reset | BR-AUTH-0006, FR-AUTH-0101, 0116–0117 | AC-AUTH-0013–0015 | action-token tests |
| Lockout/state | BR-AUTH-0007, FR-AUTH-0118–0120, 0124 | AC-AUTH-0016–0018 | lockout/state tests |
| Secret protection | BR-AUTH-0008, SEC-AUTH requirements | AC-AUTH-0020 | log/storage scans |
| Architecture | NFR-AUTH requirements | architecture constraints | architecture suite |

Immutable audit persistence, platform-support authentication, external providers, passwordless flows, and concrete MFA are deferred.
