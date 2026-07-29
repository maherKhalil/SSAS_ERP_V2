# SSAS ERP V2 - AI Coding Instructions

## Project Overview

SSAS ERP V2 is a production-grade, multi-tenant SaaS ERP system.

Primary technologies:

- ASP.NET Core (.NET Latest LTS)
- Angular
- SQL Server
- Entity Framework Core
- JWT Authentication

Architecture:

- Modular Monolith
- Clean Architecture
- CQRS
- Domain-Driven Design principles
- Future Microservice Ready

This repository contains production code only.

Do not generate demo, sample, tutorial, or prototype code.

---

# Primary Objective

Generate code that is:

- Production ready
- Maintainable
- Secure
- Testable
- Consistent
- Well documented

Favor readability over clever implementations.

---

# Documentation First

Before implementing a feature, consult the project documentation.

Priority order:

1. `docs/08-Development/Development-Standards.md`
2. `docs/03-Architecture/*`
3. `docs/00-Master-Product-Specification/*`
4. `docs/12-Feature-Packages/*`
5. Current Sprint documentation
6. Functional Specification

If documentation conflicts, stop and request clarification.

Do not invent requirements.

---

# Architecture Rules

Always preserve Clean Architecture.

Dependencies:

Presentation

↓

Application

↓

Domain

↑

Infrastructure

The Domain layer must never depend on Infrastructure.

Business logic belongs only in the Domain and Application layers.

---

# Module Rules

Each module owns:

- Domain
- Application
- Infrastructure
- API
- Contracts

Never access another module's Infrastructure.

Never access another module's database tables directly.

Cross-module communication must use contracts or domain events.

---

# Multi-Tenancy

Tenant isolation is mandatory.

All tenant-owned entities shall contain TenantId.

Never expose another tenant's data.

Never bypass tenant filtering.

---

# Coding Standards

Use:

- Dependency Injection
- CQRS
- FluentValidation
- Global Exception Handling
- Structured Logging

Avoid:

- Static business logic
- Service locators
- Business logic in controllers
- Business logic in repositories
- Hard-coded configuration
- Hard-coded secrets

---

# API Standards

REST APIs only.

Use:

- JSON
- Versioning
- ProblemDetails
- Proper HTTP status codes

Controllers must remain thin.

Delegate business logic to the Application layer.

---

# Database Standards

Use Entity Framework Core by default.

Stored Procedures may be used only when justified by documented performance requirements.

Audit fields should be included where applicable.

Prefer soft delete unless a requirement specifies physical deletion.

---

# Security

Always enforce:

- Authentication
- Authorization
- Input validation
- Parameterized database access

Never:

- Store passwords in plain text
- Log secrets
- Disable security checks

---

# Testing

New functionality should include appropriate automated tests.

Business logic should be unit tested.

Critical workflows should include integration tests.

---

# Naming Conventions

Projects

```
SSAS.Platform.API
SSAS.Platform.Application
SSAS.Platform.Domain
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

# Code Quality

Generate:

- Small methods
- Clear names
- XML documentation for public APIs where appropriate
- Consistent formatting

Avoid unnecessary complexity.

---

# Git

Keep changes focused.

Do not modify unrelated files.

Respect existing project structure.

---

# If Unsure

Do not guess.

Instead:

- Stop implementation.
- Explain what information is missing.
- Ask for clarification.

Correctness is more important than speed.