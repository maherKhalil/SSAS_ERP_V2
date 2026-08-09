---
document_id: FP-005-TRACE
title: Company / Legal Entity Traceability Matrix
status: Approved for Implementation
version: 1.0
module: Platform
milestone: Milestone 1
---

# Traceability Matrix

> Approved for Implementation — traceability reflecting the approved human decisions.

## FP-005 internal traceability

| Capability | Business requirements and rules | Functional and security requirements | Non-functional requirements | Acceptance criteria | Test scenarios | Decisions |
|---|---|---|---|---|---|---|
| Authoritative tenant-owned company identity | BR-CMP-0001, BR-CMP-0006, BRULE-CMP-0007, BRULE-CMP-0015 | FR-CMP-0101, SEC-CMP-0201, SEC-CMP-0208 | NFR-CMP-0304 | AC-CMP-0001, AC-CMP-0011, AC-CMP-0017 | TS-CMP-0001, TS-CMP-0007, TS-CMP-0045 | DEC-CMP-0001, DEC-CMP-0003, DEC-CMP-0014 |
| Company as data-partition dimension | BR-CMP-0002, BR-CMP-0007, BRULE-CMP-0016 | SEC-CMP-0204 | NFR-CMP-0302, NFR-CMP-0308 | AC-CMP-0016, AC-CMP-0018 | TS-CMP-0085, TS-CMP-0086, TS-CMP-0087 | DEC-CMP-0002, DEC-CMP-0004, DEC-CMP-0005 |
| Company code | BR-CMP-0006, BRULE-CMP-0008 | FR-CMP-0101, SEC-CMP-0208 | NFR-CMP-0304 | AC-CMP-0002, AC-CMP-0003 | TS-CMP-0002, TS-CMP-0020, TS-CMP-0028, TS-CMP-0041, TS-CMP-0042 | DEC-CMP-0006, DEC-CMP-0007 |
| Company name | BRULE-CMP-0009 | FR-CMP-0104 | NFR-CMP-0305 | AC-CMP-0004 | TS-CMP-0003, TS-CMP-0021 | DEC-CMP-0008 |
| Base currency configuration | BR-CMP-0009, BRULE-CMP-0010 | FR-CMP-0101, SEC-CMP-0208 | NFR-CMP-0304 | AC-CMP-0005, AC-CMP-0017 | TS-CMP-0004, TS-CMP-0007 | DEC-CMP-0009 |
| Lifecycle states and transitions | BR-CMP-0003, BRULE-CMP-0001, BRULE-CMP-0002, BRULE-CMP-0003, BRULE-CMP-0005 | FR-CMP-0105, FR-CMP-0106, FR-CMP-0107, SEC-CMP-0202 | NFR-CMP-0301 | AC-CMP-0006, AC-CMP-0007, AC-CMP-0008 | TS-CMP-0005, TS-CMP-0006, TS-CMP-0065, TS-CMP-0066 | DEC-CMP-0010, DEC-CMP-0011, DEC-CMP-0012, DEC-CMP-0024 |
| No automatic or subscription-driven transition | BR-CMP-0003, BRULE-CMP-0004, BRULE-CMP-0014 | FR-CMP-0105, FR-CMP-0106, FR-CMP-0107 | NFR-CMP-0302 | AC-CMP-0007 | TS-CMP-0005 | DEC-CMP-0010 |
| Transition metadata and reason codes | BR-CMP-0008, BRULE-CMP-0011, BRULE-CMP-0017 | SEC-CMP-0205 | NFR-CMP-0307 | AC-CMP-0015 | TS-CMP-0008, TS-CMP-0009, TS-CMP-0048 | DEC-CMP-0025, DEC-CMP-0026 |
| Historical preservation, no deletion, and archive extensibility | BR-CMP-0004, BRULE-CMP-0006, BRULE-CMP-0018 | SEC-CMP-0206 | NFR-CMP-0303, NFR-CMP-0304 | AC-CMP-0008, AC-CMP-0009 | TS-CMP-0006, TS-CMP-0047, TS-CMP-0082, TS-CMP-0088 | DEC-CMP-0012, DEC-CMP-0013, DEC-CMP-0027 |
| Tenant-scoped authorization and cross-tenant opacity | BR-CMP-0005, BRULE-CMP-0013 | SEC-CMP-0203, SEC-CMP-0201 | NFR-CMP-0303 | AC-CMP-0010, AC-CMP-0012, AC-CMP-0013 | TS-CMP-0022, TS-CMP-0025, TS-CMP-0026, TS-CMP-0060, TS-CMP-0061, TS-CMP-0062 | DEC-CMP-0014, DEC-CMP-0021 |
| Optimistic concurrency | BRULE-CMP-0012 | SEC-CMP-0207 | NFR-CMP-0304 | AC-CMP-0014 | TS-CMP-0024, TS-CMP-0046, TS-CMP-0063 | DEC-CMP-0020 |
| Platform persistence and isolation reuse | BR-CMP-0001, BR-CMP-0007, BRULE-CMP-0016 | SEC-CMP-0204, SEC-CMP-0206 | NFR-CMP-0302, NFR-CMP-0304, NFR-CMP-0308 | AC-CMP-0016, AC-CMP-0018 | TS-CMP-0023, TS-CMP-0040, TS-CMP-0043, TS-CMP-0044, TS-CMP-0045, TS-CMP-0047, TS-CMP-0085, TS-CMP-0086 | DEC-CMP-0001, DEC-CMP-0004, DEC-CMP-0017 |
| HTTP transport surface | BR-CMP-0003, BR-CMP-0005 | FR-CMP-0101, FR-CMP-0102, FR-CMP-0103, FR-CMP-0104, SEC-CMP-0201, SEC-CMP-0203 | NFR-CMP-0305 | AC-CMP-0010, AC-CMP-0013 | TS-CMP-0060, TS-CMP-0061, TS-CMP-0062, TS-CMP-0066, TS-CMP-0067, TS-CMP-0068 | DEC-CMP-0020, DEC-CMP-0021, DEC-CMP-0022, DEC-CMP-0023 |
| Deferred infrastructure boundaries | BR-CMP-0002 | — | NFR-CMP-0307, NFR-CMP-0308 | AC-CMP-0016, AC-CMP-0019 | TS-CMP-0086, TS-CMP-0089 | DEC-CMP-0015, DEC-CMP-0016, DEC-CMP-0017, DEC-CMP-0018, DEC-CMP-0019 |
| Architecture, quality, and focused milestone | BR-CMP-0007 | NFR-CMP-0302, NFR-CMP-0303 | NFR-CMP-0302, NFR-CMP-0303, NFR-CMP-0305, NFR-CMP-0306, NFR-CMP-0308 | AC-CMP-0019 | TS-CMP-0027, TS-CMP-0080, TS-CMP-0081, TS-CMP-0083, TS-CMP-0084, TS-CMP-0088, TS-CMP-0089 | DEC-CMP-0005, DEC-CMP-0018, DEC-CMP-0019 |

## External requirement traceability

| External source | External IDs | FP-005 coverage |
|---|---|---|
| Master Platform requirements | REQ-PLT-0010, REQ-PLT-0011, REQ-PLT-0012 | REQ-PLT-0010 (multiple companies per tenant) → BR-CMP-0001, FR-CMP-0101; REQ-PLT-0011 (independent activation/deactivation) → BR-CMP-0003, FR-CMP-0105, FR-CMP-0106; REQ-PLT-0012 (independent settings) → only the **base-currency configuration** portion, via BR-CMP-0009 and FR-CMP-0101. "Required at creation" and "immutable in M1" are FP-005 decisions (`DEC-CMP-0009`), not `REQ-PLT-0012` wording. Fiscal calendar, additional currencies, language, and numbering are deferred |
| Master Platform business rules | BR-PLT-0001, BR-PLT-0002, BR-PLT-0003, BR-PLT-0004, BR-PLT-0005 | BR-PLT-0001 tenant isolation → BR-CMP-0007, SEC-CMP-0204; BR-PLT-0002 company access → acknowledged and deferred (DEC-CMP-0015); BR-PLT-0003 soft delete → BRULE-CMP-0006; BR-PLT-0004 audit record → BR-CMP-0008, NFR-CMP-0307, DEC-CMP-0018 (production-readiness gate); BR-PLT-0005 UTC → BRULE-CMP-0011 |
| Constraints | CON-0100–CON-0107, CON-0200, CON-0201, CON-0202, CON-0203, CON-0204, CON-0205, CON-0300–CON-0303, CON-0500–CON-0503 | Clean Architecture and module boundaries → NFR-CMP-0302, NFR-CMP-0303; SQL Server and key strategy → data-model, ADR-013 (CON-0201 reconciled; CompanyId Guid is an approved exception); FK integrity and restricted deletes → data-model; auditing and UTC → BRULE-CMP-0011; REST/JSON/versioning/authorization → api-contracts; tenant-context validation → SEC-CMP-0201, SEC-CMP-0204 |
| Non-functional requirements | NFR security, maintainability, auditability | Security (no writable tenant, safe events, no secrets) → SEC-CMP-0201–0208; maintainability (Clean Architecture, aggregate-specific repositories) → NFR-CMP-0302, NFR-CMP-0305; auditability (safe events, retained history, production audit gate) → NFR-CMP-0307, DEC-CMP-0018 |
| ADR-005 | Tenant → Company → Business Data; Company is a legal entity owned by a tenant | Company as tenant-owned partition root reusing tenant isolation → DEC-CMP-0001, DEC-CMP-0002 |
| ADR-013 | Primary key and identifier strategy | CompanyId = Guid as a cross-cutting and approved cross-module boundary identifier → DEC-CMP-0003 |
| ADR-014 | Company ownership and scoping; deferred scope resolution | Tenant-owned, not company-owned; ICompanyOwnedEntity deferred; scope mechanism deferred with live-status invariant → DEC-CMP-0004, DEC-CMP-0005, DEC-CMP-0016 |
| ADR-010 | Repository pattern | Aggregate-specific ICompanyRepository, no generic repository, no IQueryable → NFR-CMP-0305 |
| ADR-003, ADR-004, ADR-008, ADR-009 | Clean Architecture, CQRS, EF Core, Domain Events | Layer independence, command/query separation, EF-in-Infrastructure, post-commit safe events → NFR-CMP-0302, NFR-CMP-0307 |
| FP-003 decisions | DEC-TEN-0001, DEC-TEN-0007, DEC-TEN-0008, DEC-TEN-0014 | Guid root identifier precedent, terminal-archive/no-delete precedent, root-type-not-self-scoped precedent, restricted Tenant FK from first migration → DEC-CMP-0003, DEC-CMP-0013, DEC-CMP-0004, data-model |
| Development Standards (API) | Platform rowversion transport convention | Reused, not a Company-specific codec → DEC-CMP-0020, api-contracts; codec extraction is an implementation prerequisite |

## Deferred traceability

User↔company assignment and company-scoped authorization, company scope resolution, per-company fiscal calendar, additional currencies, language settings, numbering sequences, branding, HR and GL entities and foreign keys, Angular UI, Row-Level Security, an immutable audit store, and an integration-event / outbox mechanism each require their own approved feature packages or later milestones. The `ICompanyOwnedEntity` interface and company filter/write-guard machinery are deferred to the first company-owned business record under `ADR-014` and `DEC-CMP-0005`. Archive-eligibility prerequisite checks for dependent modules are deferred under `BRULE-CMP-0018` / `DEC-CMP-0027`.
