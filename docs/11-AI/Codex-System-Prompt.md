.github# Codex System Prompt

**Document ID:** DOC-AI-001

**Version:** 1.0

**Status:** Approved

**Audience:** AI Coding Assistants (Codex, ChatGPT, Claude Code, Cursor AI, GitHub Copilot)

---

# Purpose

This document defines the permanent operating rules for AI-assisted development of **SSAS ERP V2**.

It is the highest-level instruction document for AI implementation.

Every implementation session shall begin by reading this document.

Sprint prompts provide task-specific instructions.

This document provides global project rules.

---

# Project Overview

Project Name

SSAS ERP V2

Product Type

Multi-Tenant Software as a Service (SaaS) ERP

Architecture

Modular Monolith

Future Migration

Microservices

Frontend

Angular

Backend

ASP.NET Core (.NET Latest LTS)

Database

Microsoft SQL Server

Hosting

Cloud First

Authentication

JWT

Authorization

Permission Based RBAC

Language Support

English

Arabic

---

# Primary Objective

Generate production-quality code.

The objective is NOT to generate demonstrations, examples, tutorials, or prototypes.

Every generated file shall be suitable for production deployment.

---

# AI Responsibilities

The AI shall:

- Read project documentation before implementation.
- Preserve architecture.
- Follow development standards.
- Produce maintainable code.
- Generate secure code.
- Keep documentation synchronized.
- Avoid assumptions.
- Report conflicts immediately.

---

# Documentation Priority

If multiple documents exist, the following precedence applies.

1. Development Standards
2. Architecture Documents
3. Master Product Specification
4. Functional Specifications
5. Feature Packages
6. Sprint Documents
7. Testing Specifications

Lower-priority documents shall never override higher-priority documents.

---

# Documents to Read Before Implementation

Always review:

```
docs/08-Development/Development-Standards.md

docs/03-Architecture/*

docs/00-Master-Product-Specification/*

docs/12-Feature-Packages/*

Current Sprint Document

Current Functional Specification
```

Do not begin implementation until these documents have been reviewed.

---

# Non-Negotiable Rules

The AI shall never:

- Break Clean Architecture.
- Break module boundaries.
- Introduce circular dependencies.
- Access another module's database directly.
- Move business logic into controllers.
- Move business logic into repositories.
- Duplicate business logic.
- Ignore validation rules.
- Ignore authorization.
- Ignore tenant isolation.
- Hard-code secrets.
- Disable security.
- Change project structure without approval.

---

# Architecture Rules

The application SHALL remain a Modular Monolith.

Each module owns:

- Domain
- Application
- Infrastructure
- API
- Contracts

Cross-module communication shall occur only through contracts or domain events.

The Domain layer shall have no dependency on Infrastructure.

---

# Multi-Tenancy Rules

Tenant isolation is mandatory.

Every tenant-owned entity shall include TenantId.

The current tenant shall be resolved from the authenticated user context.

Queries shall never expose data belonging to another tenant.

Any attempt to bypass tenant isolation shall be treated as a critical defect.

---

# Coding Standards

Generated code shall follow:

- SOLID principles
- Clean Architecture
- CQRS
- Dependency Injection
- Repository Pattern (where approved)
- FluentValidation
- Structured Logging
- Global Exception Handling

Business logic belongs only in the Application and Domain layers.

---

# Naming Standards

Projects

```
SSAS.Platform.API
SSAS.Platform.Application
SSAS.Platform.Domain
SSAS.Platform.Infrastructure
```

Namespaces

```
SSAS.Modules.Platform.Application.Commands
```

Commands

```
CreateEmployeeCommand
```

Queries

```
GetEmployeeByIdQuery
```

Handlers

```
CreateEmployeeCommandHandler
```

Entities

```
Employee
```

DTOs

```
EmployeeDto
```

Interfaces

```
IEmployeeRepository
```

---

# API Rules

REST APIs only.

JSON payloads.

Versioned endpoints.

Consistent error responses using ProblemDetails.

Controllers shall remain thin.

Controllers shall delegate all work to the Application layer.

---

# Database Rules

Use Entity Framework Core by default.

Stored Procedures may be used only when justified by measurable performance or operational requirements.

All tenant-owned tables shall include TenantId.

Audit fields are mandatory where applicable:

- CreatedDateUtc
- CreatedBy
- ModifiedDateUtc
- ModifiedBy
- DeletedDateUtc
- DeletedBy
- RowVersion

Soft delete shall be used unless a documented requirement states otherwise.

---

# Security Rules

Passwords shall never be stored in plain text.

Authentication is mandatory for protected endpoints.

Authorization is mandatory for protected resources.

Sensitive data shall never be logged.

Parameterized database access is mandatory.

---

# Testing Rules

Every implemented feature shall include:

- Unit Tests
- Integration Tests (when applicable)
- API Tests (when applicable)

Critical business workflows shall be covered by automated tests.

---

# Documentation Rules

Whenever a feature changes:

Update:

- Functional Specification
- API Specification
- Database Specification
- Testing Specification

Documentation shall remain synchronized with implementation.

---

# AI Decision Rules

When documentation is incomplete:

DO NOT GUESS.

Instead:

1. Stop implementation.
2. Report the missing information.
3. Request clarification.
4. Resume only after approval.

---

# Refactoring Rules

The AI may improve:

- Readability
- Maintainability
- Performance

The AI shall not change externally observable behavior unless the documentation requires it.

Any architectural refactoring requires approval.

---

# Commit Rules

Each implementation phase shall produce focused commits.

Example:

```
feat(platform): implement authentication infrastructure

feat(hr): add employee entity

fix(finance): correct journal posting validation

refactor(shared): simplify tenant resolution
```

Large unrelated commits are prohibited.

---

# Pull Request Rules

Every pull request shall include:

- Summary
- Requirement IDs
- Feature Package ID
- Files Changed
- Tests Executed
- Documentation Updated
- Risks
- Breaking Changes (if any)

---

# Quality Gates

The AI shall verify before completion:

- Solution builds successfully.
- Tests pass.
- No compiler warnings introduced without justification.
- No circular dependencies.
- Dependency rules respected.
- Documentation updated.
- Security requirements satisfied.

---

# Stop Conditions

Immediately stop implementation if:

- Documentation conflicts.
- Missing architecture.
- Missing feature specification.
- Undefined business rules.
- Undefined database design.
- Undefined API contract.

Never invent missing requirements.

---

# Completion Criteria

A feature is complete only when:

✓ Code compiles.

✓ Tests pass.

✓ Documentation updated.

✓ Quality gates passed.

✓ Architecture preserved.

✓ Security preserved.

✓ Tenant isolation verified.

✓ Ready for code review.

---

# Guiding Principle

Prefer correctness over speed.

Prefer maintainability over cleverness.

Prefer explicit design over hidden behavior.

When uncertain, stop and request clarification rather than making assumptions.

The long-term maintainability of SSAS ERP V2 always takes precedence over short-term implementation speed.