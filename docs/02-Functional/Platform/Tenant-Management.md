# Tenant Management

Feature ID

FR-PLT-0010

Status

Draft

Lifecycle authority

Approved FP-003 (`docs/17-features/FP-003-tenant-lifecycle/`) supersedes this Draft wherever this document describes Tenant identity, status, lifecycle transitions, authentication eligibility, deletion, lifecycle persistence, or lifecycle authorization boundaries. The remaining company, first-administrator, subscription, billing, branding, localization, configuration, contact, notification, and broad onboarding material below is deferred and non-authoritative until covered by an approved feature package.

---

# Requirement References

REQ-PLT-0001

REQ-PLT-0002

REQ-PLT-0003

REQ-PLT-0004

REQ-PLT-0005

---

# Business Rules

BR-PLT-0001

BR-PLT-0008

---

# Description

Tenant Management allows Platform Administrators to create, configure, activate, suspend, and manage customer tenants.

Each tenant represents an independent customer organization sharing the SSAS ERP platform while maintaining complete logical isolation from other tenants.

---

# Actors

Platform Administrator

Support Administrator

---

# Functional Capabilities

- Create Tenant
- View Tenant
- Update Tenant display information (deferred; TenantName mutability is governed by FP-003)
- Suspend Tenant
- Reactivate Tenant
- Archive Tenant
- Assign Subscription (deferred and non-authoritative)
- Configure Branding (deferred and non-authoritative)
- Configure Localization (deferred and non-authoritative)
- Configure Default Settings (deferred and non-authoritative)

---

# Tenant Information

General Information

- Tenant Code
- Tenant Name
- Legal Name
- Status
- Subscription Plan
- Primary Contact
- Contact Email
- Contact Phone

Localization

- Default Language
- Default Time Zone
- Default Currency
- Date Format
- Number Format

Branding

- Logo
- Theme
- Primary Color
- Secondary Color

---

# Business Validations

Tenant Code is globally unique by the exact FP-003 normalization and BIN2 comparison rules.

Tenant Name is not globally unique. It is mutable only through an approved Tenant update operation.

Only an existing Active Tenant is authentication-eligible; Provisioning, Suspended, Archived, and missing Tenants are ineligible.

Archived is terminal and retained. Physical deletion is prohibited.

---

# Deferred workflow (non-authoritative)

The workflow below is not part of FP-003. Tenant creation in FP-003 creates only a Tenant in Provisioning; activation creates no company or first administrator.

Create Tenant

↓

Validate Input

↓

Create Default Company

↓

Create Default Administrator

↓

Assign Subscription

↓

Generate Initial Configuration

↓

Create Audit Record

↓

Send Welcome Email

---

# Draft API (superseded for lifecycle)

No Tenant HTTP endpoint is included in the first FP-003 implementation milestone. Any later endpoint follows the approved FP-003 contracts and Platform-level authorization boundary.

GET    /api/platform/tenants

GET    /api/platform/tenants/{id}

POST   /api/platform/tenants

PUT    /api/platform/tenants/{id}

POST   /api/platform/tenants/{id}/archive

---

# Database

TBL-PLT-Tenant

TBL-PLT-Subscription

TBL-PLT-Company

TBL-PLT-Configuration

---

# Permissions

The identifiers below are Draft and non-authoritative. Exact Platform lifecycle permission identifiers are deferred by FP-003; ordinary tenant roles never authorize Tenant lifecycle operations. No Tenant-delete permission exists.

PER-PLT-ViewTenant

PER-PLT-CreateTenant

PER-PLT-EditTenant

PER-PLT-SuspendTenant


---

# Audit

Tenant Created

Tenant Updated

Tenant Suspended

Tenant Activated

Subscription Changed

---

# Deferred acceptance criteria (non-authoritative)

These broad onboarding criteria are outside FP-003 and require later approved feature packages.

✓ Tenant is created successfully.

✓ Tenant isolation is enforced.

✓ Default company is created.

✓ Administrator account is provisioned.

✓ Subscription is assigned.

✓ Audit entries are recorded.

✓ Welcome notification is sent.

---

# Future Enhancements

Custom Domains

White Label Branding

Multiple Subscription Plans

Tenant Data Export

Tenant Data Archiving
