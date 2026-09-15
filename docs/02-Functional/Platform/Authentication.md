# Authentication

Feature ID

FR-PLT-0001

Status

Draft — **and staying Draft; this is a decision, not an omission.** The known-wrong
expired-subscription row was corrected 2026-08-30, so that particular objection is gone, but the
document still disclaims its own content two paragraphs below: five approved feature packages
supersede it in their areas, and the Remember Me, theme, Company-status-gates-login, audit and
notification material is explicitly non-authoritative. A document whose text says *the package
governs where we disagree* cannot be promoted to a specification without first resolving each of
those, which is a larger piece of work than correcting one row. Recorded here so the next sweep
reads an answer instead of re-asking the question.

Lifecycle authority

Approved FP-002 (`docs/17-features/FP-002-authentication-token-lifecycle/`) supersedes this Draft wherever this document describes credential verification, login, post-authentication tenant selection, session and JWT access-token issuance, token claim content, refresh-token rotation, logout, revocation, password reset, lockout, or the authentication API routes. FP-002's `/api/platform/auth/*` routes supersede every `/api/auth/*` route named below. Where this document and FP-002 disagree, FP-002 governs: this document states the access token carries a Company ID, and FP-002 excludes `CompanyId` from token claims.

Approved FP-001 (`docs/17-features/FP-001-identity-access/`) supersedes this Draft wherever this document describes users, actors, roles, role assignment, or the permission model.

Approved FP-003 (`docs/17-features/FP-003-tenant-lifecycle/`) supersedes this Draft wherever this document describes Tenant status or tenant authentication eligibility. Only an `Active` Tenant is authentication-eligible.

Approved FP-004 (`docs/17-features/FP-004-localization/`) supersedes this Draft wherever this document describes language selection or language resolution.

Approved FP-005 (`docs/17-features/FP-005-company-legal-entity/`) supersedes this Draft wherever this document describes Company identity or Company status.

The expired-subscription login rule is no longer stated here as a rule, and **it is no longer true**. `OD-SUB-0009` ruled a term exists and was **amended 2026-08-26 by `DEC-L-033`: expiry gates modules and never blocks login**, because a lapsed customer who cannot log in cannot reach the page that would let them subscribe. It is carried as `REQ-SUB-0018` in FP-014 (`docs/17-features/FP-014-subscription/`), **ratified 2026-08-25 and — contrary to that package's own README — implemented**: eight domain types, two EF configurations, two migrations, and a passing gate suite. This document cites that requirement and does not restate it, so that one ruled rule does not exist in two places to drift apart. The *Failure Scenarios* entry below survived that amendment for four months and is corrected there rather than deleted, because the refusal it names is real and lands on a different surface.

The remaining Remember Me, theme selection, whether Company status gates login, audit, notification, and future-enhancement material below is deferred and non-authoritative until covered by an approved feature package. FP-002 names immutable audit storage, notification delivery, MFA and external identity providers among its own deferrals, so no approved package specifies them today. This account is what a section-by-section sweep on 2026-08-25 found; it is not a claim of exhaustiveness.

---

# Requirement References

REQ-PLT-0021

NFR-0300

---

# Business Rules

BR-PLT-0100

BR-PLT-0101

---

# Description

The Authentication feature verifies the identity of users before granting access to SSAS ERP.

Authentication is mandatory for all protected resources.

---

# Actors

System Administrator

Company Administrator

HR User

Finance User

Employee (future)

---

# Preconditions

User account exists.

Account is active.

Tenant is active.

Company is active.

---

# Workflow

User opens Login page.

↓

User enters Username or Email.

↓

User enters Password.

↓

System validates credentials.

↓

System determines Tenant Context.

↓

System determines Company Context.

↓

System loads Roles.

↓

System loads Permissions.

↓

JWT Access Token generated.

↓

Refresh Token generated.

↓

User redirected to Dashboard.

---

# Business Validations

Username must be unique.

Password cannot be empty.

Inactive users cannot login.

Locked users cannot login.

Suspended tenants cannot login.

Inactive companies cannot login.

Subscription expiry does **not** refuse login: see `REQ-SUB-0018` (`OD-SUB-0009` as amended by `DEC-L-033`). An expired tenant authenticates and is refused every gated module.

---

# Failure Scenarios

Invalid Username

Invalid Password

Locked Account

Expired Password

Inactive Tenant

Inactive Company

Expired Subscription — **not a login failure, and never was one in the shipped product.** `REQ-SUB-0018` (`OD-SUB-0009` as amended by `DEC-L-033`): an expired tenant **authenticates successfully**, reaches the platform plane — its account, its users, and the subscription surface itself — and is refused every **gated module** with `403`. Verified end to end 2026-08-30: authentication eligibility is a total function of `TenantStatus` and consults no subscription, and the entitlement resolver that does read expiry is reachable only from `RequireModule`. A suspended or archived tenant *is* refused at authentication, but that is administrative and this is commercial; `OD-SUB-0010` holds the two dimensions orthogonal and expiry never writes `TenantStatus`. The entry is kept because a reader looking for expiry looks here first.

Unexpected Error

---

# UI

Login Screen

Forgot Password

Remember Me

Language Selection

Theme Selection

---

# API

POST

/api/auth/login

POST

/api/auth/logout

POST

/api/auth/refresh

POST

/api/auth/forgot-password

POST

/api/auth/reset-password

---

# Database

TBL-PLT-User

TBL-PLT-Role

TBL-PLT-UserRole

TBL-PLT-Permission

TBL-PLT-Tenant

TBL-PLT-Company

---

# Permissions

Anonymous

Authenticated

---

# Audit

Successful Login

Failed Login

Logout

Password Reset

Refresh Token

---

# Notifications

Password Reset Email

Account Locked

Password Changed

---

# Acceptance Criteria

✓ Valid users login successfully.

✓ Invalid users receive appropriate errors.

✓ JWT contains Tenant ID.

✓ JWT contains Company ID.

✓ JWT contains Roles.

✓ Refresh Token issued.

✓ Audit entry created.

✓ Last Login updated.

---

# Future Enhancements

Multi-Factor Authentication

Single Sign-On (Azure AD)

Google Authentication

Microsoft Authentication

Passwordless Authentication