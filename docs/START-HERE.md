# SSAS ERP V2 - Project Entry Point

**Version:** 1.0

---

# Welcome

This repository contains the complete specification and implementation of **SSAS ERP V2**, a production-grade Multi-Tenant SaaS ERP.

This document is the entry point for:

- Developers
- Architects
- AI Coding Assistants
- Code Reviewers
- New Team Members

---

# Project Goals

Build a modern ERP platform that is:

- Multi-Tenant
- Cloud Native
- Modular Monolith
- Microservice Ready
- Production Ready
- AI Assisted

---

# Technology Stack

Backend

- ASP.NET Core (.NET Latest LTS)

Frontend

- Angular

Database

- SQL Server

Architecture

- Clean Architecture
- Modular Monolith
- CQRS
- DDD Principles

---

# Documentation Structure

00-Master-Product-Specification

Defines WHAT the product is.

---

01-Business

Business vision, processes, and requirements.

---

02-Functional

Functional specifications for each module.

---

03-Architecture

Solution architecture and technical design.

---

04-Database

Database design by feature.

---

05-API

REST API specifications.

---

06-UI

Screen specifications.

---

07-Security

Authentication, authorization, and security design.

---

08-Development

Coding standards and engineering practices.

---

09-Testing

Test plans and acceptance criteria.

---

10-Deployment

Deployment and DevOps documentation.

---

11-AI

Instructions for AI coding assistants.

---

12-Feature-Packages

Implementation units used by AI and developers.

---

13-Implementation

Sprint planning and implementation guides.

---

14-Engineering

Architecture decisions, technical debt, and engineering governance.

---

# Reading Order

Every developer and AI assistant shall read documents in this order:

1. START-HERE.md
2. docs/11-AI/Codex-System-Prompt.md
3. docs/11-AI/AI-Implementation-Workflow.md
4. docs/08-Development/Development-Standards.md
5. docs/03-Architecture/*
6. docs/00-Master-Product-Specification/*
7. Current Sprint
8. Current Feature Package
9. Functional Specification

---

# Development Workflow

Business Requirement

↓

Functional Specification

↓

Database Design

↓

API Design

↓

UI Design

↓

Security

↓

Testing

↓

Implementation

↓

Review

↓

Release

---

# AI Workflow

Read Documentation

↓

Understand Scope

↓

Validate Requirements

↓

Implement

↓

Run Tests

↓

Update Documentation

↓

Commit

↓

Request Review

---

# Golden Rules

- Documentation drives implementation.
- Never bypass architecture.
- Never guess missing requirements.
- One feature package at a time.
- Keep documentation synchronized with code.
- Preserve module boundaries.
- Maintain tenant isolation.
- Favor maintainability over shortcuts.

---

# Current Phase

Sprint 00 – Foundation

No business functionality shall be implemented until Sprint 00 is complete.

---

# Repository Success Criteria

The project is considered healthy when:

- Documentation is current.
- Solution builds successfully.
- Tests pass.
- Architecture rules are respected.
- Code quality gates pass.
- AI-generated code follows project standards.