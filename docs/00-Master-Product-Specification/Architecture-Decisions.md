# Architecture Decision Records

> ## ⚠⚠⚠ **THIS IS NOT THE ARCHITECTURE DECISION REGISTRY. IT IS A THREE-ENTRY STUB IN A DIFFERENT ID SPACE.** *(pointer added 2026-09-06; nothing below was changed)*
>
> ***THE MAINTAINED REGISTRY IS [`docs/14-Engineering/ADR/`](../14-Engineering/ADR/) — 29 documents,
> `ADR-001`–`ADR-027`, `ADR-029`, `ADR-030` — **29 DOCUMENTS, THREE DIGITS** *(⚠ `ADR-028` is deliberately
> RESERVED and unwritten; `ADR/README.md:38`. Corrected 2026-09-07 — this said `ADR-001 … ADR-030`, which
> implies thirty and hides the gap).*** **This file uses `ADR-0001`–`ADR-0003`, FOUR digits.**
> ⚠ ***The two spaces cannot collide, which is exactly why nobody has noticed: a search for `ADR-001` never
> returns `ADR-0001`.*** **This file has not been touched since the initial commit of 2026-07-29.**
>
> ⚠⚠⚠ **CORRECTED 2026-09-07 — THIS SAID THE FILE IS "CITED BY NOTHING", WHICH IS TRUE OF *THE FILE* AND
> BADLY UNDER-SCOPED ABOUT *THE ID SPACE*.** ***`ADR-0001`–`ADR-0003` ARE CITED FROM SEVEN OTHER
> DOCUMENTS:*** `Assumptions.md:66` · `Constraints.md:64` · `Non-Functional-Requirements.md:201` ·
> `Requirement-Numbering.md:135`, `:137`, `:139` · `03-Architecture/Solution-Architecture.md:35` ·
> **and `START-HERE.md:94`.**
>
> ***SO THE FRONT DOOR AND THE SOLUTION ARCHITECTURE BOTH POINT INTO THE SPACE "NOBODY SEARCHES".*** **That
> makes the four-digit scheme a live cross-reference target rather than a dead stub — and it makes the
> collision hazard worse, not better, because a reader arriving from `START-HERE.md` has been sent here on
> purpose.** *The original framing was right about the registry and wrong about its reach; the rung it
> missed was the id space rather than the file.*
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