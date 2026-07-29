# Requirements Traceability Matrix (RTM)

**Document ID:** MPS-RTM-001

**Version:** 1.0

**Status:** Approved

---

# Purpose

The Requirements Traceability Matrix (RTM) ensures that every business requirement is fully traceable throughout the lifecycle of the SSAS ERP project.

Each requirement shall be linked to:

- Business Rules
- Functional Features
- UI Screens
- APIs
- Database Tables
- Reports
- Permissions
- Test Cases
- Source Code
- Deployment

This document is the primary impact analysis tool.

---

# Traceability Model

```
Business Need
      │
      ▼
Requirement (REQ)
      │
      ▼
Business Rule (BR)
      │
      ▼
Feature (FR)
      │
      ▼
Workflow (WF)
      │
      ▼
Screen (SCR)
      │
      ▼
API
      │
      ▼
Database
      │
      ▼
Permission
      │
      ▼
Test Case
      │
      ▼
Implementation
```

---

# Traceability Rules

## RTM-0001

Every Requirement shall reference one or more Business Rules.

---

## RTM-0002

Every Functional Feature shall implement one or more Requirements.

---

## RTM-0003

Every Screen shall implement one or more Functional Features.

---

## RTM-0004

Every API shall implement one or more Functional Features.

---

## RTM-0005

Every Database Table shall support one or more Requirements.

---

## RTM-0006

Every Permission shall secure one or more Features.

---

## RTM-0007

Every Test Case shall validate one or more Requirements.

---

## RTM-0008

Every implemented source file should be traceable to a Requirement ID through features, APIs, or modules.

---

# Example Traceability

| Artifact | Identifier |
|----------|------------|
| Requirement | REQ-HR-0001 |
| Business Rule | BR-HR-0001 |
| Feature | FR-HR-0001 |
| Workflow | WF-HR-0001 |
| Screen | SCR-HR-0001 |
| API | API-HR-0001 |
| Database | TBL-HR-Employee |
| Permission | PER-HR-CreateEmployee |
| Report | RPT-HR-0001 |
| Test Case | TC-HR-0001 |

---

# Example Mapping

| Requirement | BR | Feature | Screen | API | Table | Permission | Test |
|-------------|----|---------|--------|-----|-------|------------|------|
| REQ-HR-0001 | BR-HR-0001 | FR-HR-0001 | SCR-HR-0001 | API-HR-0001 | TBL-HR-Employee | PER-HR-CreateEmployee | TC-HR-0001 |
| REQ-HR-0002 | BR-HR-0003 | FR-HR-0002 | SCR-HR-0002 | API-HR-0002 | TBL-HR-Employee | PER-HR-EditEmployee | TC-HR-0002 |
| REQ-GL-0001 | BR-GL-0001 | FR-GL-0001 | SCR-GL-0001 | API-GL-0001 | TBL-GL-JournalEntry | PER-GL-PostJournal | TC-GL-0001 |

---

# Lifecycle

```
Draft

↓

Approved

↓

Implemented

↓

Tested

↓

Released

↓

Maintained

↓

Deprecated
```

Requirements are never deleted.

Deprecated requirements remain in the documentation for historical traceability.

---

# Impact Analysis

Before modifying any Requirement, review:

- Business Rules
- Features
- Workflows
- UI Screens
- APIs
- Database Objects
- Reports
- Permissions
- Test Cases
- Deployment Scripts

Every affected artifact shall be updated before release.

---

# Ownership

The Product Architecture Team owns the RTM.

Development, QA, and Business Analysis teams are responsible for maintaining traceability during implementation.