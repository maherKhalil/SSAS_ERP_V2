# Architecture Decision Records

Every major architectural decision shall be documented.

## ADR-0001

Title

Architecture Style

Status

Accepted

Decision

The application shall be implemented as a Modular Monolith.

Modules must be independently deployable in the future.

Communication between modules shall occur only through contracts and application services.

Direct database access between modules is prohibited.

Future migration to microservices must not require rewriting business logic.

---

## ADR-0002

Title

Technology Stack

Status

Accepted

Backend

.NET

Frontend

Angular

Database

SQL Server

API

REST

Authentication

JWT

---

## ADR-0003

Title

Documentation First

Status

Accepted

All implementation follows documentation.

Documentation changes precede implementation.

Documentation is the project's source of truth.