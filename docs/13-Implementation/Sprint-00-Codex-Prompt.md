# Sprint 00 - Codex Implementation Guide

**Document ID:** DOC-IMP-001

**Version:** 1.0

**Status:** Approved

**Audience:** Codex AI, Developers, Architects

---

# Purpose

This document instructs Codex how to implement Sprint 00.

It defines:

- Required documents
- Document precedence
- Implementation order
- Coding rules
- Stop conditions
- Review checkpoints

This document is the execution contract between the project documentation and AI-assisted implementation.

---

# Objective

Implement the complete foundation of SSAS ERP V2.

Sprint 00 establishes the application framework only.

No business functionality shall be implemented.

---

# Authoritative Documents

Codex shall read the following documents before generating any code.

Priority Order:

1. docs/08-Development/Development-Standards.md
2. docs/03-Architecture/Solution-Structure.md
3. docs/03-Architecture/Solution-Architecture.md
4. docs/03-Architecture/Clean-Architecture.md
5. docs/03-Architecture/Modular-Monolith.md
6. docs/13-Implementation/Sprint-00-Foundation.md
7. docs/00-Master-Product-Specification/*
8. docs/14-Engineering/ADR/*

If two documents conflict:

Development Standards take precedence.

Architecture documents take precedence over Functional documents.

Functional documents take precedence over implementation assumptions.

Codex shall never guess architectural decisions.

---

# Sprint Scope

Implement only:

- Solution
- Projects
- References
- Dependency Injection
- Configuration
- Logging
- Authentication Infrastructure
- Authorization Infrastructure
- Middleware
- Health Checks
- Swagger
- Docker
- GitHub Actions
- Testing Infrastructure
- BuildingBlocks

Do NOT implement:

- Employee
- HR
- Finance
- Reports
- CRUD APIs
- Database Tables
- Business Logic

---

# Implementation Order

Codex shall implement in the following order.

## Phase 1

Create repository structure.

Create Visual Studio solution.

Create all projects.

Configure project references.

Verify build.

Commit.

---

## Phase 2

Implement BuildingBlocks.

Create:

- BaseEntity
- AggregateRoot
- Entity
- Result
- Error
- DomainEvent
- Guard
- ValueObject
- Pagination
- Specifications

Verify build.

Commit.

---

## Phase 3

Configure Host.

Implement:

- Dependency Injection
- Configuration
- Swagger
- Health Checks
- HTTPS
- Middleware
- Logging

Verify build.

Commit.

---

## Phase 4

Authentication Infrastructure.

Implement:

- JWT
- Claims
- Current User
- Current Tenant
- Password Hashing
- Authentication Middleware

No Login endpoint.

Verify build.

Commit.

---

## Phase 5

Authorization Infrastructure.

Implement:

- Policies
- Permission Framework
- Authorization Handlers
- Role Framework

Verify build.

Commit.

---

## Phase 6

Persistence Infrastructure.

Implement:

- EF Core
- Repository Base
- Unit of Work
- Migration Infrastructure

No business entities.

Verify build.

Commit.

---

## Phase 7

Testing Infrastructure.

Create:

- Unit Tests
- Integration Tests
- Architecture Tests
- API Tests

Verify build.

Commit.

---

## Phase 8

DevOps.

Create:

- Dockerfile
- docker-compose.yml
- GitHub Actions

Verify build.

Commit.

---

# Coding Rules

Codex shall follow:

- Clean Architecture
- SOLID
- CQRS
- Dependency Injection
- Modular Monolith
- Development Standards

Business logic shall never exist in:

- Controllers
- Repositories
- Middleware

---

# Naming Rules

Projects

```
SSAS.Platform.API
SSAS.Platform.Application
SSAS.Platform.Domain
SSAS.Platform.Infrastructure
```

Namespaces

```
SSAS.Modules.Platform.Application
```

Classes

PascalCase

Interfaces

IRepository

Commands

CreateEmployeeCommand

Queries

GetEmployeeQuery

Handlers

CreateEmployeeCommandHandler

---

# AI Rules

Codex shall not:

- Rename projects.
- Change folder structure.
- Introduce additional architectures.
- Introduce unnecessary libraries.
- Add sample business code.
- Add demo entities.
- Add fake APIs.

Codex shall only implement approved architecture.

---

# Allowed Libraries

Examples:

- ASP.NET Core
- Entity Framework Core
- FluentValidation
- Serilog
- xUnit
- Swashbuckle

New libraries require architectural approval.

---

# Quality Gates

Every phase must satisfy:

- Solution builds
- Tests compile
- No circular references
- No architecture violations
- No compiler errors

---

# Stop Conditions

Codex shall stop immediately if:

- Documentation conflicts.
- Architecture conflict detected.
- Missing specification.
- Missing Requirement IDs.
- Undefined project structure.
- Undefined dependency.

Codex shall report the issue instead of making assumptions.

---

# Deliverables

At the end of Sprint 00:

- Solution
- Project Structure
- Shared Kernel
- Logging
- Authentication Infrastructure
- Authorization Infrastructure
- Middleware
- Health Checks
- Swagger
- Docker
- GitHub Actions
- Testing Infrastructure

The application shall start successfully without business modules.

---

# Output Requirements

For each completed phase, Codex shall provide:

- Summary of implemented work
- Files created
- Files modified
- Build status
- Test status
- Outstanding issues
- Risks (if any)
- Next recommended step

---

# Commit Strategy

One commit per completed phase.

Recommended commit messages:

```
feat(sprint-00): create solution structure

feat(sprint-00): implement building blocks

feat(sprint-00): configure host application

feat(sprint-00): add authentication infrastructure

feat(sprint-00): add authorization infrastructure

feat(sprint-00): configure persistence

feat(sprint-00): add testing infrastructure

feat(sprint-00): configure docker and CI
```

Never combine multiple phases into a single commit.

---

# Definition of Success

Sprint 00 is complete when:

- Solution builds successfully.
- All projects compile.
- Dependency Injection is configured.
- Swagger is available.
- Health endpoints return Healthy.
- Logging is operational.
- Authentication infrastructure is configured.
- Authorization infrastructure is configured.
- Docker image builds successfully.
- CI pipeline passes.
- No business functionality exists.