---
id: ADR-006
title: Adopt JWT Authentication and Claims-Based Authorization
category: Architecture Decision Record
version: 1.0
status: Accepted
date: YYYY-MM-DD
owner: Solution Architecture Team
tags:
  - authentication
  - authorization
  - jwt
  - security
  - identity
depends_on:
  - ADR-001
  - ADR-003
  - ADR-005
used_by:
  - Platform
  - All Modules
---

# ADR-006: Adopt JWT Authentication and Claims-Based Authorization

---

# Status

**Accepted**

---

# Context

SSAS ERP V2 is a multi-tenant SaaS ERP platform.

Users access the application through web and mobile clients using secured REST APIs.

The authentication solution must support:

- Stateless APIs
- Multi-tenancy
- High scalability
- Role-based security
- Permission-based security
- Token refresh
- Future external identity providers
- Modern security practices

---

# Problem Statement

The platform requires a secure authentication and authorization mechanism that:

- Scales horizontally
- Does not rely on server-side session state
- Supports tenant isolation
- Supports fine-grained permissions
- Integrates with ASP.NET Core
- Can be extended to external identity providers in the future

---

# Decision

SSAS ERP V2 shall adopt **JWT Bearer Authentication** with **Claims-Based Authorization**.

Authentication identifies the user.

Authorization determines what the authenticated user is allowed to do.

---

# Authentication Model

The platform shall use:

- JWT Access Token
- Refresh Token

The Access Token authenticates API requests.

The Refresh Token is used to obtain new Access Tokens without requiring the user to log in again.

---

# Token Types

## Access Token

Purpose:

Authenticate API requests.

Characteristics:

- Short lifetime
- Digitally signed
- Contains identity claims
- Contains authorization claims

---

## Refresh Token

Purpose:

Issue a new Access Token.

Characteristics:

- Long lifetime
- Stored securely
- Revocable
- Rotated after successful refresh

Refresh Tokens shall never be used to access business APIs directly.

---

# Authentication Flow

```
User Login

↓

Validate Credentials

↓

Generate Access Token

↓

Generate Refresh Token

↓

Return Tokens

↓

API Requests

↓

Validate JWT

↓

Resolve Tenant

↓

Authorize Request

↓

Execute Business Operation
```

---

# JWT Claims

Every Access Token shall contain at minimum:

- UserId
- TenantId
- CompanyId (if applicable)
- UserName
- Email
- Roles
- Permissions (or permission references)
- SessionId
- Token Identifier (JTI)
- Issued At
- Expiration

Sensitive information shall never be stored in the token.

---

# Tenant Resolution

The authenticated JWT is the primary source for determining the current tenant.

The server shall resolve:

```
TenantId
```

from the validated token.

Client applications shall not specify arbitrary tenant identifiers.

---

# Authorization Model

Authorization is **claims-based** and consists of three levels:

1. Authentication
2. Role-Based Authorization
3. Permission-Based Authorization

---

# Roles

Roles group permissions.

Examples:

- Platform Administrator
- Tenant Administrator
- HR Manager
- HR Officer
- Accountant
- Finance Manager
- Inventory Manager
- Sales Manager
- Employee

Roles simplify permission management but do not replace permissions.

---

# Permissions

Permissions represent the smallest unit of authorization.

Examples:

```
Employee.Create

Employee.Update

Employee.Delete

Employee.View

Payroll.Run

Payroll.Approve

Journal.Post

Journal.Reverse

PurchaseOrder.Approve

User.Manage

Role.Manage
```

Authorization decisions should primarily rely on permissions.

---

# Authorization Policies

The application shall use ASP.NET Core Authorization Policies.

Policies may combine:

- Authentication
- Role requirements
- Permission requirements
- Tenant ownership
- Business rules

Controllers and endpoints shall reference policies rather than implementing authorization logic directly.

---

# Session Management

Each login session shall have a unique SessionId.

Sessions may be revoked independently without affecting other active sessions.

Examples:

- Logout current session
- Logout all devices
- Force administrator logout

---

# Token Lifetime

Access Tokens should have a short expiration period.

Refresh Tokens should have a configurable longer lifetime.

Exact durations are implementation details and may vary by deployment.

---

# Refresh Token Rotation

Refresh Tokens shall be rotated after successful use.

Old Refresh Tokens shall become invalid immediately after rotation.

This reduces the impact of token theft.

---

# Token Revocation

The platform shall support revocation of:

- Individual sessions
- All user sessions
- Tenant sessions (where applicable)

Revoked tokens shall no longer authorize requests.

---

# Password Storage

Passwords shall:

- Never be stored in plain text.
- Be hashed using an approved password hashing algorithm provided by ASP.NET Core Identity.
- Never be reversible.

---

# Multi-Factor Authentication

The architecture shall support optional Multi-Factor Authentication (MFA) in future releases.

This ADR does not require MFA in Version 1 but the authentication model must remain compatible with it.

---

# External Identity Providers

Future versions may integrate with:

- Microsoft Entra ID (Azure AD)
- Google
- OpenID Connect providers
- OAuth 2.0 providers

The authentication architecture shall remain extensible without changing business modules.

---

# API Security

All protected APIs shall:

- Require HTTPS
- Validate JWT signatures
- Validate issuer
- Validate audience
- Validate expiration
- Validate tenant
- Validate authorization policies

Anonymous access is allowed only for explicitly documented endpoints.

---

# Client Storage

Client applications shall store tokens securely.

Access Tokens should not be persisted longer than necessary.

Refresh Tokens require additional protection according to the client platform.

Implementation details depend on the application type (Web, Mobile, Desktop).

---

# Audit Logging

The following events shall be audited:

- Login
- Logout
- Failed Login
- Password Change
- Password Reset
- Token Refresh
- Account Lockout
- Permission Changes
- Role Changes
- Session Revocation

Audit records are immutable.

---

# Alternatives Considered

## JWT Authentication (Selected)

Advantages

- Stateless
- Scalable
- Well supported by ASP.NET Core
- Cloud friendly
- Excellent API support

Disadvantages

- Token revocation requires additional infrastructure.
- Proper key management is essential.

---

## Cookie Authentication

Advantages

- Simple for server-rendered applications.

Disadvantages

- Less suitable for REST APIs.
- Session state management.
- Poor fit for distributed services.

Rejected.

---

## Server Sessions

Advantages

- Simple implementation.

Disadvantages

- Does not scale well.
- Requires centralized session storage.
- Increased operational complexity.

Rejected.

---

# Consequences

Positive

- Stateless authentication.
- Excellent scalability.
- Strong security model.
- Clear separation of authentication and authorization.
- Compatible with cloud-native deployments.
- Easy integration with future identity providers.

Negative

- Token lifecycle management introduces additional complexity.
- Requires careful signing key management.
- Revocation strategy must be implemented correctly.

---

# Implementation Guidelines

Developers and AI assistants shall:

- Use ASP.NET Core Authentication and Authorization.
- Never trust client-supplied identity information.
- Resolve tenant information from validated claims.
- Protect all business APIs.
- Apply authorization policies consistently.
- Keep authentication logic outside business modules.
- Centralize token generation and validation.
- Rotate Refresh Tokens.
- Audit authentication events.

---

# Compliance Rules

Every secured endpoint shall:

- Require authentication unless explicitly documented.
- Validate authorization policies.
- Execute within the authenticated tenant context.
- Reject expired or invalid tokens.
- Reject unauthorized requests.

Security reviews shall verify compliance.

---

# Risks

| Risk | Mitigation |
|------|------------|
| Token theft | Short-lived Access Tokens, Refresh Token rotation, HTTPS |
| Weak passwords | Strong password policy and secure hashing |
| Incorrect authorization | Centralized policies and automated tests |
| Cross-tenant access | Resolve tenant from validated JWT claims and enforce tenant isolation |

---

# Related Documents

- ADR-001 – Modular Monolith
- ADR-003 – Clean Architecture
- ADR-005 – Multi-Tenancy
- Security Standards
- Development Standards
- Sprint-00 Foundation

---

# Review Criteria

This ADR shall be reviewed if:

- The platform adopts a different identity provider.
- Session management requirements change significantly.
- Regulatory requirements mandate additional authentication controls.
- Future versions introduce mandatory MFA or passwordless authentication.

Until then, **JWT Bearer Authentication with Claims-Based Authorization** remains the mandatory security model for SSAS ERP V2.

# Depends On

- ADR-001
- ADR-003
- ADR-005

---

# Related ADRs

| ADR | Relationship |
|------|--------------|
| ADR-004 | Commands and queries require authorization |
| ADR-005 | JWT resolves the active TenantId |
| ADR-007 | Angular uses JWT for authenticated API calls |
| ADR-010 | Repositories execute under the authenticated tenant context |