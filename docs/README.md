# SSAS ERP V2

> Enterprise Multi-Tenant SaaS ERP Platform built using ASP.NET Core, Angular, SQL Server, Clean Architecture, CQRS, and Domain-Driven Design.

---

# Overview

SSAS ERP V2 is a production-grade Enterprise Resource Planning (ERP) platform designed for Software-as-a-Service (SaaS) deployment.

The system is designed to support multiple organizations (tenants) within a single platform while maintaining strict tenant isolation, security, scalability, and maintainability.

The project is built as an **AI-First Engineering Repository**, where architecture, standards, and documentation drive implementation.

---

# Key Features

- Multi-Tenant SaaS
- Modular Monolith Architecture
- Microservice Ready
- Clean Architecture
- CQRS
- Domain-Driven Design (DDD)
- JWT Authentication
- Role & Permission Based Authorization
- Entity Framework Core
- SQL Server
- Angular Frontend
- REST APIs
- AI-Assisted Development

---

# Technology Stack

## Backend

- ASP.NET Core (.NET LTS)
- C#
- Entity Framework Core
- MediatR
- FluentValidation
- AutoMapper
- Serilog

## Frontend

- Angular
- TypeScript
- Angular Material

## Database

- SQL Server

## DevOps

- Docker
- GitHub
- GitHub Actions

---

# Repository Structure

```
docs/
src/
tests/
.github/
```

The **docs** directory contains the complete architecture and implementation guidance.

---

# Documentation

Documentation is organized into numbered sections.

> ### ⚠⚠⚠ **CORRECTED 2026-09-06 — THE LIST BELOW WAS THE INTENDED STRUCTURE, NOT THE ONE ON DISK.**
> **It declared fifteen sections; `docs/` holds eleven directories; they matched on four. *Four numbers
> collide with a different name, seven declared sections do not exist, and three real directories were not
> listed — including the largest one in the repository.*** The list is now what the tree actually contains,
> with the original preserved beneath it.

```
00-Master-Product-Specification
02-Functional
03-Architecture
08-Development
11-AI
12-Feature-Packages
13-Implementation
14-Engineering
15-Tasks
16-Governance
17-features                 <- every feature package, decision register and acceptance criteria
```

> **THE ORIGINAL LIST, PRESERVED:** *`00 Master Product Specification · 01 Business · 02 Requirements ·
> 03 Architecture · 04 Design · 05 Modules · 06 API · 07 Database · 08 Security · 09 Testing · 10 DevOps ·
> 11 AI · 12 Sprints · 13 Standards · 14 Engineering`*
>
> ⚠⚠ ***THE FOUR NUMBER-COLLISIONS WERE THE DANGEROUS PART, BECAUSE THE NUMBER AGREEING MAKES THE WRONG
> DIRECTORY LOOK RIGHT:***
>
> | declared | actually on disk |
> |---|---|
> | `02 Requirements` | **`02-Functional`** |
> | `08 Security` | **`08-Development`** |
> | `12 Sprints` | **`12-Feature-Packages`** |
> | `13 Standards` | **`13-Implementation`** |
>
> **Declared and absent:** `01 Business` · `04 Design` · `05 Modules` · `06 API` · `07 Database` ·
> `09 Testing` · `10 DevOps`.
> ***Present and unlisted:*** `15-Tasks` · `16-Governance` · **`17-features`** — ⚠ *and this same file names
> `docs/17-features/` by path further down, so it omitted the corpus's largest directory from its own map and
> then told the reader to go there.*

---

# Getting Started

Before contributing:

1. Read START-HERE.md
2. Read docs/README.md
3. Read Architecture Principles
4. Read all ADRs
5. ⚠ **Read the approved Feature Packages under `docs/17-features/` and the Git history.** *Corrected
   2026-09-06: this step read "Read the current Sprint documentation", which the Current Development Phase
   section of this same file contradicts — "current implementation state is tracked through approved Feature
   Packages and the Git history, **not through a fixed 'current sprint' marker**". `13-Implementation` and
   `15-Tasks` stop at Sprint-00 and Task-002; the project is at FP-015.*

---

# Development Philosophy

The project follows **Documentation-Driven Development**.

Business Requirements

↓

Architecture

↓

Engineering Standards

↓

Implementation

↓

Testing

↓

Review

↓

Release

Documentation is considered the authoritative source.

---

# Architecture

The architecture is defined by:

- Solution Architecture Document
- Architecture Principles
- Architecture Decision Records (ADRs)

If implementation conflicts with documentation, the documentation takes precedence.

---

# Coding Standards

All code shall comply with:

- **Development Standards** — `docs/08-Development/Development-Standards.md`
- **Coding Standards** — ⚠ *an umbrella term, not a file: Development Standards · Solution Structure ·
  Clean Architecture · Naming Standards · Dependency Rules. The membership list is at*
  `13-Implementation/Sprint-00-Foundation.md:451`. *See the note below.*
- **Architecture Principles** — `docs/14-Engineering/Architecture-Principles.md`
- **ADRs** — `docs/14-Engineering/ADR/`

> ### ⚠⚠⚠ **"CODING STANDARDS" IS REQUIRED BY THIRTEEN FILES AND HAS NEVER EXISTED** *(recorded 2026-09-06)*
>
> **`find docs -iname "*coding*"` returns nothing.** *Thirteen files nonetheless require conformance to it:*
> this file (twice, including the heading above) · `START-HERE.md:191` · `Assumptions.md` (`ASM-0401`) ·
> `Requirement-Catalog/Constraints.md` · `Requirement-Catalog/Non-Functional-Requirements.md` ·
> `11-AI/Codex-System-Prompt.md` · `13-Implementation/Sprint-00-Foundation.md` ·
> `14-Engineering/Architecture-Principles.md` · **and five Accepted ADRs — `ADR-004`, `ADR-007`, `ADR-008`,
> `ADR-010`, `ADR-011`.**
>
> ⚠⚠ **The list above names it *beside* `Development Standards` as though they were two documents.**
> ***`08-Development/Development-Standards.md` exists, is 10.8 KB and is cited by 21 files. "Coding
> Standards" is cited by thirteen and is not there.***
>
> ### ⚠⚠⚠ **RESOLVED 2026-09-06 — AND MY FIRST READING WAS WRONG IN THE DIRECTION THAT WOULD HAVE CAUSED DAMAGE**
>
> **I wrote here that *"the likeliest reading is that they are the same document under two names"* and left
> the owner a choice between repointing thirteen references and authoring a missing standard.**
> ***BOTH OPTIONS WERE WRONG. "CODING STANDARDS" IS AN UMBRELLA TERM NAMING FIVE ARTEFACTS, AND EVERY ONE
> OF THEM EXISTS.***
>
> **`13-Implementation/Sprint-00-Foundation.md:451-463` says so outright:**
> > `# Coding Standards` / `Must follow` / **Development Standards** · Solution Structure · Clean
> > Architecture · Naming Standards · Dependency Rules
>
> ***THE FIRST ENTRY UNDER "CODING STANDARDS" IS "DEVELOPMENT STANDARDS". THEY ARE NOT SYNONYMS — ONE
> CONTAINS THE OTHER.*** **And the members are locatable: `Development-Standards.md`;
> `03-Architecture/Solution-Structure.md`; `Architecture-Principles.md`'s `# Naming Conventions` (`:198`),
> `# Dependency Rules` (`:210`) and `# General Rules` (`:180`); and `11-AI/Codex-System-Prompt.md:192-207`,
> which is itself a `# Coding Standards` section with real content.**
>
> ⚠⚠ ***SO DO NOT REPOINT THESE REFERENCES AT `Development-Standards.md`. THAT WOULD NARROW AN UMBRELLA TERM
> TO ONE OF ITS FIVE MEMBERS***, and a reader following the new link would get development standards and
> lose solution structure, naming and dependency rules. **The bullet is restored below, unpointed, because
> pointing it anywhere single is the error.**
>
> **What is actually missing is one page saying *"Coding Standards means these five"* — ⚠ *and even that
> exists, at `Sprint-00-Foundation.md:451`, in an uncited file inside a layer `START-HERE.md` does not
> list.* The task is to point at it, not to write it.**
>
> *(Of the thirteen references, eleven are bare obligations carrying no subject — including all five ADRs,
> each a lone bullet in a Related Documents list. The two informative ones are not references at all: they
> are headings.)*

---

# Current Development Phase

Current implementation state is tracked through approved Feature Packages and the Git history, not through a fixed "current sprint" marker.

The Foundation and the Platform feature packages (FP-001 Identity & Access, FP-002 Authentication & Token Lifecycle, FP-003 Tenant Lifecycle, FP-004 Localization) are delivered and merged. Read the approved Feature Packages under `docs/17-features/` and the commit history to determine what exists and what comes next.

---

# AI Development

The repository is optimized for AI-assisted software engineering.

Supported AI assistants include:

- Codex
- GitHub Copilot
- ChatGPT
- Claude Code

AI agents must begin with **START-HERE.md** before generating code.

---

# License

Internal Project

All Rights Reserved.