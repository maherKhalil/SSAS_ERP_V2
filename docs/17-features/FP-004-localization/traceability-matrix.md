---
document_id: FP-004-TRACE
title: Localization Traceability Matrix
status: Approved for Implementation
version: 1.0
sprint: Sprint-01
module: Platform
---

# Traceability Matrix

## Definition ledger

| Family | Defined range | Count | Authority |
|---|---:|---:|---|
| BR-LOC | 0001-0008 | 8 | requirements.md |
| BRULE-LOC | 0001-0034 | 34 | business-rules.md |
| FR-LOC | 0101-0144 | 44 | requirements.md |
| SEC-LOC | 0201-0219 | 19 | requirements.md |
| NFR-LOC | 0301-0315 | 15 | requirements.md |
| AC-LOC | 0001-0064 | 64 | acceptance-criteria.md |
| TS-LOC | 0001-0103 | 103 | test-scenarios.md |
| DEC-LOC | 0001-0036 | 36 | decisions-approved.md |
| CONFLICT-LOC | 0001-0032 | 32 | decisions-approved.md |
| **Total** |  | **355** | 12-document package |

Every identifier in each inclusive contiguous range is defined exactly once and is governed by the mappings below. The validation audit confirms uniqueness, definition, reference, and trace coverage.

## Requirement-to-verification themes

| Theme | Requirements/rules | Acceptance | Tests | Decisions/conflicts |
|---|---|---|---|---|
| JSON catalog, completeness, keys, retirement | BR-LOC-0001, BR-LOC-0003; BRULE-LOC-0001, BRULE-LOC-0003 through BRULE-LOC-0005, BRULE-LOC-0030; FR-LOC-0101 through FR-LOC-0104, FR-LOC-0129, FR-LOC-0138, FR-LOC-0139; NFR-LOC-0306 | AC-LOC-0001, AC-LOC-0002, AC-LOC-0025, AC-LOC-0030, AC-LOC-0041 | TS-LOC-0001 through TS-LOC-0004, TS-LOC-0034, TS-LOC-0040, TS-LOC-0056, TS-LOC-0093, TS-LOC-0094 | DEC-LOC-0001, DEC-LOC-0002, DEC-LOC-0017, DEC-LOC-0032; CONFLICT-LOC-0001 through CONFLICT-LOC-0004, CONFLICT-LOC-0021, CONFLICT-LOC-0022 |
| Tenant overrides, settings, persistence | BR-LOC-0002, BR-LOC-0005, BR-LOC-0007; BRULE-LOC-0006 through BRULE-LOC-0008, BRULE-LOC-0013, BRULE-LOC-0016, BRULE-LOC-0017, BRULE-LOC-0024 through BRULE-LOC-0026; FR-LOC-0105, FR-LOC-0110, FR-LOC-0125, FR-LOC-0126, FR-LOC-0129, FR-LOC-0131, FR-LOC-0132; SEC-LOC-0201, SEC-LOC-0211; NFR-LOC-0304 | AC-LOC-0003, AC-LOC-0004, AC-LOC-0011, AC-LOC-0015, AC-LOC-0026, AC-LOC-0027, AC-LOC-0030, AC-LOC-0032 through AC-LOC-0034, AC-LOC-0055 through AC-LOC-0058 | TS-LOC-0005, TS-LOC-0017, TS-LOC-0023, TS-LOC-0024, TS-LOC-0031, TS-LOC-0032, TS-LOC-0041 through TS-LOC-0061 | DEC-LOC-0003, DEC-LOC-0011, DEC-LOC-0019, DEC-LOC-0020; CONFLICT-LOC-0013, CONFLICT-LOC-0014, CONFLICT-LOC-0017 |
| Resolution, language, cache, lifecycle | BR-LOC-0004, BR-LOC-0008; BRULE-LOC-0002, BRULE-LOC-0009, BRULE-LOC-0018, BRULE-LOC-0027; FR-LOC-0106, FR-LOC-0107, FR-LOC-0117, FR-LOC-0118, FR-LOC-0120, FR-LOC-0124, FR-LOC-0127, FR-LOC-0133, FR-LOC-0137; SEC-LOC-0209, SEC-LOC-0212, SEC-LOC-0214, SEC-LOC-0218; NFR-LOC-0303, NFR-LOC-0305, NFR-LOC-0313 | AC-LOC-0005, AC-LOC-0006, AC-LOC-0016, AC-LOC-0019, AC-LOC-0037, AC-LOC-0053, AC-LOC-0059, AC-LOC-0060 | TS-LOC-0012 through TS-LOC-0016, TS-LOC-0033, TS-LOC-0065 through TS-LOC-0068, TS-LOC-0080 through TS-LOC-0085, TS-LOC-0097 | DEC-LOC-0002 through DEC-LOC-0004, DEC-LOC-0012, DEC-LOC-0018, DEC-LOC-0023, DEC-LOC-0024, DEC-LOC-0031; CONFLICT-LOC-0011 through CONFLICT-LOC-0013, CONFLICT-LOC-0020, CONFLICT-LOC-0027 |
| Text, placeholders, compatibility, direction | BR-LOC-0006; BRULE-LOC-0010 through BRULE-LOC-0012, BRULE-LOC-0020 through BRULE-LOC-0023; FR-LOC-0108, FR-LOC-0109, FR-LOC-0116, FR-LOC-0119, FR-LOC-0128, FR-LOC-0130; SEC-LOC-0204 through SEC-LOC-0207, SEC-LOC-0212, SEC-LOC-0213, SEC-LOC-0216; NFR-LOC-0308, NFR-LOC-0311, NFR-LOC-0312, NFR-LOC-0315 | AC-LOC-0007, AC-LOC-0008, AC-LOC-0028, AC-LOC-0029, AC-LOC-0031, AC-LOC-0036, AC-LOC-0058 | TS-LOC-0006 through TS-LOC-0011, TS-LOC-0037 through TS-LOC-0039, TS-LOC-0053, TS-LOC-0054, TS-LOC-0070, TS-LOC-0095, TS-LOC-0096 | DEC-LOC-0005, DEC-LOC-0008, DEC-LOC-0010, DEC-LOC-0015, DEC-LOC-0022, DEC-LOC-0030; CONFLICT-LOC-0005, CONFLICT-LOC-0006, CONFLICT-LOC-0009, CONFLICT-LOC-0010, CONFLICT-LOC-0016 |
| Undo, Restore, history | BRULE-LOC-0014, BRULE-LOC-0015; FR-LOC-0111 through FR-LOC-0113, FR-LOC-0115; SEC-LOC-0211, SEC-LOC-0217 | AC-LOC-0012 through AC-LOC-0014, AC-LOC-0035 | TS-LOC-0018 through TS-LOC-0022, TS-LOC-0050 through TS-LOC-0052 | DEC-LOC-0009, DEC-LOC-0011, DEC-LOC-0021; CONFLICT-LOC-0014, CONFLICT-LOC-0015, CONFLICT-LOC-0017 |
| Authorization, API, security | BR-LOC-0006, BR-LOC-0007; BRULE-LOC-0019, BRULE-LOC-0031 through BRULE-LOC-0033; FR-LOC-0114 through FR-LOC-0116, FR-LOC-0122, FR-LOC-0140 through FR-LOC-0143; SEC-LOC-0201 through SEC-LOC-0204, SEC-LOC-0208 through SEC-LOC-0210, SEC-LOC-0213, SEC-LOC-0214, SEC-LOC-0217 through SEC-LOC-0219; NFR-LOC-0302 | AC-LOC-0009, AC-LOC-0010, AC-LOC-0017, AC-LOC-0018, AC-LOC-0042 through AC-LOC-0054, AC-LOC-0061 through AC-LOC-0063 | TS-LOC-0025 through TS-LOC-0030, TS-LOC-0062 through TS-LOC-0079, TS-LOC-0099 through TS-LOC-0103 | DEC-LOC-0005 through DEC-LOC-0007, DEC-LOC-0014, DEC-LOC-0020 through DEC-LOC-0022, DEC-LOC-0029, DEC-LOC-0033 through DEC-LOC-0035; CONFLICT-LOC-0005 through CONFLICT-LOC-0008, CONFLICT-LOC-0018, CONFLICT-LOC-0019, CONFLICT-LOC-0023, CONFLICT-LOC-0029 through CONFLICT-LOC-0031 |
| Rollback, audit, events, architecture | BR-LOC-0005, BR-LOC-0008; BRULE-LOC-0028, BRULE-LOC-0029, BRULE-LOC-0034; FR-LOC-0121, FR-LOC-0134 through FR-LOC-0136, FR-LOC-0144; SEC-LOC-0210, SEC-LOC-0215; NFR-LOC-0301, NFR-LOC-0307, NFR-LOC-0309, NFR-LOC-0310, NFR-LOC-0314 | AC-LOC-0020 through AC-LOC-0024, AC-LOC-0038 through AC-LOC-0040, AC-LOC-0057, AC-LOC-0064 | TS-LOC-0034 through TS-LOC-0036, TS-LOC-0057 through TS-LOC-0059, TS-LOC-0086 through TS-LOC-0092, TS-LOC-0098, TS-LOC-0102 | DEC-LOC-0013, DEC-LOC-0016, DEC-LOC-0025 through DEC-LOC-0028, DEC-LOC-0036; CONFLICT-LOC-0024 through CONFLICT-LOC-0026, CONFLICT-LOC-0028, CONFLICT-LOC-0032 |

## Route traceability

| Route | Requirement | Permission | AC | TS |
|---|---|---|---|---|
| GET `/resources` | FR-LOC-0114, FR-LOC-0140 | `Platform.Localization.View` | AC-LOC-0042 | TS-LOC-0071 |
| GET `/resources/{resourceKey}` | FR-LOC-0114, FR-LOC-0140 | `Platform.Localization.View` | AC-LOC-0043 | TS-LOC-0072 |
| PUT `/resources/{resourceKey}/overrides/{culture}` | FR-LOC-0105, FR-LOC-0132 | `Platform.Localization.Manage` | AC-LOC-0044 | TS-LOC-0073 |
| POST `.../undo` | FR-LOC-0111 | `Platform.Localization.Manage` | AC-LOC-0045 | TS-LOC-0074 |
| POST `.../restore-default` | FR-LOC-0112 | `Platform.Localization.Manage` | AC-LOC-0046 | TS-LOC-0075 |
| GET `/resources/{resourceKey}/history` | FR-LOC-0115 | `Platform.Localization.ViewHistory` | AC-LOC-0047 | TS-LOC-0076 |
| POST `/preview` | FR-LOC-0116 | `Platform.Localization.Manage` | AC-LOC-0048 | TS-LOC-0077 |
| GET `/effective` | FR-LOC-0106, FR-LOC-0142 | authenticated trusted live Tenant; no administrative permission | AC-LOC-0049, AC-LOC-0062 | TS-LOC-0078, TS-LOC-0101 |
| POST `/effective/batch` | FR-LOC-0107, FR-LOC-0142 | authenticated trusted live Tenant; no administrative permission | AC-LOC-0050, AC-LOC-0062 | TS-LOC-0079, TS-LOC-0101 |

## Permission and persistence traceability

| Focus | Acceptance | Tests |
|---|---|---|
| View positive/negative | AC-LOC-0051, AC-LOC-0052 | TS-LOC-0062 |
| Manage/Preview/Undo/Restore | AC-LOC-0036, AC-LOC-0051, AC-LOC-0052 | TS-LOC-0063, TS-LOC-0070 |
| ViewHistory positive/negative | AC-LOC-0051, AC-LOC-0052 | TS-LOC-0064 |
| Anonymous/private/live/cross-Tenant | AC-LOC-0037, AC-LOC-0053, AC-LOC-0054 | TS-LOC-0065 through TS-LOC-0069 |
| Settings bootstrap/concurrent initialization | AC-LOC-0026, AC-LOC-0027 | TS-LOC-0041 through TS-LOC-0044 |
| Tuple/inactive/history/version constraints | AC-LOC-0055 | TS-LOC-0045 through TS-LOC-0048, TS-LOC-0060, TS-LOC-0061 |
| Concurrent update/Undo/Restore | AC-LOC-0056 | TS-LOC-0049 through TS-LOC-0052 |
| UTF-16/fingerprint/bigint | AC-LOC-0029 through AC-LOC-0031, AC-LOC-0058 | TS-LOC-0038 through TS-LOC-0040, TS-LOC-0053 through TS-LOC-0056 |
| Migration upgrade/downgrade/reapply | AC-LOC-0057 | TS-LOC-0057 through TS-LOC-0059 |

## Decision coverage

Every DEC-LOC-0001 through DEC-LOC-0036 is defined in decisions-approved.md and covered by the theme/route/permission/persistence mappings above. Every CONFLICT-LOC-0001 through CONFLICT-LOC-0032 maps affected sources, FP-004 identifiers, decision, status, milestone, and dependency in that document. No obsolete decision-required reference or unresolved M2 contract blocker remains.

| Decision | Requirements | Acceptance | Tests | Conflict(s) |
|---|---|---|---|---|
| DEC-LOC-0001 | FR-LOC-0102 | AC-LOC-0025 | TS-LOC-0001, TS-LOC-0002 | CONFLICT-LOC-0001, CONFLICT-LOC-0003, CONFLICT-LOC-0022 |
| DEC-LOC-0002 | FR-LOC-0104, FR-LOC-0127 | AC-LOC-0001, AC-LOC-0005 | TS-LOC-0003, TS-LOC-0014 | CONFLICT-LOC-0004, CONFLICT-LOC-0021 |
| DEC-LOC-0003 | FR-LOC-0125 | AC-LOC-0026 | TS-LOC-0041 | CONFLICT-LOC-0013 |
| DEC-LOC-0004 | FR-LOC-0117 | AC-LOC-0016 | TS-LOC-0016 | CONFLICT-LOC-0012 |
| DEC-LOC-0005 | SEC-LOC-0203, SEC-LOC-0204 | AC-LOC-0009, AC-LOC-0028 | TS-LOC-0006, TS-LOC-0029 | CONFLICT-LOC-0005, CONFLICT-LOC-0006 |
| DEC-LOC-0006 | SEC-LOC-0208 | AC-LOC-0051, AC-LOC-0052 | TS-LOC-0062 through TS-LOC-0064 | CONFLICT-LOC-0018 |
| DEC-LOC-0007 | FR-LOC-0140 | AC-LOC-0042 through AC-LOC-0050 | TS-LOC-0071 through TS-LOC-0079 | CONFLICT-LOC-0019 |
| DEC-LOC-0008 | FR-LOC-0109 | AC-LOC-0008, AC-LOC-0058 | TS-LOC-0007, TS-LOC-0053, TS-LOC-0054, TS-LOC-0095, TS-LOC-0096 | CONFLICT-LOC-0009 |
| DEC-LOC-0009 | FR-LOC-0111 | AC-LOC-0012 | TS-LOC-0018 through TS-LOC-0020 | CONFLICT-LOC-0015 |
| DEC-LOC-0010 | FR-LOC-0119, FR-LOC-0130 | AC-LOC-0020, AC-LOC-0031 | TS-LOC-0034, TS-LOC-0039 | CONFLICT-LOC-0016 |
| DEC-LOC-0011 | FR-LOC-0112, FR-LOC-0113 | AC-LOC-0013, AC-LOC-0014 | TS-LOC-0021, TS-LOC-0022 | CONFLICT-LOC-0014, CONFLICT-LOC-0017 |
| DEC-LOC-0012 | FR-LOC-0120, FR-LOC-0133 | AC-LOC-0019 | TS-LOC-0033, TS-LOC-0080 through TS-LOC-0085 | CONFLICT-LOC-0020 |
| DEC-LOC-0013 | FR-LOC-0135 | AC-LOC-0039 | TS-LOC-0089, TS-LOC-0090 | CONFLICT-LOC-0025 |
| DEC-LOC-0014 | FR-LOC-0122 | AC-LOC-0010 | TS-LOC-0028, TS-LOC-0099 | CONFLICT-LOC-0007, CONFLICT-LOC-0008 |
| DEC-LOC-0015 | FR-LOC-0101 | AC-LOC-0023 | TS-LOC-0004 | CONFLICT-LOC-0010 |
| DEC-LOC-0016 | FR-LOC-0123, FR-LOC-0140 | AC-LOC-0024 | TS-LOC-0036 | CONFLICT-LOC-0002, CONFLICT-LOC-0024 |
| DEC-LOC-0017 | FR-LOC-0138 | AC-LOC-0041 | TS-LOC-0093, TS-LOC-0094 | CONFLICT-LOC-0003 |
| DEC-LOC-0018 | FR-LOC-0137 | AC-LOC-0060 | TS-LOC-0097 | CONFLICT-LOC-0011 |
| DEC-LOC-0019 | FR-LOC-0126 | AC-LOC-0026, AC-LOC-0027 | TS-LOC-0041 through TS-LOC-0044 | CONFLICT-LOC-0013 |
| DEC-LOC-0020 | FR-LOC-0132 | AC-LOC-0033, AC-LOC-0034 | TS-LOC-0073 | CONFLICT-LOC-0014 |
| DEC-LOC-0021 | FR-LOC-0111 | AC-LOC-0035 | TS-LOC-0020, TS-LOC-0074 | CONFLICT-LOC-0015 |
| DEC-LOC-0022 | FR-LOC-0116, SEC-LOC-0213 | AC-LOC-0036 | TS-LOC-0070, TS-LOC-0077 | CONFLICT-LOC-0005 |
| DEC-LOC-0023 | FR-LOC-0133 | AC-LOC-0019, AC-LOC-0059 | TS-LOC-0080 through TS-LOC-0084 | CONFLICT-LOC-0020 |
| DEC-LOC-0024 | FR-LOC-0124 | AC-LOC-0037 | TS-LOC-0068 | CONFLICT-LOC-0027 |
| DEC-LOC-0025 | FR-LOC-0134 | AC-LOC-0038 | TS-LOC-0086 through TS-LOC-0088 | CONFLICT-LOC-0028 |
| DEC-LOC-0026 | FR-LOC-0135, SEC-LOC-0215 | AC-LOC-0039 | TS-LOC-0089, TS-LOC-0090 | CONFLICT-LOC-0025 |
| DEC-LOC-0027 | FR-LOC-0136 | AC-LOC-0040 | TS-LOC-0091 | CONFLICT-LOC-0025, CONFLICT-LOC-0026 |
| DEC-LOC-0028 | FR-LOC-0121 | AC-LOC-0021 | TS-LOC-0092 | CONFLICT-LOC-0026 |
| DEC-LOC-0029 | FR-LOC-0122 | AC-LOC-0010 | TS-LOC-0099 | CONFLICT-LOC-0007, CONFLICT-LOC-0023 |
| DEC-LOC-0030 | FR-LOC-0101 | AC-LOC-0001 | TS-LOC-0004 | CONFLICT-LOC-0010 |
| DEC-LOC-0031 | FR-LOC-0137 | AC-LOC-0060 | TS-LOC-0097 | CONFLICT-LOC-0011 |
| DEC-LOC-0032 | FR-LOC-0138 | AC-LOC-0041 | TS-LOC-0093, TS-LOC-0094 | CONFLICT-LOC-0003 |
| DEC-LOC-0033 | FR-LOC-0141 | AC-LOC-0061 | TS-LOC-0100 | CONFLICT-LOC-0029 |
| DEC-LOC-0034 | FR-LOC-0142, SEC-LOC-0219 | AC-LOC-0062 | TS-LOC-0101 | CONFLICT-LOC-0030 |
| DEC-LOC-0035 | FR-LOC-0143 | AC-LOC-0063 | TS-LOC-0103 | CONFLICT-LOC-0031 |
| DEC-LOC-0036 | FR-LOC-0144 | AC-LOC-0064 | TS-LOC-0102 | CONFLICT-LOC-0032 |

### Explicit range-member coverage

The following identifiers are called out individually because they otherwise appear only inside an inclusive range above; each inherits that row's complete requirement-to-verification mapping:

- Catalog/domain: BRULE-LOC-0004, BRULE-LOC-0007, BRULE-LOC-0011, BRULE-LOC-0021, BRULE-LOC-0022, BRULE-LOC-0025; SEC-LOC-0205, SEC-LOC-0206; AC-LOC-0013, AC-LOC-0021, AC-LOC-0022, AC-LOC-0033, AC-LOC-0039.
- Catalog/resolution tests: TS-LOC-0002, TS-LOC-0003, TS-LOC-0007, TS-LOC-0008, TS-LOC-0009, TS-LOC-0010, TS-LOC-0013, TS-LOC-0014, TS-LOC-0015.
- History/security tests: TS-LOC-0019, TS-LOC-0020, TS-LOC-0021, TS-LOC-0026, TS-LOC-0027, TS-LOC-0028, TS-LOC-0029, TS-LOC-0035.
- SQL/settings tests: TS-LOC-0042, TS-LOC-0043, TS-LOC-0046, TS-LOC-0047, TS-LOC-0051, TS-LOC-0055, TS-LOC-0058.
- Authorization/cache/audit tests: TS-LOC-0066, TS-LOC-0067, TS-LOC-0081, TS-LOC-0082, TS-LOC-0083, TS-LOC-0084, TS-LOC-0087, TS-LOC-0088, TS-LOC-0089, TS-LOC-0090, TS-LOC-0091.
- Decision/conflict members: DEC-LOC-0027; CONFLICT-LOC-0002, CONFLICT-LOC-0003, CONFLICT-LOC-0007, CONFLICT-LOC-0012.

## External sources

| Source | Applied constraint |
|---|---|
| Master Business Rules BR-PLT-0001, BR-PLT-0003 through BR-PLT-0005, BR-PLT-0007 | isolation, no physical deletion, audit, UTC, localization independence |
| Master NFR-0200, NFR-0400, NFR-0500, NFR-0501, NFR-0602, NFR-0701 | scale, atomicity, audit, traceability, English/Arabic |
| ADR-001/003/004/010/011/012 | modular/Clean/CQRS/repository/UoW/composition boundaries |
| ADR-005/006 | trusted current Tenant and JWT/permission authority |
| ADR-007 | Angular, English/Arabic, RTL, regional-format support; runtime deferred |
| ADR-008 | EF Core, SQL Server, migrations, rowversion, real provider tests |
| ADR-009 | immutable past-tense post-commit events; FP-004 metadata interpretation documented |
| FP-001 | code-owned permissions, tenant/support planes, retention/audit/concurrency |
| FP-002 | generic authentication mappings, live Active eligibility, safe failures |
| FP-003 | Tenant lifecycle authority, Active-only use, concurrency, audit dependency |
