# Task Plan

## Phase 4: FP-014 Subscription Billing API Implementation

- [x] **Task 1: FP-014 Permissions & Authorization Configuration**
  Add the missing permissions defined in `docs/17-features/FP-014-subscription/api-contracts.md`: `Platform.Plans.View`, `Platform.Plans.Administer`, `Platform.Subscriptions.View`, `Platform.Subscriptions.Administer`, `Platform.EntitlementGrants.Administer`, `Platform.Invoices.View`, and `Platform.Invoices.Administer`. Ensure they are registered in the authorization/permission catalogue.

- [ ] **Task 2: FP-014 Plans API**
  Implement the MediatR command/query handlers, HTTP routes, and unit tests for the Plans API under `/api/platform/plans`:
  - `GET /api/platform/plans`
  - `GET /api/platform/plans/{planId}`
  - `POST /api/platform/plans`
  - `PUT /api/platform/plans/{planId}`
  - `POST /api/platform/plans/{planId}/retire`
  - `PUT /api/platform/plans/{planId}/modules`
  - `PUT /api/platform/plans/{planId}/limits`
  - `PUT /api/platform/plans/{planId}/prices`

- [ ] **Task 3: FP-014 Subscriptions API**
  Implement the command/query handlers, HTTP routes, and unit tests for the Subscriptions API (using append-only semantics):
  - `GET /api/platform/tenants/{tenantId}/subscriptions`
  - `GET /api/platform/tenants/{tenantId}/subscriptions/current`
  - `POST /api/platform/tenants/{tenantId}/subscriptions`
  - `GET /api/platform/subscriptions`

- [ ] **Task 4: FP-014 Entitlement Grants API**
  Implement the command/query handlers, HTTP routes, and unit tests for Entitlement Grants:
  - `GET /api/platform/tenants/{tenantId}/grants`
  - `POST /api/platform/tenants/{tenantId}/grants`
  - `POST /api/platform/tenants/{tenantId}/grants/revoke`

- [ ] **Task 5: FP-014 Invoices API**
  Implement the command/query handlers, HTTP routes, and unit tests for Invoices:
  - `GET /api/platform/invoices`
  - `GET /api/platform/invoices/{invoiceId}`
  - `GET /api/platform/tenants/{tenantId}/invoices`
  - `POST /api/platform/invoices`
  - `PUT /api/platform/invoices/{invoiceId}` (draft edits only)
  - `POST /api/platform/invoices/{invoiceId}/issue`
  - `POST /api/platform/invoices/{invoiceId}/void`
  - `GET /api/platform/invoices/{invoiceId}/attempts`

- [ ] **Task 6: FP-014 Tenant Enabled Modules Read API**
  Implement the handler and route for `GET /api/platform/modules/enabled`. This route must be exempt from module enablement checks and available to any authenticated tenant user.
