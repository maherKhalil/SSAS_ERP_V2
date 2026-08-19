---
document_id: FP-006-TRACE
title: HR Employee Traceability Matrix
status: Approved for Implementation
version: 1.0
module: HR
milestone: Milestone 1
---

# Traceability Matrix

> Approved for Implementation — traceability reflecting the settled FP-006A decisions.

## FP-006 internal traceability

| Capability | Business requirements and rules | Functional and security requirements | Non-functional requirements | Acceptance criteria | Test scenarios | Decisions |
|---|---|---|---|---|---|---|
| Authoritative tenant-owned employee identity | BR-EMP-0001, BRULE-EMP-0006, BRULE-EMP-0012 | FR-EMP-0101, SEC-EMP-0201, SEC-EMP-0209 | NFR-EMP-0304 | AC-EMP-0001, AC-EMP-0002 | TS-EMP-0001, TS-EMP-0007, TS-EMP-0073, TS-EMP-0074 | DEC-EMP-0001, DEC-EMP-0004 |
| Company ownership and partition | BR-EMP-0002, BRULE-EMP-0013 | FR-EMP-0101, SEC-EMP-0202, SEC-EMP-0209 | NFR-EMP-0309 | AC-EMP-0003 | TS-EMP-0075, TS-EMP-0112 | DEC-EMP-0002, DEC-EMP-0008 |
| Branch ownership and partition | BR-EMP-0003, BRULE-EMP-0014 | FR-EMP-0101, SEC-EMP-0203 | NFR-EMP-0309 | AC-EMP-0004 | TS-EMP-0112 | DEC-EMP-0003, DEC-EMP-0005 |
| Employee number identity and uniqueness | BR-EMP-0004, BRULE-EMP-0007, BRULE-EMP-0008, BRULE-EMP-0009 | FR-EMP-0101, SEC-EMP-0209 | NFR-EMP-0308 | AC-EMP-0006, AC-EMP-0008 | TS-EMP-0002, TS-EMP-0020, TS-EMP-0028, TS-EMP-0068, TS-EMP-0069, TS-EMP-0099 | DEC-EMP-0009, DEC-EMP-0010, DEC-EMP-0012 |
| National identity | BR-EMP-0005, BRULE-EMP-0010 | FR-EMP-0104 | NFR-EMP-0308 | AC-EMP-0009 | TS-EMP-0004, TS-EMP-0021, TS-EMP-0070 | DEC-EMP-0013 |
| Employee name and profile maintenance | BR-EMP-0006 | FR-EMP-0104 | NFR-EMP-0305 | AC-EMP-0007 | TS-EMP-0003, TS-EMP-0022, TS-EMP-0091 | DEC-EMP-0019 |
| Lifecycle states and transitions | BR-EMP-0006, BRULE-EMP-0001, BRULE-EMP-0002, BRULE-EMP-0003 | FR-EMP-0105 | NFR-EMP-0301 | AC-EMP-0012, AC-EMP-0013, AC-EMP-0014, AC-EMP-0018 | TS-EMP-0005, TS-EMP-0006, TS-EMP-0094 | DEC-EMP-0014, DEC-EMP-0016 |
| Employment dates and termination | BR-EMP-0006, BRULE-EMP-0004, BRULE-EMP-0011 | FR-EMP-0105 | NFR-EMP-0304 | AC-EMP-0010, AC-EMP-0015, AC-EMP-0016 | TS-EMP-0008, TS-EMP-0009, TS-EMP-0024, TS-EMP-0071, TS-EMP-0095 | DEC-EMP-0014 |
| Historical preservation and no physical deletion | BR-EMP-0007, BRULE-EMP-0005 | SEC-EMP-0207 | NFR-EMP-0304 | AC-EMP-0017 | TS-EMP-0077, TS-EMP-0085, TS-EMP-0120 | DEC-EMP-0015 |
| Transfer as an explicit operation | BR-EMP-0008, BRULE-EMP-0014, BRULE-EMP-0015, BRULE-EMP-0016, BRULE-EMP-0017 | FR-EMP-0106, SEC-EMP-0209 | NFR-EMP-0309 | AC-EMP-0031, AC-EMP-0034 | TS-EMP-0011, TS-EMP-0079, TS-EMP-0093, TS-EMP-0114 | DEC-EMP-0019, DEC-EMP-0020 |
| Branch assignment history | BR-EMP-0009, BRULE-EMP-0018, BRULE-EMP-0019 | FR-EMP-0107 | NFR-EMP-0307 | AC-EMP-0005 | TS-EMP-0010, TS-EMP-0029, TS-EMP-0072, TS-EMP-0085, TS-EMP-0097, TS-EMP-0113 | DEC-EMP-0006, DEC-EMP-0007, DEC-EMP-0024 |
| Historical branch attribution | BR-EMP-0009, BRULE-EMP-0022 | FR-EMP-0107 | NFR-EMP-0305 | AC-EMP-0035 | TS-EMP-0030 | DEC-EMP-0025 |
| Dual branch authorization for transfer | BR-EMP-0010, BRULE-EMP-0020 | SEC-EMP-0212, SEC-EMP-0205 | NFR-EMP-0304 | AC-EMP-0032 | TS-EMP-0050, TS-EMP-0084 | DEC-EMP-0022 |
| Transfer atomicity and concurrency | BR-EMP-0008, BRULE-EMP-0023 | FR-EMP-0106, SEC-EMP-0208 | NFR-EMP-0304 | AC-EMP-0033 | TS-EMP-0029, TS-EMP-0080, TS-EMP-0081, TS-EMP-0082, TS-EMP-0083 | DEC-EMP-0021 |
| Inactive source branch recovery | BR-EMP-0008, BRULE-EMP-0021 | SEC-EMP-0212 | NFR-EMP-0303 | AC-EMP-0036 | TS-EMP-0051 | DEC-EMP-0023 |
| Sanctioned transfer channel | BR-EMP-0008, BRULE-EMP-0014, BRULE-EMP-0015 | SEC-EMP-0203, SEC-EMP-0209 | NFR-EMP-0309 | AC-EMP-0043 | TS-EMP-0114 | DEC-EMP-0019 |
| Company execution context | BR-EMP-0002, BRULE-EMP-0013 | SEC-EMP-0202, SEC-EMP-0205, SEC-EMP-0211 | NFR-EMP-0309 | AC-EMP-0003, AC-EMP-0042 | TS-EMP-0045, TS-EMP-0046, TS-EMP-0049, TS-EMP-0100 | DEC-EMP-0026 |
| User↔company authorization | BR-EMP-0010, BRULE-EMP-0024 | SEC-EMP-0204, SEC-EMP-0205 | NFR-EMP-0303, NFR-EMP-0309 | AC-EMP-0040, AC-EMP-0041 | TS-EMP-0042, TS-EMP-0044, TS-EMP-0046 | DEC-EMP-0027 |
| Three independent authorization dimensions | BR-EMP-0010, BRULE-EMP-0024 | SEC-EMP-0204 | NFR-EMP-0303 | AC-EMP-0040, AC-EMP-0041 | TS-EMP-0041, TS-EMP-0042, TS-EMP-0043, TS-EMP-0044 | DEC-EMP-0028 |
| Functional permissions | BR-EMP-0010 | SEC-EMP-0204 | NFR-EMP-0303 | AC-EMP-0040 | TS-EMP-0040 | DEC-EMP-0030 |
| Explicit scope predicates and read guards | BR-EMP-0010, BR-EMP-0011, BRULE-EMP-0025 | FR-EMP-0102, FR-EMP-0103, SEC-EMP-0206 | NFR-EMP-0305, NFR-EMP-0306 | AC-EMP-0029, AC-EMP-0030 | TS-EMP-0047, TS-EMP-0048, TS-EMP-0110, TS-EMP-0111 | DEC-EMP-0029 |
| Employee retrieval, search, and opacity | BR-EMP-0011, BRULE-EMP-0004 | FR-EMP-0102, FR-EMP-0103, SEC-EMP-0210 | NFR-EMP-0305 | AC-EMP-0016, AC-EMP-0027, AC-EMP-0028 | TS-EMP-0023, TS-EMP-0024, TS-EMP-0092, TS-EMP-0096 | DEC-EMP-0029 |
| B1 runtime proofs — ADR-023 LOW-1 | BR-EMP-0003, BRULE-EMP-0014 | SEC-EMP-0203, SEC-EMP-0205 | NFR-EMP-0304, NFR-EMP-0306 | AC-EMP-0020, AC-EMP-0021, AC-EMP-0022, AC-EMP-0023 | TS-EMP-0060, TS-EMP-0061, TS-EMP-0062, TS-EMP-0063 | DEC-EMP-0003, DEC-EMP-0034 |
| Authorization freshness and revocation | BR-EMP-0010, BRULE-EMP-0024 | SEC-EMP-0205, SEC-EMP-0211 | NFR-EMP-0304 | AC-EMP-0024, AC-EMP-0025, AC-EMP-0026 | TS-EMP-0064, TS-EMP-0065, TS-EMP-0066 | DEC-EMP-0028, DEC-EMP-0034 |
| Optimistic concurrency | BR-EMP-0012, BRULE-EMP-0023 | SEC-EMP-0208 | NFR-EMP-0304 | AC-EMP-0019 | TS-EMP-0026, TS-EMP-0076, TS-EMP-0098 | DEC-EMP-0021 |
| HTTP transport surface | BR-EMP-0011 | FR-EMP-0101, FR-EMP-0102, FR-EMP-0103, FR-EMP-0104, FR-EMP-0105, FR-EMP-0106, FR-EMP-0107, SEC-EMP-0210 | NFR-EMP-0305 | AC-EMP-0044 | TS-EMP-0090, TS-EMP-0091, TS-EMP-0092, TS-EMP-0093, TS-EMP-0094, TS-EMP-0095, TS-EMP-0096, TS-EMP-0097, TS-EMP-0098, TS-EMP-0099, TS-EMP-0100, TS-EMP-0101, TS-EMP-0102 | DEC-EMP-0019, DEC-EMP-0030 |
| Tenant persistence and isolation reuse | BR-EMP-0001, BRULE-EMP-0012 | SEC-EMP-0201, SEC-EMP-0207 | NFR-EMP-0302, NFR-EMP-0304 | AC-EMP-0011 | TS-EMP-0067, TS-EMP-0073, TS-EMP-0074, TS-EMP-0115, TS-EMP-0116, TS-EMP-0119 | DEC-EMP-0001, DEC-EMP-0002 |
| Safe domain events and auditability | BR-EMP-0012 | SEC-EMP-0205 | NFR-EMP-0307 | AC-EMP-0018 | TS-EMP-0012, TS-EMP-0078, TS-EMP-0121 | DEC-EMP-0024 |
| Clean Architecture and module isolation | BR-EMP-0001 | — | NFR-EMP-0301, NFR-EMP-0302, NFR-EMP-0303 | — | TS-EMP-0025, TS-EMP-0031, TS-EMP-0119, TS-EMP-0122 | DEC-EMP-0002 |
| Shared→Dedicated cutover | BR-EMP-0007, BR-EMP-0009 | — | NFR-EMP-0310 | AC-EMP-0037, AC-EMP-0038, AC-EMP-0039 | TS-EMP-0130, TS-EMP-0131, TS-EMP-0132, TS-EMP-0133 | DEC-EMP-0033 |
| Department and Position deferral | BRULE-EMP-0026 | — | NFR-EMP-0303 | AC-EMP-0045 | TS-EMP-0117 | DEC-EMP-0017, DEC-EMP-0018, DEC-EMP-0031 |
| Employee number generation deferral | BR-EMP-0004, BRULE-EMP-0027 | FR-EMP-0101 | NFR-EMP-0308 | AC-EMP-0046 | TS-EMP-0118 | DEC-EMP-0011 |
| Documents, import, and export deferral | BR-EMP-0011 | — | NFR-EMP-0303 | AC-EMP-0047 | TS-EMP-0101 | DEC-EMP-0032 |
| Trusted state and spoof refusal | BR-EMP-0001, BR-EMP-0002, BR-EMP-0003 | SEC-EMP-0201, SEC-EMP-0202, SEC-EMP-0203 | NFR-EMP-0304 | AC-EMP-0002, AC-EMP-0003, AC-EMP-0004, AC-EMP-0021 | TS-EMP-0027, TS-EMP-0061, TS-EMP-0074, TS-EMP-0075 | DEC-EMP-0026, DEC-EMP-0034 |

## External authority traceability

| FP-006 element | External authority |
|---|---|
| BR-EMP-0001, BRULE-EMP-0012 | BR-PLT-0001; ADR-005 |
| BR-EMP-0002, DEC-EMP-0002, DEC-EMP-0008 | ADR-014 revision 1.1; ADR-025 decision 1 |
| BR-EMP-0003, DEC-EMP-0003 | BR-PLT-0013; ADR-023 *For HR*; Architecture Principle 11 |
| BR-EMP-0004, BRULE-EMP-0008, DEC-EMP-0012 | BR-HR-0001; ADR-014; ADR-023 *For HR* |
| BRULE-EMP-0009 | ADR-023 *For HR* (company-wide uniqueness must not include BranchId) |
| BR-EMP-0005, BRULE-EMP-0010, DEC-EMP-0013 | BR-HR-0002 |
| BRULE-EMP-0011 | BR-HR-0003 |
| BRULE-EMP-0004, AC-EMP-0015 | BR-HR-0004 |
| BR-EMP-0007, BRULE-EMP-0005, DEC-EMP-0015 | BR-PLT-0003; DEC-CMP-0013 (precedent) |
| BR-EMP-0012, NFR-EMP-0307 | BR-PLT-0004; BR-PLT-0005 |
| BR-EMP-0008, DEC-EMP-0019 | REQ-HR-0004; ADR-023 decision 18; ADR-024 decisions 2, 3, 10 |
| BR-EMP-0009, DEC-EMP-0006, DEC-EMP-0007 | REQ-HR-0006; ADR-024 decisions 4, 5; Architecture Principle 11 |
| DEC-EMP-0020 | ADR-024 decision 9 |
| DEC-EMP-0021 | ADR-024 decision 7 |
| DEC-EMP-0022 | ADR-024 decision 6 |
| DEC-EMP-0023, BRULE-EMP-0021 | ADR-024 decision 12; ADR-023 decision 5 (exception to) |
| DEC-EMP-0025, BRULE-EMP-0022 | ADR-024 decision 8 |
| DEC-EMP-0026 | ADR-025 decisions 2, 3, 4, 11; ADR-014 revision 1.1 Correction B |
| DEC-EMP-0027 | BR-PLT-0002; ADR-025 decisions 5, 6; ADR-014 revision 1.1 Correction C |
| DEC-EMP-0028, BRULE-EMP-0024 | ADR-023 decision 5; ADR-025 decisions 7, 8 |
| DEC-EMP-0029, BRULE-EMP-0025 | BR-PLT-0016; ADR-023 decision 22; ADR-025 decision 10; ADR-014 revision 1.1 Correction D |
| DEC-EMP-0030 | BR-PLT-0101; BR-PLT-0103 |
| DEC-EMP-0004 | ADR-013 |
| DEC-EMP-0010 | DEC-CMP-0006; DEC-CMP-0007 (convention precedent) |
| DEC-EMP-0024 | DEC-CMP-0025; DEC-CMP-0026 (convention precedent) |
| DEC-EMP-0011, BRULE-EMP-0027 | BR-PLT-0006; FP-005 numbering-sequence exclusion |
| DEC-EMP-0017, DEC-EMP-0018, DEC-EMP-0031, BRULE-EMP-0026 | BR-HR-0005; BR-HR-0006; BR-HR-0007; REQ-HR-0100; REQ-HR-0200 |
| DEC-EMP-0032 | REQ-HR-0005; REQ-HR-0009; REQ-HR-0010 |
| DEC-EMP-0033, NFR-EMP-0310 | ADR-020; ADR-023 decision 21 |
| DEC-EMP-0034 | ADR-023 LOW-1 (deferred obligations) |
| DEC-EMP-0001, DEC-EMP-0014 | ADR-023 *For HR*; DEC-CMP-0011 (contrasted, not followed) |
| NFR-EMP-0302, NFR-EMP-0305 | ADR-003; ADR-004; ADR-010 |
| Rowversion transport, AC-EMP-0019 | `Development-Standards.md` — Optimistic Concurrency (RowVersion) Transport |
| Data model FK targets | ADR-014 revision 1.1 Correction A (`tenant.Companies`); ADR-017 (no cross-catalog FK) |

## Source requirement coverage

| Source requirement | Coverage in FP-006 |
|---|---|
| REQ-HR-0001 Create Employee | FR-EMP-0101 — realized |
| REQ-HR-0002 Update Employee | FR-EMP-0104 — realized |
| REQ-HR-0003 Terminate Employee | FR-EMP-0105 — realized |
| REQ-HR-0004 Transfer Employee | FR-EMP-0106 — realized |
| REQ-HR-0005 Employee Documents | **Deferred whole** — DEC-EMP-0032, AC-EMP-0047 |
| REQ-HR-0006 Employee History | **Partially realized** — branch-assignment history only (FR-EMP-0107, DEC-EMP-0006). Profile, department, and position history deferred |
| REQ-HR-0007 Employee Status | FR-EMP-0105 and the lifecycle model — realized |
| REQ-HR-0008 Employee Search | FR-EMP-0103 — realized |
| REQ-HR-0009 Employee Import | **Deferred whole** — DEC-EMP-0032, AC-EMP-0047 |
| REQ-HR-0010 Employee Export | **Deferred whole** — DEC-EMP-0032, AC-EMP-0047 |
| REQ-HR-0100 Department CRUD | **Out of scope** — DEC-EMP-0017; BR-HR-0005 retained and deferred |
| REQ-HR-0200 Position Management | **Out of scope** — DEC-EMP-0018; BR-HR-0006 retained and deferred |

## Carried obligation traceability

Every obligation carried into FP-006 from an earlier slice is traceable to a first-class acceptance criterion and a first-class test scenario. None exists only in prose.

| Carried obligation | Origin | Acceptance criteria | Test scenarios | Decision |
|---|---|---|---|---|
| **V** — branch write authorizer genuinely invoked and branch stamped | ADR-023 LOW-1 item 1 and 2 | AC-EMP-0020 | TS-EMP-0060 | DEC-EMP-0034 |
| **W** — spoofed BranchId refused | ADR-023 LOW-1 item 3 | AC-EMP-0021 | TS-EMP-0061 | DEC-EMP-0034 |
| **X** — ordinary update cannot mutate BranchId | ADR-023 LOW-1 item 4 | AC-EMP-0022 | TS-EMP-0062 | DEC-EMP-0034 |
| **Y** — cross-branch update and delete refused | ADR-023 LOW-1 item 5 | AC-EMP-0023 | TS-EMP-0063 | DEC-EMP-0034 |
| Revoked branch assignment refuses next write | ADR-023 decision 10; BR-PLT-0014 | AC-EMP-0024 | TS-EMP-0064 | DEC-EMP-0034 |
| Revoked Tenant Administrator authority removes implicit branch scope | ADR-023 decisions 5 and 10 | AC-EMP-0025 | TS-EMP-0065 | DEC-EMP-0028, DEC-EMP-0034 |
| Revoked company authorization refuses next operation | BR-PLT-0002; ADR-025 decision 6 | AC-EMP-0026 | TS-EMP-0066 | DEC-EMP-0027 |
| ADR-023 decision 22 branch-read guard | ADR-023 decision 22 (forward rule, unenforced) | AC-EMP-0029, AC-EMP-0030 | TS-EMP-0110 | DEC-EMP-0029 |
| ADR-025 decision 10 company-read guard | ADR-025 decision 10 | AC-EMP-0029, AC-EMP-0030 | TS-EMP-0111 | DEC-EMP-0029 |
| Transfer history recorded and immutable | ADR-024 decisions 5 and 7 | AC-EMP-0005, AC-EMP-0031, AC-EMP-0035 | TS-EMP-0010, TS-EMP-0029, TS-EMP-0079, TS-EMP-0085 | DEC-EMP-0006 |
| EmployeeBranchAssignment not branch-owned | ADR-024 decision 4; Principle 11 | AC-EMP-0005 | TS-EMP-0113 | DEC-EMP-0007 |
| Sanctioned channel not externally activatable | ADR-024 decisions 3 and 11 | AC-EMP-0043 | TS-EMP-0114 | DEC-EMP-0019 |
| Copy manifest declared inventory extended | ADR-020; ADR-023 decision 21 | AC-EMP-0037, AC-EMP-0038 | TS-EMP-0130, TS-EMP-0131 | DEC-EMP-0033 |
| Copy ordering valid | ADR-020 | AC-EMP-0039 | TS-EMP-0132, TS-EMP-0133 | DEC-EMP-0033 |
| Department deferral traceable, not discarded | BR-HR-0005; REQ-HR-0100 | AC-EMP-0045 | TS-EMP-0117 | DEC-EMP-0017 |
| Position deferral traceable, not discarded | BR-HR-0006; REQ-HR-0200 | AC-EMP-0045 | TS-EMP-0117 | DEC-EMP-0018 |
| Manager rule deferral traceable, not discarded | BR-HR-0007 | AC-EMP-0045 | TS-EMP-0117 | DEC-EMP-0031 |
| EmployeeNumber generation deferral | BR-PLT-0006 | AC-EMP-0046 | TS-EMP-0118 | DEC-EMP-0011 |
| No cross-database foreign key | ADR-017; ADR-023 decision 4; ADR-025 decision 5 | AC-EMP-0044 | TS-EMP-0115 | DEC-EMP-0027 |

## Completeness

Every identifier defined in this package is referenced at least once above:

- **BR-EMP-0001 … BR-EMP-0012** — all 12 business requirements traced.
- **BRULE-EMP-0001 … BRULE-EMP-0027** — all 27 business rules traced.
- **FR-EMP-0101 … FR-EMP-0107** — all 7 functional requirements traced.
- **SEC-EMP-0201 … SEC-EMP-0212** — all 12 security requirements traced.
- **NFR-EMP-0301 … NFR-EMP-0310** — all 10 non-functional requirements traced.
- **AC-EMP-0001 … AC-EMP-0047** — all 47 acceptance criteria traced.
- **TS-EMP-0001 … TS-EMP-0133** — all 92 test scenarios traced.
- **DEC-EMP-0001 … DEC-EMP-0034** — all 34 decisions traced.

No identifier is referenced that is not defined, and no defined identifier is left untraced.
