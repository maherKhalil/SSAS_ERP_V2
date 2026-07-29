# Modular Monolith Architecture

Document ID

DOC-SAD-003

Version

1.0

Status

Approved

---

# Purpose

SSAS ERP V2 shall be implemented as a Modular Monolith.

Each module behaves like an independent microservice while remaining inside one deployable application.

---

# Module Structure

```

Module

├── API

├── Application

├── Domain

├── Infrastructure

└── Contracts

```

Every module owns

- Business Logic
- Entities
- Repositories
- Services
- Events

---

# Module Independence

Modules shall never reference another module's Infrastructure.

Modules shall never query another module's database tables.

Modules communicate only through

- Contracts
- Application Services
- Domain Events

---

# Database Ownership

Each module owns its tables.

Example

Platform

TBL-PLT-*

HR

TBL-HR-*

Finance

TBL-GL-*

---

# Dependency Rules

Allowed

API

↓

Application

↓

Domain

Infrastructure

↓

Domain

Forbidden

HR

↓

Finance Infrastructure

Finance

↓

HR Database

Platform

↓

HR Repository

---

# Future Extraction

Each module shall be independently deployable.

When migrating to microservices

Application Logic

NO CHANGE

Domain Logic

NO CHANGE

Infrastructure

Replace

Database

May Split

API

May Become Service API
