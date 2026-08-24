# Solution Structure

Document ID

DOC-SAD-005

Version

1.0

Status

Approved

Owner

Solution Architecture Team

---

# Purpose

This document defines the physical structure of the SSAS ERP solution.

Every project, namespace, folder, dependency, and shared component shall follow this document.

This document is the authoritative reference for creating and maintaining the Visual Studio solution.

AI-assisted development tools (including Codex) shall generate projects according to this specification.

---

# Solution Name

SSAS.ERP.sln

---

# Repository Layout

```

SSAS_ERP_V2

├── docs

├── src

├── tests

├── tools

├── build

├── deployment

├── scripts

├── samples

├── .github

├── README.md

└── LICENSE

```

---

# Source Structure

```

src

│

├── BuildingBlocks

│

├── Platform

│

├── Modules

│

├── Shared

│

└── Host

```

---

# Platform

```

Platform

│

├── SSAS.Platform.API

├── SSAS.Platform.Application

├── SSAS.Platform.Domain

├── SSAS.Platform.Infrastructure

└── SSAS.Platform.Contracts

```

---

# HR Module

```

Modules

└── HR

├── SSAS.HR.API

├── SSAS.HR.Application

├── SSAS.HR.Domain

├── SSAS.HR.Infrastructure

└── SSAS.HR.Contracts

```

---

# Finance Module

```

Modules

└── Finance

├── SSAS.GL.API

├── SSAS.GL.Application

├── SSAS.GL.Domain

└── SSAS.GL.Infrastructure

```

> **`SSAS.GL.Contracts` was removed on 2026-08-23 (FP-011).** `OD-GL-0009` ruled that nothing posts to
> General Ledger in V1 — Payroll (V2) is the first inbound poster — so GL has no cross-module contract
> surface to publish. The project existed empty and was already referenced by GL's API, Application and
> Infrastructure, which is one step closer to the coupling that ruling excluded than an unreferenced stub
> would be: the dependency edges were in place, waiting only for content.
>
> **It returns when Payroll consumes it, shaped by its actual consumer** — the promote-when-needed
> discipline `ADR-027` decision 4 applies to types, applied here to an assembly. Git history preserves the
> skeleton for anyone who needs the archaeology.

---

# Future Modules

```

Modules

Payroll

Inventory

CRM

Projects

Manufacturing

Procurement

Assets

Sales

Purchasing

Reporting

```

Each module follows the same structure.

---

# Host Project

```

Host

│

└── SSAS.Host.API

```

Responsibilities

- Application Startup
- Dependency Injection
- Authentication
- Swagger
- Health Checks
- Middleware
- Configuration
- Module Registration

The Host project shall not contain business logic.

As the composition root, the Host may reference the approved Platform, HR, and GL API and Infrastructure projects for runtime composition only.

---

# BuildingBlocks

Shared infrastructure used by every module.

```

BuildingBlocks

│

├── SSAS.BuildingBlocks.Domain

├── SSAS.BuildingBlocks.Application

├── SSAS.BuildingBlocks.Infrastructure

├── SSAS.BuildingBlocks.Contracts

└── SSAS.BuildingBlocks.SharedKernel

```

Contains

BaseEntity

AggregateRoot

DomainEvent

Result Pattern

Guard Clauses

Pagination

Validation

Common Exceptions

DateTime Provider

Current User

Current Tenant

Specifications

Common Interfaces

---

# Shared Folder

```

Shared

│

├── Localization

├── Resources

├── Themes

├── Templates

├── Email

├── SMS

└── Files

```

---

# Test Projects

```

tests

│

├── Platform.Tests

├── HR.Tests

├── Finance.Tests

├── Integration.Tests

├── API.Tests

├── UI.Tests

├── Performance.Tests

└── Architecture.Tests

```

Every production project shall have corresponding automated tests.

---

# Folder Structure Inside Every Module

Application

```

Commands

Queries

DTOs

Validators

Interfaces

Behaviors

Mappings

Events

Services

Authorization

```

Domain

```

Entities

ValueObjects

Events

Specifications

Repositories

Enums

Exceptions

Services

```

Infrastructure

```

Persistence

Repositories

Configurations

Migrations

Identity

Caching

Messaging

Storage

BackgroundJobs

```

API

```

Controllers

Endpoints

Filters

Middleware

Authorization

Swagger

Configuration

```

Contracts

```

Requests

Responses

Events

Enums

Constants

Interfaces

```

---

# Dependency Rules

Allowed

```

API

↓

Application

↓

Domain

↑

Infrastructure

```

Forbidden

```

Controller

↓

Entity Framework

Repository

↓

Business Logic

Domain

↓

Infrastructure

Module

↓

Another Module Infrastructure

```

---

# Naming Conventions

Solutions

```

SSAS.ERP.sln

```

Projects

```

SSAS.Platform.API

SSAS.HR.Application

SSAS.GL.Domain

```

Namespaces

```

SSAS.Modules.HR.Application.Commands

SSAS.Modules.HR.Domain.Entities

SSAS.Modules.HR.Infrastructure.Persistence

```

Classes

PascalCase

Interfaces

IEmployeeRepository

Commands

CreateEmployeeCommand

Queries

GetEmployeeByIdQuery

Handlers

CreateEmployeeCommandHandler

Controllers

EmployeeController

DTO

EmployeeDto

Entities

Employee

Value Objects

EmployeeNumber

---

# Package References

Preferred packages shall be centrally managed.

Examples

- Entity Framework Core
- FluentValidation
- AutoMapper (or approved mapping alternative)
- Serilog
- MediatR (or approved mediator alternative)
- Swashbuckle
- xUnit
- FluentAssertions

Package versions shall be defined centrally using .NET Central Package Management.

---

# Configuration

Configuration sources

```

appsettings.json

↓

appsettings.{Environment}.json

↓

Environment Variables

↓

Secret Store

```

Secrets shall never be committed to source control.

---

# Module Registration

Every module exposes explicit registration extensions.

```

Infrastructure

Add{Module}Infrastructure(IServiceCollection, IConfiguration)

API

Map{Module}Endpoints(IEndpointRouteBuilder)

```

Infrastructure extensions register DbContexts, repositories, Unit of Work implementations, and external adapters. API extensions map endpoints. The Host invokes the known module registrations during application startup and coordinates configuration and middleware only.

Reflection-based runtime module discovery is not used in V1.

---

# Solution Rules

Every module owns

- Database Tables
- Business Logic
- Validation
- APIs
- Contracts

No module may directly reference another module's Infrastructure project.

No module may directly query another module's database tables.

Cross-module business communication must use approved public contracts, integration events, or explicitly authorized module-facing abstractions. Direct references to another module's internal Domain, Application, API, or Infrastructure assemblies are forbidden.

---

# Future Microservice Migration

Every module shall be extractable into an independent service with minimal changes.

The following elements must remain stable during extraction:

- Domain Layer
- Application Layer
- Contracts
- Validation
- Business Rules

Only the hosting model, infrastructure, deployment, and communication mechanisms should require change.

---

# AI Development Requirements

Codex shall:

- Preserve this solution structure.
- Create new projects only within the approved hierarchy.
- Follow dependency rules.
- Avoid introducing circular references.
- Keep business logic inside the Application and Domain layers.
- Use module contracts for cross-module communication.
- Generate test projects alongside production projects when applicable.

Any deviation from this document requires an approved Architecture Decision Record (ADR).
