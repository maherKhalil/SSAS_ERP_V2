# Solution Architecture

Document ID

DOC-SAD-002

Version

1.0

Status

Approved

---

# Purpose

This document describes the overall architecture of SSAS ERP V2.

It serves as the primary technical blueprint for implementation.

---

# Architecture Style

Modular Monolith

Future Migration

Microservices

Reference

ADR-0001

---

# High Level Architecture

```

Browser

↓

Angular

↓

REST API

↓

Application Layer

↓

Domain Layer

↓

Infrastructure Layer

↓

SQL Server

```

---

# Logical Layers

Presentation

↓

Application

↓

Domain

↓

Infrastructure

↓

Database

Dependencies always point inward.

---

# Modules

Platform

HR

Finance

Reporting

Notifications

Shared

Future

Payroll

Inventory

CRM

Manufacturing

Projects

Assets

---

# Execution Context Dimensions

Tenant

Company

Branch

Tenant is the isolation boundary. Company is a legal entity within a tenant. Branch is an operating location within a tenant.

Company and Branch are sibling dimensions beneath the tenant, not nested.

```

Tenant

├── Company        legal entity          (ADR-014)

└── Branch         operating location    (ADR-023)

      └── Branch-owned business data

```

Branch lives in the tenant database. User branch authorization lives in the platform database. No foreign key crosses between them.

The active branch is held on the authentication session as durable execution context. It is not authorization proof, and is re-authorized against live state on every branch-owned write.

Reference

ADR-014

ADR-023

---

# Cross-Cutting Services

Authentication

Authorization

Logging

Caching

Configuration

Localization

Notifications

Audit

File Storage

Background Jobs

Monitoring

---

# External Integrations

Email

SMS

Identity Provider

Payment Gateway

Future ERP Integrations

Government APIs

---

# Design Principles

Single Responsibility

Open Closed

Liskov

Interface Segregation

Dependency Inversion

---

# Scalability Strategy

Vertical Scaling

Horizontal Scaling

Future Service Extraction

Database Optimization

Caching

---

# Deployment

Cloud Ready

Docker

Future Kubernetes

CI/CD

GitHub Actions

---

# Security

JWT

HTTPS

Role Based Authorization

Tenant Isolation

Audit Logging

Encryption

---

# Quality Attributes

Maintainability

Performance

Availability

Scalability

Security

Testability

Observability
