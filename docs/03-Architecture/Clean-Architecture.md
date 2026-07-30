# Clean Architecture

Document ID

DOC-SAD-004

Version

1.0

Status

Approved

---

# Purpose

The ERP shall follow Clean Architecture to ensure maintainability, testability, and future scalability.

---

# Layers

Presentation

↓

Application

↓

Domain

↑

Infrastructure

---

# Domain Layer

Contains

Entities

Value Objects

Domain Services

Domain Events

Specifications

Business Rules

No infrastructure dependencies are allowed.

---

# Application Layer

Contains

Commands

Queries

Handlers

DTOs

Interfaces

Validation

Authorization

Use Cases

---

# Infrastructure Layer

Contains

Entity Framework

Repositories

Identity

Caching

Email

Logging

External APIs

File Storage

---

# Presentation Layer

Contains

REST Controllers

Middleware

Filters

Swagger

Authentication

---

# Rules

Business logic never belongs in Controllers.

Business logic never belongs in Repositories.

Repositories never contain validation.

Controllers never access Entity Framework directly.

Only Application Services coordinate use cases.

---

# Composition Root Exception

SSAS.Host.API is the outermost composition root and may reference approved module API and Infrastructure projects solely to register concrete implementations and map endpoints. This exception does not permit business logic in the Host, Infrastructure references from module API projects, or Host and Infrastructure dependencies from Application or Domain projects.
