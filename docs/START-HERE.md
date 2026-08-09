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