# Development Standards

**Document ID:** DOC-DEV-001

**Version:** 1.0

**Status:** Approved

---

# Purpose

This document defines the mandatory development standards for SSAS ERP V2.

All developers, architects, and AI coding assistants (including Codex) shall follow these standards.

These standards ensure consistency, maintainability, scalability, and future migration to microservices.

---

# Scope

These standards apply to:

- Backend Development
- Frontend Development
- Database Development
- API Development
- Testing
- DevOps
- AI Generated Code

---

# Technology Stack

## Backend

.NET (Latest LTS version approved for the project)

Language

C#

Architecture

Clean Architecture

Deployment

ASP.NET Core

REST API

JWT Authentication

---

## Frontend

Angular (Latest LTS version approved for the project)

TypeScript

RxJS

Angular Material

SCSS

---

## Database

Microsoft SQL Server

Entity Framework Core

SQL Scripts

Stored Procedures only when justified by performance or operational requirements.

---

## Infrastructure

Docker

Kubernetes (Future)

Azure

GitHub

GitHub Actions

---

# Architecture Principles

The application SHALL be implemented as a Modular Monolith.

Every module shall be independently deployable in the future.

Every module owns:

- Domain
- Application
- Infrastructure
- API
- Contracts

Modules shall never reference another module's implementation.

Communication occurs through contracts or domain events.

Direct database access between modules is prohibited.

---

# Dependency Rule

Dependencies always point inward.

```
Presentation

↓

Application

↓

Domain

↑

Infrastructure
```

The Domain layer shall never depend on Infrastructure.

---

# Solution Structure

```
src/

Platform/

HR/

Finance/

Shared/

BuildingBlocks/

tests/

docs/
```

Every module follows the same structure.

```
Module/

Module.API

Module.Application

Module.Domain

Module.Infrastructure

Module.Contracts
```

---

# Naming Standards

Projects

SSAS.Platform.API

SSAS.Platform.Application

SSAS.Platform.Domain

SSAS.Platform.Infrastructure

Classes

PascalCase

Interfaces

Prefix I

Example

IEmployeeRepository

Methods

PascalCase

Properties

PascalCase

Variables

camelCase

Private Fields

_prefixCamelCase

Constants

PascalCase

Enums

PascalCase

Namespaces

SSAS.Modules.HR.Application.Commands

---

# Folder Standards

Application

Commands

Queries

DTOs

Interfaces

Validators

Mappings

Behaviors

Domain

Entities

ValueObjects

Events

Specifications

Services

Repositories

Infrastructure

Persistence

Repositories

Configurations

Migrations

API

Controllers

Endpoints

Middleware

Filters

---

# CQRS

Every business operation shall be implemented using:

Command

or

Query

Commands modify data.

Queries never modify data.

---

# Mediation

Mediator pattern is recommended.

Business logic shall never exist inside Controllers.

---

# Validation

Validation shall occur before business logic executes.

Validation errors shall return consistent API responses.

---

# Repository Pattern

Repositories expose aggregate persistence.

Repositories shall not contain business logic.

---

# Unit of Work

Transactions shall be managed centrally.

Business services shall not manually manage transactions.

---

# Entity Framework

Fluent API preferred.

Avoid Data Annotations unless justified.

Lazy Loading disabled.

Explicit Includes preferred.

---

# Exception Handling

Global exception middleware is mandatory.

Business exceptions

Validation exceptions

Infrastructure exceptions

shall be handled consistently.

---

# Logging

Structured logging only.

Never use Console.WriteLine in production code.

Log Levels

Trace

Debug

Information

Warning

Error

Critical

Sensitive information shall never be logged.

---

# Security

Passwords shall never be stored.

Only password hashes.

Authorization required.

JWT mandatory.

HTTPS mandatory.

Parameterized SQL only.

Secrets stored outside source code.

---

# API Standards

REST

Versioned

JSON

Pagination

Filtering

Sorting

Consistent status codes

ProblemDetails for errors

---

# Database Standards

Primary Key

BIGINT IDENTITY

Audit Columns

CreatedDateUtc

CreatedBy

ModifiedDateUtc

ModifiedBy

DeletedDateUtc

DeletedBy

RowVersion

TenantId required on tenant-owned data.

CompanyId required where applicable.

---

# Testing

Unit Tests

Integration Tests

API Tests

UI Tests

Performance Tests

Every critical business workflow shall be covered by automated tests.

---

# Documentation

Every feature shall reference:

Requirement ID

Business Rule

Feature ID

API

Database Table

Test Case

Documentation shall be updated before implementation.

---

# Git Standards

Feature branches only.

Pull Requests mandatory.

Direct commits to the main branch are prohibited.

Every Pull Request shall include:

- Related Requirement IDs
- Related Feature IDs
- Test Evidence
- Documentation Updates

---

# AI Development Rules

AI-generated code shall:

- Follow Clean Architecture.
- Preserve module boundaries.
- Respect Dependency Rules.
- Follow naming conventions.
- Include XML documentation for public APIs.
- Avoid introducing undocumented dependencies.
- Reference Requirement IDs in generated pull requests where applicable.

AI-generated code shall not:

- Access another module's database directly.
- Duplicate business logic.
- Bypass validation.
- Bypass authorization.
- Hard-code configuration values.

---

# Definition of Complete

A feature is considered complete only when:

✓ Requirements implemented.

✓ Business Rules satisfied.

✓ Unit tests pass.

✓ Integration tests pass.

✓ Documentation updated.

✓ Code review approved.

✓ Security review completed.

✓ Deployment verified.

---

# References

DOC-MPS-001 Master Product Specification

DOC-SAD-001 Solution Architecture

DOC-DBD-001 Database Design

DOC-API-001 API Specification

DOC-TST-001 Testing Strategy