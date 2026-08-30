# Platform Functional Specification

Module

Platform

Module ID

MOD-PLT

Purpose

The Platform module provides shared functionality used by every ERP module.

Platform capabilities include:

- Authentication
- Authorization
- Tenant Management
- Company Management
- Branch Management
- User Management
- Roles
- Permissions
- Subscription
- Configuration
- Audit
- Notifications


**⚠ Nine areas are listed above and three have documents:** `Authentication.md`, `Branch-Management.md`
and `Tenant-Management.md`. **Company Management, User Management, Roles, Permissions, Subscription,
Configuration, Audit and Notifications have none here.**

**That is not necessarily a gap.** Subscription is specified in `docs/17-features/FP-014-subscription`,
company and user management in their own feature packages. ⚠ **But this list reads as an index and is not
one, and a reader looking for the subscription specification will look here first and find nothing.**
**Stated rather than fixed: moving or deleting entries would be a judgement about where each area's
specification belongs, and that is not settled by this note.**

Every business module depends on Platform.

Business modules shall never reimplement Platform functionality.