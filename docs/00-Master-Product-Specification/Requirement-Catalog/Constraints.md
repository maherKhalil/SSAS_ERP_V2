# Constraints

**Document ID:** MPS-CON-001

**Version:** 1.0

**Status:** Approved

---

# Purpose

This document defines the business, technical, operational, and architectural constraints that govern the implementation of SSAS ERP.

Constraints are mandatory and shall not be violated without an approved Architecture Decision Record (ADR).

---

# Business Constraints

## CON-0001

The application shall operate as a subscription-based Software-as-a-Service (SaaS) platform.

---

## CON-0002

The application shall support multiple independent tenants.

---

## CON-0003

Tenant data shall remain logically isolated.

No business operation may expose another tenant's data.

---

## CON-0004

Version 1 scope is limited to:

- Platform
- Human Resources
- General Ledger
- Reporting
- Notifications
- Audit

Additional modules shall follow the established architecture.

---

# Architecture Constraints

## CON-0100

The system shall follow Clean Architecture.

Reference

ADR-0001

---

## CON-0101

The application shall be implemented as a Modular Monolith with a Microservice Extraction Strategy.

---

## CON-0102

Every module owns its business logic.

---

## CON-0103

Modules shall communicate only through public contracts.

Direct references to another module's implementation are prohibited.

---

## CON-0104

Direct database access between modules is prohibited.

---

## CON-0105

Business logic shall not exist inside Controllers.

---

## CON-0106

Business logic shall not exist inside Infrastructure projects.

---

## CON-0107

Infrastructure dependencies shall point inward.

---

# Database Constraints

## CON-0200

SQL Server is the primary database.

---

## CON-0201

Primary Keys shall use BIGINT Identity unless otherwise approved.

---

## CON-0202

Foreign Keys shall enforce referential integrity where appropriate.

---

## CON-0203

Every business entity shall support auditing.

---

## CON-0204

Soft Delete shall be used unless business requirements explicitly require physical deletion.

---

## CON-0205

UTC shall be used for all persisted timestamps.

---

# API Constraints

## CON-0300

REST shall be the public integration standard.

---

## CON-0301

JSON shall be the default payload format.

---

## CON-0302

API Versioning is mandatory.

---

## CON-0303

Every endpoint requires authorization unless explicitly documented.

---

# UI Constraints

## CON-0400

Angular shall be used for the web client.

---

## CON-0401

Responsive layouts are mandatory.

---

## CON-0402

Arabic and English shall be supported.

---

# Security Constraints

## CON-0500

Authentication is mandatory.

---

## CON-0501

Authorization is Role Based.

---

## CON-0502

Passwords shall never be stored in plain text.

---

## CON-0503

Tenant Context shall be validated on every request.

---

# Development Constraints

## CON-0600

Every implemented feature shall reference at least one Requirement ID.

---

## CON-0601

Every API shall reference a Functional Requirement.

---

## CON-0602

Every database table shall belong to one module.

---

## CON-0603

Every pull request shall update documentation when requirements change.

---

## CON-0604

Source code shall comply with Coding Standards.

---

# AI Development Constraints

## CON-0700

AI-generated code shall follow all project documentation.

---

## CON-0701

Generated code shall not bypass architecture.

---

## CON-0702

Generated code shall preserve module boundaries.

---

# Change Control

Any constraint changes require:

- Architecture review
- Updated ADR
- Documentation update
- Impact assessment