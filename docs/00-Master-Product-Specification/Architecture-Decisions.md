# Architecture Decision Records

> ## ⚠⚠⚠ **THIS IS NOT THE ARCHITECTURE DECISION REGISTRY. IT IS A THREE-ENTRY STUB IN A DIFFERENT ID SPACE.** *(pointer added 2026-09-06; nothing below was changed)*
>
> ***THE MAINTAINED REGISTRY IS [`docs/14-Engineering/ADR/`](../14-Engineering/ADR/) — 29 documents,
> `ADR-001` … `ADR-030`, THREE DIGITS.*** **This file uses `ADR-0001`–`ADR-0003`, FOUR digits.**
> ⚠ ***The two spaces cannot collide, which is exactly why nobody has noticed: a search for `ADR-001` never
> returns `ADR-0001`.*** **This file has not been touched since the initial commit of 2026-07-29 and is cited
> by nothing.**
>
> **Where each entry now lives — checked, not assumed:**
>
> | here | subject | the maintained record |
> |---|---|---|
> | `ADR-0001` | Architecture Style — Modular Monolith | **`ADR-001-Modular-Monolith.md`** — same subject, far fuller |
> | `ADR-0002` | Technology Stack | **split across `ADR-002` (SQL Server), `ADR-006` (JWT), `ADR-007` (Angular), `ADR-008` (EF Core)** |
> | `ADR-0003` | Documentation First | ⚠⚠ ***NO COUNTERPART. Searched the whole ADR directory: nothing states it.*** |
>
> ***SO THIS FILE MUST NOT SIMPLY BE DELETED OR MARKED SUPERSEDED.*** **`ADR-0003` — *"All implementation
> follows documentation. Documentation changes precede implementation. Documentation is the project's source
> of truth"* — is arguably the most load-bearing working rule this project has, and this uncited stub is the
> only place it is written down.** *Where it should live is an owner question; that it would be lost is not.*

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