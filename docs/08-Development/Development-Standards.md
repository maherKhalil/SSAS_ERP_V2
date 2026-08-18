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

## Optimistic Concurrency (RowVersion) Transport

SQL Server `rowversion` concurrency tokens exposed or accepted over HTTP use one canonical Platform-wide encoding:

- Encoding: RFC 4648 **padded** Base64 (standard alphabet), compatible with .NET `System.Text.Json` byte-array serialization.
- Rejected: Base64Url, hexadecimal, surrounding whitespace, non-canonical encodings, and any value that does not decode to exactly 8 bytes.
- A supplied token must be nonblank, decode to exactly 8 bytes, and re-encode byte-for-byte to the submitted canonical form. Server output is always canonical.
- Malformed transport is `400 platform.rowversion_invalid`.
- A missing token where one is required is `400 request.invalid`.
- A valid but stale token is `409 concurrency.conflict`.

This convention is Platform-wide and reusable across features. It must be implemented once in a neutral shared Platform/Host transport component; features must not define their own competing rowversion codec. (The existing localization-owned `LocalizationRowVersionCodec` predates this convention and should be extracted into the shared component so all features share one codec.)

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

BranchId required on branch-owned data.

---

## Branch Ownership

Every tenant entity SHALL be explicitly classified as tenant-global or branch-owned. Unclassified is a defect.

Branch-owned entities implement `IBranchOwnedEntity` in addition to `ITenantOwnedEntity` and carry both `TenantId` and `BranchId`.

`BranchId` is assigned by the server from the authenticated execution context. It SHALL NOT be accepted from a request DTO, header, form field, or token claim, and SHALL NOT change after creation.

Branch-scoped queries SHALL carry an explicit `BranchId` predicate over the current branch or an authorized branch set. Omitting the predicate is a defect.

No foreign key SHALL be created from the platform database to the tenant `Branches` table. `BranchId` is an opaque cross-database identifier.

Reference: ADR-023

---

# Testing

Unit Tests

Integration Tests

API Tests

UI Tests

Performance Tests

Every critical business workflow shall be covered by automated tests.

## Query plan capture

Tests SHALL NOT assert on plan-cache residency, and SHALL NOT read server-wide plan DMVs
(`sys.dm_exec_query_stats` and related) to obtain a query plan. Plan capture SHALL be in-session and
deterministic. `tests/Integration.Tests/QueryPlanCapture.cs` is the supported mechanism.

The plan cache is a cache, not a record: it holds what the server still happens to remember. Reads of it were
measured returning nothing on a developer instance running this suite, twice, so a test built on it cannot
distinguish "the query scanned" from "the server forgot" — and a test that cannot tell those apart is not a
guard. `QueryPlanCapture` instead replays the exact statement production issued, under `SET STATISTICS XML ON`,
and reads the actual plan back from the same session, where it cannot be evicted.

This rule does not depend on any particular explanation for why the cache empties. That question was
investigated and left INCONCLUSIVE; the measured unreliability of the reads is sufficient on its own.

## Test gates and stale binaries

A test gate SHALL NOT be trusted unless the compiled test output is newer than every source file under test.
`--no-build` is permitted only immediately after a build in the same sequence. A green run on a stale binary
reports success for code that was never executed.

This is not hypothetical. A full 376/376 Integration run passed while the fix under test had not been
compiled in — the Debug output predated the source by four minutes, because the preceding build had been
`-c Release`, which does not refresh the Debug output the test runner loads. The leak that fix was written to
prevent appeared in that same green run, which is the only reason the stale binary was noticed at all.

## Test catalogs

`SSAS_` is a RESERVED PREFIX for test databases. Do not name a scratch or personal database with it. Cleanup
failures are reported by `CatalogLeakGuardTests`; the reaping procedure is in
`tests/Integration.Tests/README.md`.

Suites SHALL be run serially against one SQL Server instance. Concurrent Integration and API runs will fail
the catalog leak guard, which cannot distinguish a sibling suite's live catalog from a leaked one.

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