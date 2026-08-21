# SSAS ERP V2 - AI & Developer Entry Point

Version 2.0

---

# Purpose

This document is the mandatory entry point for:

- Developers
- Architects
- AI Coding Agents
- Code Reviewers
- Technical Leads

Every implementation begins here.

---

# Repository Mission

Build an enterprise-grade ERP platform that is:

- Multi-Tenant
- Secure
- Cloud Ready
- Modular
- Maintainable
- AI Assisted
- Production Ready

---

# Repository Authority

The Git repository is the single source of truth.

Implementation shall follow the repository documentation.

If implementation conflicts with documentation:

Documentation wins.

If documentation conflicts internally:

Architecture Decision Records (ADRs) are authoritative.

---

# Documentation Precedence

When documents disagree, resolve the conflict using this order, highest authority first:

1. Accepted Architecture Decision Record (ADR)
2. Approved Feature Package
3. Master Product Specification / Requirement Catalog / Functional Specification
4. Historical Sprint / planning documentation

This refines the rule above: ADRs remain authoritative for internal conflicts, an approved Feature Package supersedes older functional or planning text within its scope, and historical Sprint or planning notes never override an approved Feature Package, the Master Product Specification, or an ADR.

---

# Mandatory Reading Order

Before writing code, read the following in order:

1. START-HERE.md
2. README.md
3. docs/README.md
4. docs/14-Engineering/Architecture-Principles.md
5. docs/14-Engineering/ADR/*
6. docs/11-AI/Codex-System-Prompt.md
7. docs/11-AI/AI-Implementation-Workflow.md
8. docs/08-Development/Development-Standards.md
9. docs/03-Architecture/*
10. docs/00-Master-Product-Specification/*
11. Current Sprint
12. Current Feature Package
13. Functional Specification

Do not begin implementation until the required documents have been reviewed.

---

# Development Workflow

Business Requirement

↓

Architecture

↓

Feature Package

↓

Database Design

↓

API Design

↓

Frontend

↓

Testing

↓

Review

↓

Release

---

# AI Workflow

Read Documentation

↓

Understand Scope

↓

Validate Architecture

↓

Implement

↓

Compile

↓

Run Tests

↓

Update Documentation (if required)

↓

Commit

↓

Request Review

---

# Architecture Rules

Every implementation shall comply with:

- Architecture Principles
- All ADRs
- Development Standards
- Coding Standards

Never violate an accepted ADR.

---

# Golden Rules

- Documentation drives implementation.
- One feature package at a time.
- Preserve module boundaries.
- Never bypass tenant isolation.
- Never place business logic inside controllers.
- Never expose DbContext outside Infrastructure.
- Never expose EF Core to the Application layer.
- Use CQRS for all application use cases.
- Follow the Feature Package Template.
- Prefer maintainability over shortcuts.

---

# Build Requirements

Every implementation must:

- Compile successfully.
- Pass automated tests.
- Introduce no build errors.
- Maintain architecture compliance.
- Preserve backward compatibility unless explicitly approved.

---

# Definition of Done

A task is complete only when:

- Code compiles.
- Tests pass.
- Coding standards are satisfied.
- Documentation is updated if required.
- No undocumented TODO items remain.
- The feature is ready for review.

---

# Stop Conditions

Stop implementation immediately if:

- Sprint scope is complete.
- Requirements are ambiguous.
- Documentation conflicts cannot be resolved.
- A new architectural decision is required.
- A new ADR is needed.

Do not continue beyond the approved sprint.

---

# Sprint Order

Sprint-00 Foundation

↓

Sprint-01 Platform

↓

Sprint-02 HR

↓

Sprint-03 General Ledger

↓

Sprint-04 Payroll

↓

Remaining ERP Modules

Each sprint must be reviewed and approved before the next begins.

---

# Current Implementation State

Current implementation state is tracked through approved Feature Packages and the Git history, not through a single "current sprint" marker in this document.

Sprint-00 (Foundation) and the Sprint-01 Platform feature packages are delivered and merged: FP-001 Identity & Access, FP-002 Authentication & Token Lifecycle, FP-003 Tenant Lifecycle, and FP-004 Localization. To determine what exists, read the approved Feature Packages under `docs/17-features/` and the commit history.

The **Branch foundation** (branch persistence, lifecycle, mandatory user branch assignment, and the active-branch session flow) is **delivered and merged to `main`**. Its architecture is recorded in `ADR-023` and its behaviour in `docs/02-Functional/Platform/Branch-Management.md`.

**FP-006 HR Employee is delivered and merged.** `Employee` is the first production `IBranchOwnedEntity` and the first production `ICompanyOwnedEntity`, so the `ADR-023` decisions that were structurally implemented but not runtime-proven are now proven against real SQL: the `ADR-023` LOW-1 obligations are closed by `tests/Integration.Tests/EmployeeBoundarySqlServerTests.cs`, and `ADR-023` decision 22 and `ADR-025` decision 10 are closed by executable guards in `tests/Architecture.Tests/EmployeeReadScopeArchitectureTests.cs` and `tests/Architecture.Tests/CompanyOwnershipArchitectureTests.cs`.

**FP-007 HR Department is delivered and merged** (PR #45). `Department` is the product's first hierarchical aggregate and the first record that takes one ownership dimension while deliberately refusing another — it is company-owned and **not** branch-owned, and an architecture guard asserts that absence. Its architecture is recorded in `ADR-026`. `Employee` now carries a mandatory `DepartmentId` with append-only history, and the Shared→Dedicated cutover manifest covers seven tenant-owned entities, derived by reflection rather than declared.

**FP-008 HR Position is approved for implementation and not yet built.** `docs/17-features/FP-008-hr-position/` is a complete design package: all six owner decisions (`OD-POS-001` … `OD-POS-006`) were closed on 2026-08-21 and all eleven engineering proposals ratified. Three aggregates (`Position`, `JobGrade`, `SalaryGrade`), twelve permissions, twenty routes, and the E3 cutover manifest going from seven entities to eleven. `BR-HR-0006` ("every employee must have one active position") is satisfied **by design** but not yet by any database — `Employee.PositionId` ships `NOT NULL` with **no backfill**, because no production tenant holds Employee rows, and `DEC-POS-0026` requires the migration to verify that fact per tenant database and fail loudly rather than assume it. `ADR-027` is drafted alongside it and is **activated** by the `OD-POS-004` ruling: it sets `decimal(19,4)` as the product's money representation, which General Ledger will inherit.

Do not resume completed work on the basis of an older "current sprint" heading. The repository state and the approved Feature Packages are authoritative for what has been built and what comes next.

---

# Repository Success Criteria

The repository is considered healthy when:

- Documentation is current.
- Architecture remains compliant.
- The solution builds successfully.
- Tests pass.
- Coding standards are followed.
- Security standards are maintained.
- AI-generated code follows project standards.

---

# Final Instruction to AI Agents

Work incrementally.

Make the smallest reasonable change.

Build after every major change.

Run tests before completion.

Respect every Architecture Decision Record.

When in doubt, stop and request clarification rather than making assumptions.