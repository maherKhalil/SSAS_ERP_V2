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

```
00 Master Product Specification
01 Business
02 Requirements
03 Architecture
04 Design
05 Modules
06 API
07 Database
08 Security
09 Testing
10 DevOps
11 AI
12 Sprints
13 Standards
14 Engineering
```

---

# Getting Started

Before contributing:

1. Read START-HERE.md
2. Read docs/README.md
3. Read Architecture Principles
4. Read all ADRs
5. Read the current Sprint documentation

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

- Development Standards
- Coding Standards
- Architecture Principles
- ADRs

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